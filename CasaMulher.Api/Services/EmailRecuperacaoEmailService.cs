using System.Net;
using CasaMulher.Api.Models;
using Microsoft.AspNetCore.Identity;

namespace CasaMulher.Api.Services;

public class EmailRecuperacaoEmailService : IEmailRecuperacaoEmailService
{
    private const string TipoEmail = "ConfirmacaoEmailRecuperacao";
    private const string Assunto = "Confirmação de e-mail de recuperação - Sistema Casa da Mulher";

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEmailService _emailService;
    private readonly IFrontendUrlService _frontendUrlService;
    private readonly IWebHostEnvironment _environment;

    public EmailRecuperacaoEmailService(
        UserManager<ApplicationUser> userManager,
        IEmailService emailService,
        IFrontendUrlService frontendUrlService,
        IWebHostEnvironment environment)
    {
        _userManager = userManager;
        _emailService = emailService;
        _frontendUrlService = frontendUrlService;
        _environment = environment;
    }

    public async Task<ResultadoEmailRecuperacao> EnviarConfirmacaoAsync(ApplicationUser funcionario)
    {
        if (string.IsNullOrWhiteSpace(funcionario.EmailRecuperacao))
        {
            return ResultadoEmailRecuperacao.SemEmail();
        }

        var emailRecuperacao = funcionario.EmailRecuperacao.Trim();
        var token = await _userManager.GenerateUserTokenAsync(
            funcionario,
            TokenOptions.DefaultProvider,
            EmailRecuperacaoTokenPurpose.Criar(emailRecuperacao));

        var linkRelativo = GerarLinkConfirmacaoRelativo(emailRecuperacao, token);
        var linkAbsoluto = GerarLinkAbsoluto(linkRelativo);

        if (string.IsNullOrWhiteSpace(linkAbsoluto))
        {
            return ResultadoEmailRecuperacao.SemBaseUrl();
        }

        var corpoHtml = MontarCorpoEmail(funcionario.NomeCompleto, linkAbsoluto);

        try
        {
            var resultadoEnvio = await _emailService.EnviarAsync(
                emailRecuperacao,
                Assunto,
                corpoHtml,
                TipoEmail);

            var aviso = resultadoEnvio.Enviado
                ? null
                : "O provedor de e-mail está em modo simulado. Nenhum e-mail real foi enviado; configure SMTP.";

            return new ResultadoEmailRecuperacao(
                resultadoEnvio.Enviado,
                resultadoEnvio.Status,
                aviso,
                _environment.IsDevelopment() ? linkAbsoluto : null);
        }
        catch
        {
            return new ResultadoEmailRecuperacao(
                false,
                "Falhou",
                "Não foi possível enviar o link de confirmação. Confira a configuração de e-mail.",
                _environment.IsDevelopment() ? linkAbsoluto : null);
        }
    }

    private static string GerarLinkConfirmacaoRelativo(string emailRecuperacao, string token)
    {
        return $"confirmar-email-recuperacao.html?email={Uri.EscapeDataString(emailRecuperacao)}&token={Uri.EscapeDataString(token)}";
    }

    private string? GerarLinkAbsoluto(string linkRelativo)
    {
        return _frontendUrlService.CriarLink(linkRelativo);
    }

    private static string MontarCorpoEmail(string nomeCompleto, string linkConfirmacao)
    {
        var nome = WebUtility.HtmlEncode(nomeCompleto);
        var link = WebUtility.HtmlEncode(linkConfirmacao);

        return $"""
            <div style="text-align: center; margin-bottom: 24px;">
                <img src="https://files.catbox.moe/ovf0uf.png" alt="Casa da Mulher de Itaquaquecetuba" style="height: 80px; width: auto;" />
            </div>
            <p>Olá, {nome}.</p>
            <p>Foi solicitado o cadastro deste e-mail como e-mail de recuperação no Sistema Casa da Mulher.</p>
            <p>Depois de confirmado, ele poderá ser usado em fluxos futuros de recuperação de acesso.</p>
            <p>Para confirmar, clique no botão abaixo:</p>
            <p>
                <a href="{link}" style="display:inline-block;padding:12px 18px;background:#18726b;color:#ffffff;text-decoration:none;border-radius:6px;font-weight:700;">
                    Confirmar e-mail de recuperação
                </a>
            </p>
            <p>Se o botão não abrir, copie e cole este link no navegador:</p>
            <p><a href="{link}">{link}</a></p>
            <p>Se você não solicitou esse cadastro, ignore esta mensagem ou entre em contato com a coordenação.</p>
            <p>Atenciosamente,<br>Casa da Mulher de Itaquaquecetuba</p>
            """;
    }
}
