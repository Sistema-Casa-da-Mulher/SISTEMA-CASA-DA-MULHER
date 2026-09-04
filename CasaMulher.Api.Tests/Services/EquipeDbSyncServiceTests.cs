using CasaMulher.Api.Data;
using CasaMulher.Api.Models;
using CasaMulher.Api.Security;
using CasaMulher.Api.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CasaMulher.Api.Tests.Services;

public sealed class EquipeDbSyncServiceTests
{
    [Fact]
    public async Task RestaurarPermissoesReparaAliasesRolesEMembroSemAlterarSeguranca()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var remoteMember = new EquipeDbMembro
        {
            EqpId = "EQP-000003",
            AdmId = "ADM-000005",
            Nome = "Membro Teste",
            GitHubUsername = "membro-teste",
            GitHubId = "123",
            PapelEquipe = EquipePapeis.Contributor,
            FluxoTrabalho = EquipeFluxosTrabalho.ForkCodespaces,
            Status = "ativo",
            PasswordHash = "hash-remoto-que-nao-deve-ser-usado",
            PasswordVersion = 99,
            SenhaAtualizadaEm = DateTime.UtcNow.AddDays(1),
            AtivadoEm = DateTime.UtcNow.AddMonths(-1),
            AtualizadoEm = DateTime.UtcNow
        };

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AppDbContext>(options => options.UseSqlite(connection));
        services.AddIdentityCore<ApplicationUser>()
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<AppDbContext>();
        services.AddSingleton<IEquipeDbGitHubService>(
            new FakeEquipeDbGitHubService(remoteMember));
        services.AddScoped<EquipeDbSyncService>();

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var service = scope.ServiceProvider.GetRequiredService<EquipeDbSyncService>();
        await dbContext.Database.EnsureCreatedAsync();

        foreach (var role in new[] { PerfisAcesso.Equipe, PerfisAcesso.Adm, PerfisAcesso.Recepcao })
        {
            Assert.True((await roleManager.CreateAsync(new IdentityRole(role))).Succeeded);
        }

        var lockoutEnd = DateTimeOffset.UtcNow.AddMinutes(5);
        var usuario = new ApplicationUser
        {
            Id = "usuario-pareado",
            UserName = "EQP-000003",
            NormalizedUserName = "EQP-000003",
            NomeCompleto = "Membro Teste",
            Perfil = PerfisAcesso.Recepcao,
            IdentificadorFuncionario = "EQP-000003",
            Ativo = false,
            PasswordHash = "hash-local-preservado",
            SecurityStamp = "security-stamp-local",
            TwoFactorEnabled = true,
            LockoutEnabled = true,
            LockoutEnd = lockoutEnd,
            AccessFailedCount = 4,
            EmailRecuperacao = "recuperacao@example.com",
            EmailRecuperacaoConfirmado = true
        };
        Assert.True((await userManager.CreateAsync(usuario)).Succeeded);
        Assert.True((await userManager.AddToRoleAsync(usuario, PerfisAcesso.Recepcao)).Succeeded);

        dbContext.UserLoginIdentifiers.AddRange(
            new UserLoginIdentifier
            {
                UserId = usuario.Id,
                Identificador = "EQP-000003",
                Tipo = "EQP",
                Ativo = false
            },
            new UserLoginIdentifier
            {
                UserId = usuario.Id,
                Identificador = "ADM-000005",
                Tipo = "ADM",
                Ativo = false
            },
            new UserLoginIdentifier
            {
                UserId = usuario.Id,
                Identificador = "ADM-999999",
                Tipo = "ADM",
                Ativo = true
            });
        dbContext.EquipeMembros.Add(new EquipeMembro
        {
            UserId = usuario.Id,
            CodigoEquipe = "EQP-000003",
            Nome = "Membro Teste",
            PapelEquipe = EquipePapeis.Maintainer,
            FluxoTrabalho = EquipeFluxosTrabalho.LocalOwner,
            PodeCriarConvitesEquipe = true,
            Ativo = false
        });
        await dbContext.SaveChangesAsync();

