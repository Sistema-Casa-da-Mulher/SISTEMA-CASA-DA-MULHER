using CasaMulher.Api.Models;
using CasaMulher.Api.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CasaMulher.Api.Data;

/// <summary>
/// Conta de teste compartilhada do Serviço Social (somente desenvolvimento e homologação).
/// No seed de homologação o ID SOC-000001 já existe como funcionária fictícia inativa e sem senha;
/// aqui ela vira a conta de teste, sem 2FA obrigatório (como as contas de teste do Jurídico e da
/// Psicologia). Só o hash da senha fica no código; a senha vai no ACESSO-TESTE.txt do grupo.
/// Se a conta já tiver senha própria, nada é alterado.
/// </summary>
public static class ContaTesteServicoSocialSeeder
{
    public const string Identificador = "SOC-000001";
    public const string Email = "social.teste@casamulher.local";

    private const string Nome = "Serviço Social Teste";

    public const string CressFicticio = "CRESS 9ª Região nº 00000 (fictício)";

    // PBKDF2 (formato do ASP.NET Identity v3) da senha de teste compartilhada.
    private const string SenhaHash = "AQAAAAIAAYagAAAAEM5OyxRnBJUxzmFtsjXLzDATR/17TVZjgUoeIirxXeK7wPWyjMxjN0iAoF2nNr+0Xg==";

    public static async Task GarantirAsync(IServiceProvider services, ILogger logger)
    {
        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        if (!await roleManager.RoleExistsAsync(PerfisAcesso.AssistenteSocial))
        {
            await roleManager.CreateAsync(new IdentityRole(PerfisAcesso.AssistenteSocial));
        }

        var usuario = await dbContext.Users.SingleOrDefaultAsync(u => u.IdentificadorFuncionario == Identificador);

        if (usuario is not null && !string.IsNullOrEmpty(usuario.PasswordHash))
        {
            // Conta já preparada antes do campo CRESS existir: só completa o registro fictício.
            if (usuario.Email == Email && string.IsNullOrWhiteSpace(usuario.Cress))
            {
                usuario.Cress = CressFicticio;
                await userManager.UpdateAsync(usuario);
            }

            return;
        }

        if (usuario is null && await dbContext.FuncionariosConvites.AnyAsync(c => c.IdentificadorFuncionario == Identificador))
        {
            logger.LogWarning("Conta de teste {Id} não criada: o ID já está reservado por um convite.", Identificador);
            return;
        }

        var emailEmUso = await userManager.FindByEmailAsync(Email);
        if (emailEmUso is not null && emailEmUso.Id != usuario?.Id)
        {
            logger.LogWarning("Conta de teste {Id} não criada: o e-mail {Email} já está em uso.", Identificador, Email);
            return;
        }

        var nova = usuario is null;
        usuario ??= new ApplicationUser { UserName = Identificador, IdentificadorFuncionario = Identificador, CriadoEm = DateTime.UtcNow };
        usuario.NomeCompleto = Nome;
        usuario.Email = Email;
        usuario.EmailConfirmed = true;
        usuario.Perfil = PerfisAcesso.AssistenteSocial;
        usuario.Cress = CressFicticio;
        usuario.Ativo = true;
        usuario.DoisFatoresObrigatorio = false;
        usuario.SecuritySetupRequired = false;
        usuario.DeveTrocarSenha = false;
        usuario.PasswordHash = SenhaHash;
        usuario.SecurityStamp = Guid.NewGuid().ToString("N");

        var resultado = nova ? await userManager.CreateAsync(usuario) : await userManager.UpdateAsync(usuario);
        if (!resultado.Succeeded)
        {
            logger.LogWarning("Conta de teste {Id} não pôde ser preparada: {Erros}", Identificador, string.Join("; ", resultado.Errors.Select(e => e.Description)));
            return;
        }

        if (!await userManager.IsInRoleAsync(usuario, PerfisAcesso.AssistenteSocial))
        {
            await userManager.AddToRoleAsync(usuario, PerfisAcesso.AssistenteSocial);
        }

        logger.LogInformation("Conta de teste {Id} do Serviço Social pronta.", Identificador);
    }
}
