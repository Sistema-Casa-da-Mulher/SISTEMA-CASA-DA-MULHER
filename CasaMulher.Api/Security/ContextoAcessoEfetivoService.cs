using System.Security.Claims;
using CasaMulher.Api.Data;
using CasaMulher.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace CasaMulher.Api.Security;

public sealed record ContextoAcessoEfetivo(
    ApplicationUser Usuario,
    string Perfil,
    string IdentificadorFuncionario);

public interface IContextoAcessoEfetivoService
{
    Task<ContextoAcessoEfetivo?> ObterAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default);

    Task<bool> PodeGerenciarAreaInstitucionalAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default);

    Task<bool> EhMasterAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default);

    Task<bool> EhSuperAdminInstitucionalAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Valida o contexto efetivo emitido pelo backend no JWT contra o estado atual do banco.
/// Uma conta sincronizada pode ter aliases EQP e ADM, mas recebe apenas as permissões do
/// identificador usado naquela sessão.
/// </summary>
public sealed class ContextoAcessoEfetivoService : IContextoAcessoEfetivoService
{
    private readonly AppDbContext _dbContext;
    private readonly IMasterUserService _masterUserService;

    public ContextoAcessoEfetivoService(
        AppDbContext dbContext,
        IMasterUserService masterUserService)
    {
        _dbContext = dbContext;
        _masterUserService = masterUserService;
    }

    public async Task<ContextoAcessoEfetivo?> ObterAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        var usuarioId = principal.FindFirstValue(ClaimTypes.NameIdentifier)?.Trim();
        var perfilInformado = NormalizarPerfil(principal.FindFirstValue("perfil"));
        var identificadorInformado = NormalizarIdentificador(
            principal.FindFirstValue("identificadorFuncionario"));

        if (string.IsNullOrWhiteSpace(usuarioId)
            || string.IsNullOrWhiteSpace(perfilInformado)
            || string.IsNullOrWhiteSpace(identificadorInformado))
        {
            return null;
        }

