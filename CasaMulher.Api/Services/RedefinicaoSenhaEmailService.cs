using System.Net;
using CasaMulher.Api.Models;
using Microsoft.AspNetCore.Identity;

namespace CasaMulher.Api.Services;

public class RedefinicaoSenhaEmailService : IRedefinicaoSenhaEmailService
{
    private const string TipoEmail = "RedefinicaoSenha";
    private const string Assunto = "Redefinição de senha - Sistema Casa da Mulher";

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEmailService _emailService;
    private readonly IFrontendUrlService _frontendUrlService;

    public RedefinicaoSenhaEmailService(
        UserManager<ApplicationUser> userManager,
        IEmailService emailService,
        IFrontendUrlService frontendUrlService)
    {
        _userManager = userManager;
        _emailService = emailService;
        _frontendUrlService = frontendUrlService;
    }

    public async Task<ResultadoRedefinicaoSenhaEmail> EnviarAsync(ApplicationUser funcionario)
    {
        if (string.IsNullOrWhiteSpace(funcionario.Email))
        {
            return ResultadoRedefinicaoSenhaEmail.SemEmail();
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(funcionario);
        var linkRelativo = GerarLinkRedefinicaoRelativo(funcionario.Email, token);
        var linkAbsoluto = GerarLinkAbsoluto(linkRelativo);

        if (string.IsNullOrWhiteSpace(linkAbsoluto))
        {
            return ResultadoRedefinicaoSenhaEmail.SemBaseUrl();
        }

        var corpoHtml = MontarCorpoEmail(funcionario.NomeCompleto, linkAbsoluto);

        try
        {
            var resultadoEnvio = await _emailService.EnviarAsync(
                funcionario.Email,
                Assunto,
                corpoHtml,
                TipoEmail);

            if (!resultadoEnvio.Enviado)
            {
                return new ResultadoRedefinicaoSenhaEmail(
                    false,
                    resultadoEnvio.Status,
                    "O provedor de e-mail está em modo simulado. Nenhum e-mail real foi enviado; configure SMTP.");
            }

            return new ResultadoRedefinicaoSenhaEmail(true, resultadoEnvio.Status, null);
        }
        catch
        {
            return new ResultadoRedefinicaoSenhaEmail(
                false,
                "Falhou",
                "Não foi possível enviar o link de redefinição de senha. Confira a configuração de e-mail.");
        }
    }

    private static string GerarLinkRedefinicaoRelativo(string email, string token)
    {
        return $"redefinir-senha.html?email={Uri.EscapeDataString(email)}&token={Uri.EscapeDataString(token)}";
    }

    private string? GerarLinkAbsoluto(string linkRelativo)
    {
        return _frontendUrlService.CriarLink(linkRelativo);
    }

    private static string MontarCorpoEmail(string nomeCompleto, string linkRedefinicao)
    {
        var nome = WebUtility.HtmlEncode(nomeCompleto);
        var link = WebUtility.HtmlEncode(linkRedefinicao);

        return $"""
            <div style="text-align: center; margin-bottom: 24px;">
                <img src="https://files.catbox.moe/ovf0uf.png" alt="Casa da Mulher de Itaquaquecetuba" style="height: 80px; width: auto;" />
            </div>
            <p>Olá, {nome}.</p>
            <p>Foi solicitada uma redefinição de senha para seu acesso ao Sistema Casa da Mulher.</p>
            <p>Para criar uma nova senha, clique no botão abaixo:</p>
            <p>
                <a href="{link}" style="display:inline-block;padding:12px 18px;background:#18726b;color:#ffffff;text-decoration:none;border-radius:6px;font-weight:700;">
                    Redefinir minha senha
                </a>
            </p>
            <p>Se o botão não abrir, copie e cole este link no navegador:</p>
            <p><a href="{link}">{link}</a></p>
            <p>Se você não solicitou essa alteração, ignore esta mensagem ou entre em contato com a coordenação.</p>
            <p>Atenciosamente,<br>Casa da Mulher de Itaquaquecetuba</p>
            """;
    }
}
