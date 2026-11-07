using System.Globalization;
using System.Security.Cryptography;
using CasaMulher.Api.Data;
using CasaMulher.Api.DTOs;
using CasaMulher.Api.Models;
using CasaMulher.Api.Security;
using CasaMulher.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CasaMulher.Api.Controllers;

/// <summary>
/// Anexos do prontuário social (RG, comprovantes, BO, medida protetiva...).
/// Só PDF, JPG e PNG de até 5 MB, conferidos pela assinatura do arquivo. O arquivo fica no banco,
/// criptografado; o download passa sempre por aqui (nunca há link público) e cada envio,
/// download, recusa e exclusão vai para o histórico de ações.
/// Recepção e Coordenação ficam de fora já na política de acesso.
/// </summary>
[ApiController]
[Route("api/anexos-sociais")]
[Authorize(Policy = PoliticasAcesso.AnexosEquipeTecnica)]
public class AnexosSociaisController : ControllerBase
{
    private const long LimiteRequisicao = AnexosSociaisArquivos.TamanhoMaximoBytes + 512 * 1024;

    private readonly AppDbContext _dbContext;
    private readonly AssistidasService _assistidas;
    private readonly ServicoSocialAcessoService _acesso;
    private readonly AnexosSociaisArquivos _arquivos;
    private readonly IAuditoriaService _auditoria;
    private readonly SecuritySnapshotPersistenceService _snapshot;

    public AnexosSociaisController(
        AppDbContext dbContext,
        AssistidasService assistidas,
        ServicoSocialAcessoService acesso,
        AnexosSociaisArquivos arquivos,
        IAuditoriaService auditoria,
        SecuritySnapshotPersistenceService snapshot)
    {
        _dbContext = dbContext;
        _assistidas = assistidas;
        _acesso = acesso;
        _arquivos = arquivos;
        _auditoria = auditoria;
        _snapshot = snapshot;
    }

    [HttpPost("prontuarios/{assistidaId:int}")]
    [RequestSizeLimit(LimiteRequisicao)]
    [RequestFormLimits(MultipartBodyLengthLimit = LimiteRequisicao)]
    public async Task<ActionResult<AnexoSocialResponse>> Enviar(
        int assistidaId,
        [FromForm] IFormFile? arquivo,
        [FromForm] string? tipo,
        [FromForm] string? descricao,
        [FromForm] string? visibilidade,
        [FromForm] int? atendimentoId,
        CancellationToken cancellationToken)
    {
        var usuario = await _assistidas.ObterUsuarioAsync(User, cancellationToken);
        if (usuario is null) return Unauthorized();
        if (!AssistidasService.AtuaComoServicoSocial(usuario.Perfil)) return Proibido("Somente o Serviço Social anexa documentos.");
        if (!_arquivos.Disponivel) return StatusCode(StatusCodes.Status503ServiceUnavailable, new { mensagem = "Anexos indisponíveis: a chave de criptografia não está configurada no servidor." });

        if (!ServicoSocialCatalogo.TiposAnexo.Contains(tipo)) return BadRequest(new { mensagem = "Escolha o tipo do documento." });
        var visivel = string.IsNullOrWhiteSpace(visibilidade) ? AnexoSocialVisibilidade.ServicoSocial : visibilidade;
        if (!AnexoSocialVisibilidade.Todos.Contains(visivel)) return BadRequest(new { mensagem = "Visibilidade do anexo inválida." });
        if ((descricao?.Trim().Length ?? 0) > 200) return BadRequest(new { mensagem = "A descrição passou de 200 caracteres." });
        if (arquivo is null || arquivo.Length == 0) return BadRequest(new { mensagem = "Escolha o arquivo." });
        if (arquivo.Length > AnexosSociaisArquivos.TamanhoMaximoBytes) return BadRequest(new { mensagem = "O arquivo passa de 5 MB. Reduza ou digitalize de novo com menos resolução." });

        var (prontuario, acesso) = await CarregarAsync(assistidaId, usuario, cancellationToken);
        if (prontuario is null) return NotFound(new { mensagem = "Abra o prontuário antes de anexar documentos." });
        if (!acesso.PodeVerConteudo) return Proibido("Você não tem acesso ao conteúdo deste prontuário.");

        if (atendimentoId is not null
            && !await _dbContext.AtendimentosSociais.AnyAsync(a => a.Id == atendimentoId && a.ProntuarioSocialId == prontuario.Id, cancellationToken))
        {
            return BadRequest(new { mensagem = "Atendimento não pertence a este prontuário." });
        }

        byte[] dados;
        await using (var memoria = new MemoryStream())
        {
            await arquivo.CopyToAsync(memoria, cancellationToken);
            dados = memoria.ToArray();
        }

        var (validado, erro) = AnexosSociaisArquivos.Validar(arquivo.FileName, dados);
        if (validado is null) return BadRequest(new { mensagem = erro });

        var totalGuardado = await _dbContext.AnexosSociais.SumAsync(a => (long?)a.TamanhoBytes, cancellationToken) ?? 0;
        if (totalGuardado + dados.Length > _arquivos.LimiteTotalBytes)
        {
            return BadRequest(new { mensagem = "O espaço para anexos do sistema está cheio. Avise a coordenação do projeto." });
        }

        var anexo = new AnexoSocial
        {
            ProntuarioSocialId = prontuario.Id,
            AtendimentoSocialId = atendimentoId,
            Tipo = tipo!,
            Descricao = string.IsNullOrWhiteSpace(descricao) ? null : descricao.Trim(),
            NomeArquivo = validado.NomeArquivo,
            ContentType = validado.ContentType,
            TamanhoBytes = dados.Length,
            Sha256 = AnexosSociaisArquivos.Hash(dados),
            Visibilidade = visivel,
            AutorUserId = usuario.UserId,
            AutorIdentificador = usuario.Identificador,
            AutorNome = usuario.Nome,
            CriadoEm = DateTime.UtcNow,
            Conteudo = new AnexoSocialConteudo { Dados = _arquivos.Criptografar(dados) }
        };
        _dbContext.AnexosSociais.Add(anexo);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditoria.RegistrarAsync(
            "SOCIAL_ANEXO_ENVIADO",
            "AnexoSocial",
            anexo.Id.ToString(CultureInfo.InvariantCulture),
            $"{usuario.Identificador} anexou um documento ({anexo.Tipo}, {anexo.ContentType}, {anexo.TamanhoBytes} bytes, visibilidade {anexo.Visibilidade}) ao prontuário social {prontuario.Numero}.");

        await _snapshot.PersistAsync("social_anexo", CancellationToken.None);

        var numero = atendimentoId is null
            ? new Dictionary<int, int>()
            : await _dbContext.AtendimentosSociais.Where(a => a.Id == atendimentoId).ToDictionaryAsync(a => a.Id, a => a.Numero, cancellationToken);
        return Ok(ServicoSocialController.MapearAnexo(anexo, numero, podeExcluir: true));
    }

