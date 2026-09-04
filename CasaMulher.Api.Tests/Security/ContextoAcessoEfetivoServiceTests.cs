using System.Security.Claims;
using CasaMulher.Api.Data;
using CasaMulher.Api.Models;
using CasaMulher.Api.Security;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace CasaMulher.Api.Tests.Security;

public sealed class ContextoAcessoEfetivoServiceTests
{
    [Fact]
    public async Task AdmPareadoNaoOwnerRecebePermissoesDeAdm()
    {
        await using var fixture = await TestFixture.CreateAsync();
        var usuario = await fixture.AddPairedUserAsync(
            "usuario-pareado",
            "EQP-000003",
            "ADM-000005");

        var principal = CriarPrincipal(usuario.Id, PerfisAcesso.Adm, "ADM-000005");

        var contexto = await fixture.Service.ObterAsync(principal);

        Assert.NotNull(contexto);
        Assert.Equal(PerfisAcesso.Adm, contexto.Perfil);
        Assert.Equal("ADM-000005", contexto.IdentificadorFuncionario);
        Assert.True(await fixture.Service.PodeGerenciarAreaInstitucionalAsync(principal));
        Assert.False(await fixture.Service.EhMasterAsync(principal));
    }

    [Fact]
    public async Task MesmaContaEntrandoComoEqpMantemLimitacoesDeEquipe()
    {
        await using var fixture = await TestFixture.CreateAsync();
        var usuario = await fixture.AddPairedUserAsync(
            "usuario-pareado",
            "EQP-000003",
            "ADM-000005");

        var principal = CriarPrincipal(usuario.Id, PerfisAcesso.Equipe, "EQP-000003");

        var contexto = await fixture.Service.ObterAsync(principal);

        Assert.NotNull(contexto);
        Assert.Equal(PerfisAcesso.Equipe, contexto.Perfil);
        Assert.False(await fixture.Service.PodeGerenciarAreaInstitucionalAsync(principal));
    }

    [Fact]
    public async Task AdmOwnerEhReconhecidoPeloIdentificadorEfetivo()
    {
        await using var fixture = await TestFixture.CreateAsync();
        var usuario = await fixture.AddPairedUserAsync(
            "owner",
            "EQP-000001",
            "ADM-000003");
        fixture.DbContext.EquipeMembros.Add(new EquipeMembro
        {
            UserId = usuario.Id,
            CodigoEquipe = "EQP-000001",
            PapelEquipe = EquipePapeis.Owner,
            FluxoTrabalho = EquipeFluxosTrabalho.ForkCodespaces,
            Ativo = true
        });
        await fixture.DbContext.SaveChangesAsync();

        var principal = CriarPrincipal(usuario.Id, PerfisAcesso.Adm, "ADM-000003");

        Assert.True(await fixture.Service.EhSuperAdminInstitucionalAsync(principal));
        Assert.True(await fixture.Service.EhMasterAsync(principal));
        Assert.True(await fixture.Service.PodeGerenciarAreaInstitucionalAsync(principal));
    }

    [Fact]
    public async Task UsuarioInstitucionalComumNaoGerenciaAdministracao()
    {
        await using var fixture = await TestFixture.CreateAsync();
        var usuario = await fixture.AddInstitutionalUserAsync(
            "usuario-recepcao",
            "REC-000001",
            PerfisAcesso.Recepcao);
        var principal = CriarPrincipal(
            usuario.Id,
            PerfisAcesso.Recepcao,
            "REC-000001");

        Assert.NotNull(await fixture.Service.ObterAsync(principal));
        Assert.False(await fixture.Service.PodeGerenciarAreaInstitucionalAsync(principal));
    }

    [Theory]
    [InlineData(null, null, null)]
    [InlineData("adm", "ADM-000005", "equipe")]
    [InlineData("adm", "EQP-000003", "adm")]
    [InlineData("equipe", "ADM-000005", "equipe")]
    public async Task JwtAntigoOuComContextoInvalidoEhRecusado(
        string? perfil,
        string? identificador,
        string? role)
    {
        await using var fixture = await TestFixture.CreateAsync();
        var usuario = await fixture.AddPairedUserAsync(
            "usuario-pareado",
            "EQP-000003",
            "ADM-000005");
        var principal = CriarPrincipal(usuario.Id, perfil, identificador, role);

        Assert.Null(await fixture.Service.ObterAsync(principal));
        Assert.False(await fixture.Service.PodeGerenciarAreaInstitucionalAsync(principal));
    }