        var usuario = await _dbContext.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == usuarioId, cancellationToken);

        if (usuario is null || !usuario.Ativo)
        {
            return null;
        }

        var alias = await _dbContext.UserLoginIdentifiers
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.Identificador.ToUpper() == identificadorInformado,
                cancellationToken);

        string perfilEsperado;
        if (alias is not null)
        {
            if (!alias.Ativo || !string.Equals(alias.UserId, usuarioId, StringComparison.Ordinal))
            {
                return null;
            }

            perfilEsperado = ObterPerfilDoContexto(
                alias.Tipo,
                alias.Identificador,
                usuario.Perfil);

            if (!TipoEIdentificadorSaoCoerentes(alias.Tipo, alias.Identificador))
            {
                return null;
            }
        }
        else
        {
            var identificadorCanonico = NormalizarIdentificador(usuario.IdentificadorFuncionario);
            var userNameCanonico = NormalizarIdentificador(usuario.UserName);

            if (!string.Equals(identificadorInformado, identificadorCanonico, StringComparison.Ordinal)
                && !string.Equals(identificadorInformado, userNameCanonico, StringComparison.Ordinal))
            {
                return null;
            }

            perfilEsperado = ObterPerfilDoContexto(
                string.Empty,
                identificadorInformado,
                usuario.Perfil);
        }

        perfilEsperado = NormalizarPerfil(perfilEsperado);
        if (!string.Equals(perfilInformado, perfilEsperado, StringComparison.Ordinal)
            || !principal.IsInRole(perfilEsperado))
        {
            return null;
        }

        return new ContextoAcessoEfetivo(usuario, perfilEsperado, identificadorInformado);
    }

    public async Task<bool> PodeGerenciarAreaInstitucionalAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        var contexto = await ObterAsync(principal, cancellationToken);
        if (contexto is null)
        {
            return false;
        }

        if (string.Equals(contexto.Perfil, PerfisAcesso.Adm, StringComparison.Ordinal))
        {
            return true;
        }

        return string.Equals(contexto.Perfil, PerfisAcesso.Equipe, StringComparison.Ordinal)
            && await EhEquipeOwnerPrincipalAsync(contexto.Usuario.Id, cancellationToken);
    }

    public async Task<bool> EhMasterAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        var contexto = await ObterAsync(principal, cancellationToken);
        if (contexto is null)
        {
            return false;
        }

        if (EhSuperAdminInstitucional(contexto))
        {
            return true;
        }

        return await EhEquipeOwnerPrincipalAsync(contexto.Usuario.Id, cancellationToken);
    }

    public async Task<bool> EhSuperAdminInstitucionalAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        var contexto = await ObterAsync(principal, cancellationToken);
        return contexto is not null && EhSuperAdminInstitucional(contexto);
    }

    public static string ObterPerfilDoContexto(
        string? tipo,
        string? identificador,
        string? perfilPadrao)
    {
        if (string.Equals(tipo, "EQP", StringComparison.OrdinalIgnoreCase)
            || identificador?.StartsWith("EQP-", StringComparison.OrdinalIgnoreCase) == true)
        {
            return PerfisAcesso.Equipe;
        }

        if (string.Equals(tipo, "ADM", StringComparison.OrdinalIgnoreCase)
            || identificador?.StartsWith("ADM-", StringComparison.OrdinalIgnoreCase) == true)
        {
            return PerfisAcesso.Adm;
        }

        return NormalizarPerfil(perfilPadrao);
    }

    private bool EhSuperAdminInstitucional(ContextoAcessoEfetivo contexto)
    {
        return string.Equals(contexto.Perfil, PerfisAcesso.Adm, StringComparison.Ordinal)
            && string.Equals(
                contexto.IdentificadorFuncionario,
                NormalizarIdentificador(_masterUserService.SuperAdminIdentificador),
                StringComparison.Ordinal);
    }

    private Task<bool> EhEquipeOwnerPrincipalAsync(
        string usuarioId,
        CancellationToken cancellationToken)
    {
        return _dbContext.EquipeMembros.AnyAsync(
            membro => membro.UserId == usuarioId
                && membro.Ativo
                && membro.PapelEquipe == EquipePapeis.Owner
                && membro.CodigoEquipe == _masterUserService.EquipeOwnerCodigo,
            cancellationToken);
    }

    private static bool TipoEIdentificadorSaoCoerentes(string? tipo, string? identificador)
    {
        if (string.Equals(tipo, "EQP", StringComparison.OrdinalIgnoreCase))
        {
            return identificador?.StartsWith("EQP-", StringComparison.OrdinalIgnoreCase) == true;
        }

        if (string.Equals(tipo, "ADM", StringComparison.OrdinalIgnoreCase))
        {
            return identificador?.StartsWith("ADM-", StringComparison.OrdinalIgnoreCase) == true;
        }

        return true;
    }

    private static string NormalizarPerfil(string? perfil) =>
        perfil?.Trim().ToLowerInvariant() ?? string.Empty;

    private static string NormalizarIdentificador(string? identificador) =>
        identificador?.Trim().ToUpperInvariant() ?? string.Empty;
}

public sealed class ContextoAcessoEfetivoRequirement : IAuthorizationRequirement;

public sealed class ContextoAcessoEfetivoAuthorizationHandler
    : AuthorizationHandler<ContextoAcessoEfetivoRequirement>
{
    private readonly IContextoAcessoEfetivoService _contextoAcesso;

    public ContextoAcessoEfetivoAuthorizationHandler(
        IContextoAcessoEfetivoService contextoAcesso)
    {
        _contextoAcesso = contextoAcesso;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ContextoAcessoEfetivoRequirement requirement)
    {
        if (await _contextoAcesso.ObterAsync(context.User) is not null)
        {
            context.Succeed(requirement);
        }
    }
}