    /// <summary>
    /// Anexos que quem pede pode ver para a assistida: o Serviço Social vê pelas regras do prontuário;
    /// Jurídico e Psicologia responsáveis veem só os marcados como "equipe técnica".
    /// </summary>
    [HttpGet("assistidas/{assistidaId:int}")]
    public async Task<ActionResult<AnexosCompartilhadosResponse>> Listar(int assistidaId, CancellationToken cancellationToken)
    {
        var usuario = await _assistidas.ObterUsuarioAsync(User, cancellationToken);
        if (usuario is null) return Unauthorized();

        var (prontuario, acesso) = await CarregarAsync(assistidaId, usuario, cancellationToken);
        if (prontuario is null) return Ok(new AnexosCompartilhadosResponse());

        var tecnico = !acesso.PodeVerConteudo && await _acesso.ResponsavelTecnicoAsync(assistidaId, usuario, cancellationToken);
        if (!acesso.PodeVerConteudo && !tecnico)
        {
            return Proibido("Os documentos do Serviço Social ficam só com quem acompanha a assistida.");
        }

        var anexos = await _dbContext.AnexosSociais.AsNoTracking()
            .Where(a => a.ProntuarioSocialId == prontuario.Id && !a.Excluido)
            .OrderByDescending(a => a.CriadoEm)
            .ToListAsync(cancellationToken);
        var numeros = await _dbContext.AtendimentosSociais.AsNoTracking()
            .Where(a => a.ProntuarioSocialId == prontuario.Id)
            .ToDictionaryAsync(a => a.Id, a => a.Numero, cancellationToken);

        return Ok(new AnexosCompartilhadosResponse
        {
            NumeroProntuario = acesso.PodeVerConteudo ? prontuario.Numero : null,
            Anexos = anexos
                .Where(a => ServicoSocialAcessoService.PodeVerAnexo(a, acesso, tecnico))
                .Select(a => ServicoSocialController.MapearAnexo(a, acesso.PodeVerConteudo ? numeros : new Dictionary<int, int>(), acesso.PodeVerConteudo))
                .ToList()
        });
    }

