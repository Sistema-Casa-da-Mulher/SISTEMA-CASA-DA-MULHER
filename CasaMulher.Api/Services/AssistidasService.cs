using System.Globalization;
using System.Security.Claims;
using System.Text;
using CasaMulher.Api.Data;
using CasaMulher.Api.Models;
using CasaMulher.Api.Security;
using Microsoft.EntityFrameworkCore;

namespace CasaMulher.Api.Services;

public sealed record UsuarioAtuante(
    string UserId,
    string Perfil,
    string Identificador,
    string Nome,
    string? Email);

/// <summary>
/// Regras compartilhadas do cadastro único de assistidas: quem vê o quê, validações
/// e importação do seed fictício de homologação.
/// </summary>
public sealed class AssistidasService
{
    // Mesma lista de cursos da Recepção (CURSOS_RECEPCAO em telas/app.js).
    private static readonly Dictionary<string, string> CursosRecepcao = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Informatica"] = "Informática e Inclusão Digital",
        ["Culinaria"] = "Culinária e Autonomia",
        ["Estetica"] = "Estética e Autoestima",
        ["Primeiros Socorros"] = "Primeiros Socorros",
        ["Danca"] = "Dança Circular",
        ["Pilates"] = "Pilates e Bem-estar",
        ["Empoderamento"] = "Empoderamento Feminino"
    };

    public const string DirecionamentoJuridico = "Juridica";
    public const string DirecionamentoPsicologia = "Psicologa";

    private readonly AppDbContext _dbContext;
    private readonly ILogger<AssistidasService> _logger;

    public AssistidasService(AppDbContext dbContext, ILogger<AssistidasService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<UsuarioAtuante?> ObterUsuarioAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        var usuario = await _dbContext.Users.AsNoTracking()
            .SingleOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (usuario is null)
        {
            return null;
        }

        return new UsuarioAtuante(
            usuario.Id,
            (principal.FindFirstValue("perfil") ?? usuario.Perfil).ToLowerInvariant(),
            principal.FindFirstValue("identificadorFuncionario") ?? usuario.IdentificadorFuncionario,
            usuario.NomeCompleto,
            usuario.Email);
    }

    /// <summary>Perfis que trabalham como Jurídico (EQP só chega aqui em dev/homologação).</summary>
    public static bool AtuaComoJuridico(string perfil)
    {
        return perfil is PerfisAcesso.Juridico or PerfisAcesso.Equipe;
    }

    /// <summary>Perfis que trabalham como Psicologia (EQP só chega aqui em dev/homologação).</summary>
    public static bool AtuaComoPsicologia(string perfil)
    {
        return perfil is PerfisAcesso.Psicologia or PerfisAcesso.Equipe;
    }

    /// <summary>Quem enxerga assistidas com acesso restrito.</summary>
    public static bool PodeVerRestritas(string perfil)
    {
        return perfil is PerfisAcesso.Adm or PerfisAcesso.Juridico or PerfisAcesso.Psicologia or PerfisAcesso.Equipe;
    }

    public static bool PodeAlterarRestricao(string perfil)
    {
        return perfil is PerfisAcesso.Adm or PerfisAcesso.Juridico or PerfisAcesso.Psicologia or PerfisAcesso.Equipe;
    }

    public static bool PodeRemover(string perfil)
    {
        return perfil is PerfisAcesso.Adm or PerfisAcesso.Recepcao or PerfisAcesso.Equipe;
    }

    public static string SomenteDigitos(string? valor)
    {
        return new string((valor ?? string.Empty).Where(char.IsDigit).ToArray());
    }

    public static bool CpfValido(string? cpf)
    {
        var digitos = SomenteDigitos(cpf);

        if (digitos.Length != 11 || digitos.Distinct().Count() == 1)
        {
            return false;
        }

        int Digito(int quantidade)
        {
            var soma = 0;
            for (var i = 0; i < quantidade; i++)
            {
                soma += (digitos[i] - '0') * (quantidade + 1 - i);
            }

            var resto = soma * 10 % 11;
            return resto == 10 ? 0 : resto;
        }

        return Digito(9) == digitos[9] - '0' && Digito(10) == digitos[10] - '0';
    }

    public static string FormatarCpf(string cpf)
    {
        return cpf.Length == 11
            ? $"{cpf[..3]}.{cpf[3..6]}.{cpf[6..9]}-{cpf[9..]}"
            : cpf;
    }

    public static string MascararCpf(string cpf)
    {
        return cpf.Length == 11 ? $"***.{cpf[3..6]}.{cpf[6..9]}-**" : "---";
    }

    public static string NormalizarTexto(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return string.Empty;
        }

        var decomposto = texto.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposto.Length);

        foreach (var caractere in decomposto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(caractere) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(caractere);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
    }

    public static bool CursoCorresponde(string? direcionamento, string? cursoProfessor)
    {
        if (string.IsNullOrWhiteSpace(direcionamento) || string.IsNullOrWhiteSpace(cursoProfessor))
        {
            return false;
        }

        var alvo = NormalizarTexto(cursoProfessor);

        if (NormalizarTexto(direcionamento) == alvo)
        {
            return true;
        }

        return CursosRecepcao.TryGetValue(direcionamento, out var nome) && NormalizarTexto(nome) == alvo;
    }

    /// <summary>
    /// Em homologação, leva os cadastros fictícios do seed para o banco uma única vez,
    /// substituindo o antigo preenchimento do localStorage pelo navegador.
    /// </summary>
    public async Task<int> ImportarSeedAsync(HomologacaoSeedDocument? documento, CancellationToken cancellationToken = default)
    {
        if (documento is null || documento.Recepcao.Count == 0)
        {
            return 0;
        }

        if (await _dbContext.Assistidas.AnyAsync(cancellationToken))
        {
            return 0;
        }

        var cpfsUsados = new HashSet<string>();
        var agora = DateTime.UtcNow;

        foreach (var item in documento.Recepcao)
        {
            var cpf = SomenteDigitos(item.Cpf);

            if (cpf.Length != 11 || !cpfsUsados.Add(cpf))
            {
                continue;
            }

            _dbContext.Assistidas.Add(new Assistida
            {
                Nome = item.Nome.Trim(),
                Cpf = cpf,
                Telefone = string.IsNullOrWhiteSpace(item.Telefone) ? null : item.Telefone.Trim(),
                DataNascimento = DateOnly.TryParse(item.DataNascimento, CultureInfo.InvariantCulture, out var data) ? data : null,
                Direcionamento = string.IsNullOrWhiteSpace(item.Curso) ? null : item.Curso.Trim(),
                EncaminhadaJuridicoEm = string.Equals(item.Curso, DirecionamentoJuridico, StringComparison.OrdinalIgnoreCase) ? agora : null,
                EncaminhadaPsicologiaEm = string.Equals(item.Curso, DirecionamentoPsicologia, StringComparison.OrdinalIgnoreCase) ? agora : null,
                Email = string.IsNullOrWhiteSpace(item.Email) ? null : item.Email.Trim(),
                Endereco = string.IsNullOrWhiteSpace(item.Endereco) ? null : item.Endereco.Trim(),
                Observacoes = string.IsNullOrWhiteSpace(item.Observacoes) ? null : item.Observacoes.Trim(),
                Origem = AssistidaOrigem.Recepcao,
                CadastradaPorUserId = "seed-homologacao",
                CadastradaPorIdentificador = "SEED",
                CadastradaPorNome = string.IsNullOrWhiteSpace(item.Atendido) ? "Seed de homologação" : item.Atendido.Trim(),
                CriadoEm = agora,
                AtualizadoEm = agora
            });
        }

        var total = await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Seed de homologação: {Total} assistidas fictícias importadas para o banco.", total);
        return total;
    }
}
