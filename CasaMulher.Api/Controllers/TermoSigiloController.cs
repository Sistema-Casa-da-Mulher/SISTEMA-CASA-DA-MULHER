using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Text;
using CasaMulher.Api.Data;
using CasaMulher.Api.DTOs;
using CasaMulher.Api.Models;
using CasaMulher.Api.Security;
using CasaMulher.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace CasaMulher.Api.Controllers;

[ApiController]
[Route("api/termo-sigilo")]
[Authorize]
public class TermoSigiloController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly TermoSigiloService _termoSigiloService;
    private readonly IAuditoriaService _auditoriaService;
    private readonly SecuritySnapshotPersistenceService _securitySnapshot;
    private readonly IEmailService _emailService;
    private readonly ILogger<TermoSigiloController> _logger;

    public TermoSigiloController(
        AppDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        TermoSigiloService termoSigiloService,
        IAuditoriaService auditoriaService,
        SecuritySnapshotPersistenceService securitySnapshot,
        IEmailService emailService,
        ILogger<TermoSigiloController> logger)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _termoSigiloService = termoSigiloService;
        _auditoriaService = auditoriaService;
        _securitySnapshot = securitySnapshot;
        _emailService = emailService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<TermoSigiloResponse>> Obter()
    {
        var usuario = await ObterUsuarioAtualAsync();

        if (usuario is null)
        {
            return Unauthorized();
        }

        var perfil = User.FindFirstValue("perfil") ?? usuario.Perfil;
        var aceitoEm = await _termoSigiloService.ObterDataAceiteAsync(usuario.Id);

        return Ok(MontarResposta(usuario, perfil, aceitoEm));
    }

    [HttpPost("aceitar")]
    public async Task<ActionResult<TermoSigiloResponse>> Aceitar(AceitarTermoSigiloRequest request)
    {
        var usuario = await ObterUsuarioAtualAsync();

        if (usuario is null)
        {
            return Unauthorized();
        }

        var perfil = User.FindFirstValue("perfil") ?? usuario.Perfil;
        var identificador = User.FindFirstValue("identificadorFuncionario") ?? usuario.IdentificadorFuncionario;

        if (!TermoSigilo.PerfilExigeAceite(perfil))
        {
            return BadRequest(new { mensagem = "Este perfil não precisa aceitar o termo." });
        }

        var aceiteExistente = await _termoSigiloService.ObterDataAceiteAsync(usuario.Id);

        if (aceiteExistente is not null)
        {
            return Ok(MontarResposta(usuario, perfil, aceiteExistente));
        }

        if (!string.Equals(request.Versao, TermoSigilo.Versao, StringComparison.Ordinal))
        {
            return Conflict(new { mensagem = "O termo foi atualizado. Recarregue a página e leia a nova versão antes de aceitar." });
        }

        if (request.Confirmacoes is null
            || request.Confirmacoes.Count != TermoSigilo.Confirmacoes.Count
            || request.Confirmacoes.Any(confirmado => !confirmado))
        {
            return BadRequest(new { mensagem = "Marque todas as declarações para aceitar o termo." });
        }

        if (!NomesConferem(request.NomeAssinado, usuario.NomeCompleto))
        {
            return BadRequest(new { mensagem = "Digite seu nome completo exatamente como está no cadastro para assinar." });
        }

        var aceite = new TermoSigiloAceite
        {
            UserId = usuario.Id,
            IdentificadorFuncionario = identificador,
            Perfil = perfil,
            VersaoTermo = TermoSigilo.Versao,
            HashTermo = TermoSigilo.Hash,
            // O nome digitado já foi conferido; registra a grafia oficial do cadastro.
            NomeAssinado = usuario.NomeCompleto,
            AceitoEm = DateTime.UtcNow,
            EnderecoIp = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = Limitar(HttpContext.Request.Headers.UserAgent.ToString(), 512)
        };

        _dbContext.TermoSigiloAceites.Add(aceite);
        await _dbContext.SaveChangesAsync();
        _termoSigiloService.MarcarAceite(usuario.Id, aceite.AceitoEm);

        await _auditoriaService.RegistrarAsync(
            "TERMO_SIGILO_ACEITO",
            "ApplicationUser",
            usuario.Id,
            $"{identificador} aceitou o Termo de Confidencialidade e Proteção de Dados versão {TermoSigilo.Versao} (hash {TermoSigilo.Hash[..16]}).",
            identificador);

        // O aceite é uma prova jurídica: precisa sobreviver a um restart do Render.
        var snapshot = await _securitySnapshot.PersistAsync("confidentiality_term_accepted", CancellationToken.None);

        await EnviarCopiaPorEmailAsync(usuario, aceite);

        var resposta = MontarResposta(usuario, perfil, aceite.AceitoEm);
        resposta.AvisoSnapshot = snapshot.AvisoSnapshot;
        return Ok(resposta);
    }

    private TermoSigiloResponse MontarResposta(ApplicationUser usuario, string perfil, DateTime? aceitoEm)
    {
        return new TermoSigiloResponse
        {
            Versao = TermoSigilo.Versao,
            Titulo = TermoSigilo.Titulo,
            Hash = TermoSigilo.Hash,
            Resumo = TermoSigilo.Resumo,
            Secoes = TermoSigilo.Secoes
                .Select(secao => new TermoSigiloSecaoResponse { Titulo = secao.Titulo, Paragrafos = secao.Paragrafos })
                .ToList(),
            Confirmacoes = TermoSigilo.Confirmacoes,
            Obrigatorio = TermoSigilo.PerfilExigeAceite(perfil),
            Aceito = aceitoEm is not null,
            AceitoEm = aceitoEm,
            NomeCompleto = usuario.NomeCompleto,
            IdentificadorFuncionario = User.FindFirstValue("identificadorFuncionario") ?? usuario.IdentificadorFuncionario
        };
    }

    private async Task EnviarCopiaPorEmailAsync(ApplicationUser usuario, TermoSigiloAceite aceite)
    {
        if (string.IsNullOrWhiteSpace(usuario.Email))
        {
            return;
        }

        var html = new StringBuilder();
        html.Append($"<p>Olá, {WebUtility.HtmlEncode(usuario.NomeCompleto)}.</p>");
        html.Append("<p>Este e-mail é a sua cópia do termo aceito no Sistema Casa da Mulher. Guarde-o.</p>");
        html.Append("<ul>");
        html.Append($"<li>ID de acesso: {WebUtility.HtmlEncode(aceite.IdentificadorFuncionario)}</li>");
        html.Append($"<li>Assinado como: {WebUtility.HtmlEncode(aceite.NomeAssinado)}</li>");
        html.Append($"<li>Data e hora (UTC): {aceite.AceitoEm.ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture)}</li>");
        html.Append($"<li>Versão do termo: {WebUtility.HtmlEncode(aceite.VersaoTermo)}</li>");
        html.Append($"<li>Código de verificação: {WebUtility.HtmlEncode(aceite.HashTermo)}</li>");
        html.Append("</ul>");
        html.Append($"<h2>{WebUtility.HtmlEncode(TermoSigilo.Titulo)}</h2>");

        foreach (var secao in TermoSigilo.Secoes)
        {
            html.Append($"<h3>{WebUtility.HtmlEncode(secao.Titulo)}</h3>");
            foreach (var paragrafo in secao.Paragrafos)
            {
                html.Append($"<p>{WebUtility.HtmlEncode(paragrafo)}</p>");
            }
        }

        html.Append("<h3>Declarações confirmadas</h3><ul>");
        foreach (var confirmacao in TermoSigilo.Confirmacoes)
        {
            html.Append($"<li>{WebUtility.HtmlEncode(confirmacao)}</li>");
        }
        html.Append("</ul>");

        try
        {
            await _emailService.EnviarAsync(
                usuario.Email,
                "Cópia do Termo de Confidencialidade - Sistema Casa da Mulher",
                html.ToString(),
                "TermoSigiloAceito");
        }
        catch (Exception ex)
        {
            // O aceite já está registrado; a cópia por e-mail é apenas uma conveniência.
            _logger.LogWarning(ex, "Não foi possível enviar a cópia do termo de sigilo para {UserId}.", usuario.Id);
        }
    }

    private async Task<ApplicationUser?> ObterUsuarioAtualAsync()
    {
        var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return string.IsNullOrWhiteSpace(usuarioId) ? null : await _userManager.FindByIdAsync(usuarioId);
    }

    private static bool NomesConferem(string? digitado, string cadastrado)
    {
        return !string.IsNullOrWhiteSpace(digitado)
            && string.Equals(NormalizarNome(digitado), NormalizarNome(cadastrado), StringComparison.Ordinal);
    }

    // Ignora maiúsculas, acentos e espaços extras para não barrar quem digita "joao" em vez de "João".
    private static string NormalizarNome(string nome)
    {
        var decomposto = nome.Trim().Normalize(NormalizationForm.FormD);
        var semAcento = new StringBuilder(decomposto.Length);

        foreach (var caractere in decomposto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(caractere) != UnicodeCategory.NonSpacingMark)
            {
                semAcento.Append(caractere);
            }
        }

        var partes = semAcento.ToString()
            .Normalize(NormalizationForm.FormC)
            .ToUpperInvariant()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);

        return string.Join(' ', partes);
    }

    private static string? Limitar(string? valor, int maximo)
    {
        if (string.IsNullOrEmpty(valor))
        {
            return null;
        }

        return valor.Length <= maximo ? valor : valor[..maximo];
    }
}