    /// <summary>Baixa (ou mostra, com inline=true) o arquivo. Sempre autenticado e auditado.</summary>
    [HttpGet("{anexoId:int}/arquivo")]
    public async Task<IActionResult> Baixar(int anexoId, [FromQuery] bool inline, CancellationToken cancellationToken)
    {
        var usuario = await _assistidas.ObterUsuarioAsync(User, cancellationToken);
        if (usuario is null) return Unauthorized();

        var anexo = await _dbContext.AnexosSociais.AsNoTracking()
            .Include(a => a.Prontuario).ThenInclude(p => p!.Responsaveis)
            .SingleOrDefaultAsync(a => a.Id == anexoId && !a.Excluido, cancellationToken);
        if (anexo?.Prontuario is null) return NotFound(new { mensagem = "Documento não encontrado." });

        var acesso = await _acesso.AvaliarAsync(anexo.Prontuario, usuario, cancellationToken);
        var tecnico = !acesso.PodeVerConteudo && await _acesso.ResponsavelTecnicoAsync(anexo.Prontuario.AssistidaId, usuario, cancellationToken);
        if (!ServicoSocialAcessoService.PodeVerAnexo(anexo, acesso, tecnico))
        {
            await _auditoria.RegistrarAsync(
                "SOCIAL_ANEXO_ACESSO_NEGADO",
                "AnexoSocial",
                anexo.Id.ToString(CultureInfo.InvariantCulture),
                $"{usuario.Identificador} tentou baixar um documento do prontuário social {anexo.Prontuario.Numero} sem permissão.");
            return Proibido("Você não tem acesso a este documento.");
        }

        var conteudo = await _dbContext.AnexosSociaisConteudos.AsNoTracking()
            .SingleOrDefaultAsync(c => c.AnexoSocialId == anexo.Id, cancellationToken);
        if (conteudo is null) return NotFound(new { mensagem = "Arquivo não encontrado." });

        byte[] dados;
        try
        {
            dados = _arquivos.Descriptografar(conteudo.Dados);
        }
        catch (Exception ex) when (ex is CryptographicException or InvalidOperationException)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { mensagem = "Não foi possível abrir o documento (chave de criptografia diferente da usada no envio)." });
        }

        if (AnexosSociaisArquivos.Hash(dados) != anexo.Sha256)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { mensagem = "O documento guardado não confere com o original." });
        }

        await _auditoria.RegistrarAsync(
            inline ? "SOCIAL_ANEXO_VISUALIZADO" : "SOCIAL_ANEXO_BAIXADO",
            "AnexoSocial",
            anexo.Id.ToString(CultureInfo.InvariantCulture),
            $"{usuario.Identificador} {(inline ? "visualizou" : "baixou")} o documento #{anexo.Id} ({anexo.Tipo}) do prontuário social {anexo.Prontuario.Numero}"
                + (tecnico ? " como equipe técnica." : "."));

        Response.Headers.CacheControl = "no-store, max-age=0";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        if (inline)
        {
            var disposicao = new Microsoft.Net.Http.Headers.ContentDispositionHeaderValue("inline");
            disposicao.SetHttpFileName(anexo.NomeArquivo);
            Response.Headers.ContentDisposition = disposicao.ToString();
            return File(dados, anexo.ContentType);
        }

        return File(dados, anexo.ContentType, anexo.NomeArquivo);
    }

    /// <summary>Exclusão lógica: o anexo some das listas, mas o registro e o motivo ficam.</summary>
    [HttpPost("{anexoId:int}/excluir")]
    public async Task<IActionResult> Excluir(int anexoId, ExcluirAnexoRequest request, CancellationToken cancellationToken)
    {
        var usuario = await _assistidas.ObterUsuarioAsync(User, cancellationToken);
        if (usuario is null) return Unauthorized();

        var motivo = request.Motivo?.Trim() ?? string.Empty;
        if (motivo.Length < 5 || motivo.Length > 500) return BadRequest(new { mensagem = "Explique o motivo da exclusão (entre 5 e 500 caracteres)." });

        var anexo = await _dbContext.AnexosSociais
            .Include(a => a.Prontuario).ThenInclude(p => p!.Responsaveis)
            .SingleOrDefaultAsync(a => a.Id == anexoId && !a.Excluido, cancellationToken);
        if (anexo?.Prontuario is null) return NotFound(new { mensagem = "Documento não encontrado." });

        var acesso = await _acesso.AvaliarAsync(anexo.Prontuario, usuario, cancellationToken);
        if (!acesso.PodeVerConteudo) return Proibido("Somente quem cuida do prontuário pode excluir documentos.");

        anexo.Excluido = true;
        anexo.ExcluidoEm = DateTime.UtcNow;
        anexo.ExcluidoPorIdentificador = usuario.Identificador;
        anexo.MotivoExclusao = motivo;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditoria.RegistrarAsync(
            "SOCIAL_ANEXO_EXCLUIDO",
            "AnexoSocial",
            anexo.Id.ToString(CultureInfo.InvariantCulture),
            $"{usuario.Identificador} excluiu o documento #{anexo.Id} ({anexo.Tipo}) do prontuário social {anexo.Prontuario.Numero}. Motivo: {(motivo.Length > 300 ? motivo[..300] : motivo)}");

        var snapshot = await _snapshot.PersistAsync("social_anexo", CancellationToken.None);
        return Ok(new { mensagem = "Documento excluído.", avisoSnapshot = snapshot.AvisoSnapshot });
    }

    private async Task<(ProntuarioSocial? Prontuario, AcessoProntuarioSocial Acesso)> CarregarAsync(int assistidaId, UsuarioAtuante usuario, CancellationToken cancellationToken)
    {
        var prontuario = await _dbContext.ProntuariosSociais.AsNoTracking()
            .Include(p => p.Responsaveis)
            .SingleOrDefaultAsync(p => p.AssistidaId == assistidaId && p.Assistida!.Ativa, cancellationToken);
        return (prontuario, await _acesso.AvaliarAsync(prontuario, usuario, cancellationToken));
    }

    private ObjectResult Proibido(string mensagem) => StatusCode(StatusCodes.Status403Forbidden, new { mensagem });
}