        dbContext.ChangeTracker.Clear();
        var segurancaAntes = await dbContext.Users
            .AsNoTracking()
            .SingleAsync(item => item.Id == usuario.Id);

        var result = await service.RestaurarPermissoesPadraoAsync("EQP-000003");

        dbContext.ChangeTracker.Clear();
        var usuarioAtualizado = await dbContext.Users.SingleAsync(item => item.Id == usuario.Id);
        var membroAtualizado = await dbContext.EquipeMembros.SingleAsync(item => item.UserId == usuario.Id);
        var aliases = await dbContext.UserLoginIdentifiers
            .Where(item => item.UserId == usuario.Id)
            .ToDictionaryAsync(item => item.Identificador);
        var roles = await userManager.GetRolesAsync(usuarioAtualizado);

        Assert.Equal("ADM-000005", result.AdmId);
        Assert.Equal(
            new[] { PerfisAcesso.Adm, PerfisAcesso.Equipe },
            roles.OrderBy(item => item));
        Assert.True(aliases["EQP-000003"].Ativo);
        Assert.True(aliases["ADM-000005"].Ativo);
        Assert.False(aliases["ADM-999999"].Ativo);
        Assert.True(usuarioAtualizado.Ativo);
        Assert.Equal(PerfisAcesso.Equipe, usuarioAtualizado.Perfil);
        Assert.Equal(segurancaAntes.PasswordHash, usuarioAtualizado.PasswordHash);
        Assert.Equal(segurancaAntes.SecurityStamp, usuarioAtualizado.SecurityStamp);
        Assert.Equal(segurancaAntes.TwoFactorEnabled, usuarioAtualizado.TwoFactorEnabled);
        Assert.Equal(segurancaAntes.LockoutEnd, usuarioAtualizado.LockoutEnd);
        Assert.Equal(segurancaAntes.AccessFailedCount, usuarioAtualizado.AccessFailedCount);
        Assert.Equal(segurancaAntes.EmailRecuperacao, usuarioAtualizado.EmailRecuperacao);
        Assert.Equal(segurancaAntes.EmailRecuperacaoConfirmado, usuarioAtualizado.EmailRecuperacaoConfirmado);
        Assert.Equal(EquipePapeis.Contributor, membroAtualizado.PapelEquipe);
        Assert.Equal(EquipeFluxosTrabalho.ForkCodespaces, membroAtualizado.FluxoTrabalho);
        Assert.True(membroAtualizado.PrecisaFork);
        Assert.True(membroAtualizado.UsaCodespaces);
        Assert.False(membroAtualizado.PodeCriarConvitesEquipe);
        Assert.True(membroAtualizado.Ativo);
    }

    private sealed class FakeEquipeDbGitHubService : IEquipeDbGitHubService
    {
        private readonly EquipeDbMembro _member;

        public FakeEquipeDbGitHubService(EquipeDbMembro member)
        {
            _member = member;
        }

        public bool LeituraConfigurada => true;
        public bool EscritaConfigurada => false;
        public string RepositoryLabel => "test/repository";
        public string DbPath => "data/equipe-db.json";
        public string EventsPath => "data/equipe-events.ndjson";
        public string AccessRequestsPath => "data/access-requests.json";

        public Task<EquipeDbFile> LerAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new EquipeDbFile
            {
                Exists = true,
                Document = new EquipeDbDocument { Membros = [_member] }
            });

        public Task SalvarAsync(
            EquipeDbDocument document,
            string? sha,
            string commitMessage,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task AcrescentarEventoAsync(
            EquipeDbEvent evento,
            string commitMessage,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<EquipeAccessRequestsFile> LerSolicitacoesAcessoAsync(
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task SalvarSolicitacoesAcessoAsync(
            EquipeAccessRequestsDocument document,
            string? sha,
            string commitMessage,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
