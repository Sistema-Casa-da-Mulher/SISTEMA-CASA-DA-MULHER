using System.Security.Claims;
using CasaMulher.Api.Services;

namespace CasaMulher.Api.Middleware;

/// <summary>
/// Bloqueia no servidor qualquer chamada da API feita por funcionária que ainda não
/// aceitou a versão atual do Termo de Confidencialidade, mesmo que a tela seja burlada.
/// </summary>
public sealed class TermoSigiloMiddleware
{
    public const string CodigoErro = "TERMO_SIGILO_PENDENTE";

    // Rotas necessárias para entrar, trocar a senha obrigatória, ler/aceitar o termo e sair.
    private static readonly string[] RotasLiberadas =
    [
        "/api/auth",
        "/api/termo-sigilo"
    ];

    private readonly RequestDelegate _next;

    public TermoSigiloMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, TermoSigiloService termoSigiloService)
    {
        var path = context.Request.Path;

        if (context.User.Identity?.IsAuthenticated != true
            || !path.StartsWithSegments("/api")
            || RotasLiberadas.Any(rota => path.StartsWithSegments(rota, StringComparison.OrdinalIgnoreCase)))
        {
            await _next(context);
            return;
        }

        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var perfil = context.User.FindFirstValue("perfil");

        if (!string.IsNullOrWhiteSpace(userId)
            && await termoSigiloService.AceitePendenteAsync(userId, perfil, context.RequestAborted))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new
            {
                erro = CodigoErro,
                mensagem = "Aceite o Termo de Confidencialidade e Proteção de Dados para continuar usando o sistema."
            });
            return;
        }

        await _next(context);
    }
}