    [Fact]
    public async Task AliasRevogadoInvalidaSessaoExistente()
    {
        await using var fixture = await TestFixture.CreateAsync();
        var usuario = await fixture.AddPairedUserAsync(
            "usuario-pareado",
            "EQP-000003",
            "ADM-000005");
        var alias = await fixture.DbContext.UserLoginIdentifiers
            .SingleAsync(item => item.Identificador == "ADM-000005");
        alias.Ativo = false;
        await fixture.DbContext.SaveChangesAsync();

        var principal = CriarPrincipal(usuario.Id, PerfisAcesso.Adm, "ADM-000005");

        Assert.Null(await fixture.Service.ObterAsync(principal));
    }

    [Fact]
    public async Task AliasCanonicoRevogadoNaoUsaFallbackDoUsuario()
    {
        await using var fixture = await TestFixture.CreateAsync();
        var usuario = await fixture.AddPairedUserAsync(
            "usuario-pareado",
            "EQP-000003",
            "ADM-000005");
        var alias = await fixture.DbContext.UserLoginIdentifiers
            .SingleAsync(item => item.Identificador == "EQP-000003");
        alias.Ativo = false;
        await fixture.DbContext.SaveChangesAsync();

        var principal = CriarPrincipal(usuario.Id, PerfisAcesso.Equipe, "EQP-000003");

        Assert.Null(await fixture.Service.ObterAsync(principal));
    }

    private static ClaimsPrincipal CriarPrincipal(
        string userId,
        string? perfil,
        string? identificador,
        string? role = null)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId)
        };

        if (perfil is not null)
        {
            claims.Add(new Claim("perfil", perfil));
        }

        if (identificador is not null)
        {
            claims.Add(new Claim("identificadorFuncionario", identificador));
        }

        if ((role ?? perfil) is { } effectiveRole)
        {
            claims.Add(new Claim(ClaimTypes.Role, effectiveRole));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(
            claims,
            "Test",
            ClaimTypes.Name,
            ClaimTypes.Role));
    }

    private sealed class TestFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private TestFixture(
            SqliteConnection connection,
            AppDbContext dbContext,
            ContextoAcessoEfetivoService service)
        {
            _connection = connection;
            DbContext = dbContext;
            Service = service;
        }

        public AppDbContext DbContext { get; }
        public ContextoAcessoEfetivoService Service { get; }

        public static async Task<TestFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;
            var dbContext = new AppDbContext(options);
            await dbContext.Database.EnsureCreatedAsync();

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Seguranca:Master:SuperAdminIdentificador"] = "ADM-000003",
                    ["Seguranca:Master:EquipeOwnerCodigo"] = "EQP-000001"
                })
                .Build();
            var masterUserService = new MasterUserService(configuration);
            var service = new ContextoAcessoEfetivoService(dbContext, masterUserService);
            return new TestFixture(connection, dbContext, service);
        }

        public async Task<ApplicationUser> AddPairedUserAsync(
            string userId,
            string eqpId,
            string admId)
        {
            var usuario = await AddInstitutionalUserAsync(
                userId,
                eqpId,
                PerfisAcesso.Equipe);

            DbContext.UserLoginIdentifiers.AddRange(
                new UserLoginIdentifier
                {
                    UserId = usuario.Id,
                    Identificador = eqpId,
                    Tipo = "EQP",
                    Ativo = true
                },
                new UserLoginIdentifier
                {
                    UserId = usuario.Id,
                    Identificador = admId,
                    Tipo = "ADM",
                    Ativo = true
                });
            await DbContext.SaveChangesAsync();
            return usuario;
        }

        public async Task<ApplicationUser> AddInstitutionalUserAsync(
            string userId,
            string identificador,
            string perfil)
        {
            var usuario = new ApplicationUser
            {
                Id = userId,
                UserName = identificador,
                NormalizedUserName = identificador.ToUpperInvariant(),
                NomeCompleto = userId,
                Perfil = perfil,
                IdentificadorFuncionario = identificador,
                Ativo = true,
                SecurityStamp = Guid.NewGuid().ToString("N")
            };
            DbContext.Users.Add(usuario);
            await DbContext.SaveChangesAsync();
            return usuario;
        }

        public async ValueTask DisposeAsync()
        {
            await DbContext.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
