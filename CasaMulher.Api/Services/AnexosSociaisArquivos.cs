using System.Security.Cryptography;
using System.Text;

namespace CasaMulher.Api.Services;

public sealed record ArquivoAnexoValidado(string NomeArquivo, string ContentType, string Extensao);

/// <summary>
/// Validação e criptografia dos anexos do Serviço Social.
/// O arquivo fica no banco (o disco do Render gratuito é apagado a cada deploy), cifrado com
/// AES-256-GCM, o mesmo esquema do snapshot do banco (<see cref="HmlDbSnapshotCrypto"/>).
/// A chave é ANEXOS_CRYPTO_KEY ou, se ela não existir, a mesma HML_DB_SNAPSHOT_KEY do snapshot.
/// Fora de produção, sem nenhuma das duas, usa uma chave de desenvolvimento derivada do Jwt:Key.
/// </summary>
public sealed class AnexosSociaisArquivos
{
    public const long TamanhoMaximoBytes = 5 * 1024 * 1024;

    public const string MensagemTipoInvalido = "Envie só PDF, JPG ou PNG.";

    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("CMANX01");

    private static readonly Dictionary<string, string> ContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png"
    };

    private readonly byte[]? _chave;

    public AnexosSociaisArquivos(IConfiguration configuration, IWebHostEnvironment environment, ILogger<AnexosSociaisArquivos> logger)
    {
        var configurada = configuration["ANEXOS_CRYPTO_KEY"];
        if (string.IsNullOrWhiteSpace(configurada)) configurada = configuration["HML_DB_SNAPSHOT_KEY"];

        if (!string.IsNullOrWhiteSpace(configurada))
        {
            _chave = HmlDbSnapshotCrypto.ParseKey(configurada);
        }
        else if (!environment.IsProduction() && !string.IsNullOrWhiteSpace(configuration["Jwt:Key"]))
        {
            _chave = SHA256.HashData(Encoding.UTF8.GetBytes("CasaMulher.AnexosSociais.Desenvolvimento|" + configuration["Jwt:Key"]));
            logger.LogWarning("Anexos do Serviço Social usando chave de desenvolvimento. Configure ANEXOS_CRYPTO_KEY ou HML_DB_SNAPSHOT_KEY no servidor.");
        }
        else
        {
            logger.LogWarning("Anexos do Serviço Social desativados: configure ANEXOS_CRYPTO_KEY ou HML_DB_SNAPSHOT_KEY.");
        }

        LimiteTotalBytes = configuration.GetValue("ANEXOS_LIMITE_TOTAL_MB", 60L) * 1024 * 1024;
    }

    /// <summary>Usado nos testes.</summary>
    public AnexosSociaisArquivos(byte[] chave, long limiteTotalBytes = long.MaxValue)
    {
        _chave = chave;
        LimiteTotalBytes = limiteTotalBytes;
    }

    public bool Disponivel => _chave is not null;

    /// <summary>
    /// Teto da soma dos anexos guardados. O banco inteiro vai para o snapshot no GitHub, que
    /// não aceita arquivos muito grandes; acima disso novos anexos são recusados.
    /// </summary>
    public long LimiteTotalBytes { get; }

    /// <summary>Confere extensão, tamanho e assinatura do arquivo (os primeiros bytes), não só o nome.</summary>
    public static (ArquivoAnexoValidado? Arquivo, string? Erro) Validar(string? nomeOriginal, byte[] dados)
    {
        if (dados.Length == 0) return (null, "O arquivo está vazio.");
        if (dados.Length > TamanhoMaximoBytes) return (null, "O arquivo passa de 5 MB. Reduza ou digitalize de novo com menos resolução.");

        var nome = LimparNome(nomeOriginal);
        var extensao = Path.GetExtension(nome).ToLowerInvariant();
        if (!ContentTypes.TryGetValue(extensao, out var pelaExtensao)) return (null, MensagemTipoInvalido);

        var pelaAssinatura = TipoPelaAssinatura(dados);
        if (pelaAssinatura is null || pelaAssinatura != pelaExtensao)
        {
            return (null, "O conteúdo do arquivo não confere com a extensão. Envie só PDF, JPG ou PNG verdadeiros.");
        }

        return (new ArquivoAnexoValidado(nome, pelaAssinatura, extensao), null);
    }

    public static string? TipoPelaAssinatura(ReadOnlySpan<byte> dados)
    {
        if (dados.StartsWith("%PDF-"u8)) return "application/pdf";
        if (dados.Length >= 3 && dados[0] == 0xFF && dados[1] == 0xD8 && dados[2] == 0xFF) return "image/jpeg";
        if (dados.StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A })) return "image/png";
        return null;
    }

    public static string LimparNome(string? nome)
    {
        var baseNome = Path.GetFileName((nome ?? string.Empty).Replace('\\', '/').Split('/').Last());
        var limpo = new string(baseNome.Where(c => !char.IsControl(c) && c is not ('"' or '<' or '>' or '|' or ':' or '*' or '?')).ToArray()).Trim();
        if (limpo.Length == 0) limpo = "documento";
        if (limpo.Length <= 160) return limpo;

        var extensao = Path.GetExtension(limpo);
        return limpo[..(160 - extensao.Length)] + extensao;
    }

    public static string Hash(byte[] dados) => Convert.ToHexString(SHA256.HashData(dados)).ToLowerInvariant();

    public byte[] Criptografar(byte[] dados)
    {
        var chave = _chave ?? throw new InvalidOperationException("Chave dos anexos não configurada.");
        var nonce = RandomNumberGenerator.GetBytes(12);
        var tag = new byte[16];
        var cifrado = new byte[dados.Length];
        using var aes = new AesGcm(chave, tag.Length);
        aes.Encrypt(nonce, dados, cifrado, tag, Magic);

        var resultado = new byte[Magic.Length + nonce.Length + tag.Length + cifrado.Length];
        Magic.CopyTo(resultado, 0);
        nonce.CopyTo(resultado, Magic.Length);
        tag.CopyTo(resultado, Magic.Length + nonce.Length);
        cifrado.CopyTo(resultado, Magic.Length + nonce.Length + tag.Length);
        return resultado;
    }

    public byte[] Descriptografar(byte[] guardado)
    {
        var chave = _chave ?? throw new InvalidOperationException("Chave dos anexos não configurada.");
        var cabecalho = Magic.Length + 12 + 16;
        if (guardado.Length < cabecalho || !guardado.AsSpan(0, Magic.Length).SequenceEqual(Magic))
        {
            throw new CryptographicException("Formato de anexo inválido.");
        }

        var dados = new byte[guardado.Length - cabecalho];
        using var aes = new AesGcm(chave, 16);
        aes.Decrypt(
            guardado.AsSpan(Magic.Length, 12),
            guardado.AsSpan(cabecalho),
            guardado.AsSpan(Magic.Length + 12, 16),
            dados,
            Magic);
        return dados;
    }
}
