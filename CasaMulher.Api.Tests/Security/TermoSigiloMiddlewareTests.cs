using System.Security.Claims;
using CasaMulher.Api.Data;
using CasaMulher.Api.Middleware;
using CasaMulher.Api.Models;
using CasaMulher.Api.Security;
using CasaMulher.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CasaMulher.Api.Tests.Security;

public sealed class TermoSigiloMiddlewareTests
{
    [Fact]
    public async Task FuncionariaSemAceiteEhBloqueadaNaApi()
    {
        await using var fixture = await TestFixture.CreateAsync();
        await fixture.AddUserAsync("u1", PerfisAcesso.Recepcao);

        var (status, proximoChamado) = await fixture.InvokeAsync("/api/funcionarios", "u1", PerfisAcesso.Recepcao);

        Assert.Equal(StatusCodes.Status403Forbidden, status);
        Assert.False(proximoChamado);
    }

    [Theory]
    [InlineData("/api/auth/me")]
    [InlineData("/api/termo-sigilo")]
    [InlineData("/api/termo-sigilo/aceitar")]
    [InlineData("/painel.html")]
    public async Task RotasNecessariasParaAceitarContinuamLiberadas(string path)
    {
        await using var fixture = await TestFixture.CreateAsync();
        await fixture.AddUserAsync("u1", PerfisAcesso.Recepcao);

        var (_, proximoChamado) = await fixture.InvokeAsync(path, "u1", PerfisAcesso.Recepcao);

        Assert.True(proximoChamado);
    }

    [Fact]
    public async Task FuncionariaComAceiteDaVersaoAtualPassa()
    {
        await using var fixture = await TestFixture.CreateAsync();
        await fixture.AddUserAsync("u1", PerfisAcesso.Juridico);
        await fixture.AddAceiteAsync("u1", TermoSigilo.Versao);

        var (_, proximoChamado) = await fixture.InvokeAsync("/api/funcionarios", "u1", PerfisAcesso.Juridico);

        Assert.True(proximoChamado);
    }

    [Fact]
    public async Task AceiteDeVersaoAntigaNaoVale()
    {
        await using var fixture = await TestFixture.CreateAsync();
        await fixture.AddUserAsync("u1", PerfisAcesso.Adm);
        await fixture.AddAceiteAsync("u1", "versao-antiga");

        var (status, proximoChamado) = await fixture.InvokeAsync("/api/funcionarios", "u1", PerfisAcesso.Adm);

        Assert.Equal(StatusCodes.Status403Forbidden, status);
        Assert.False(proximoChamado);
    }

    [Fact]
    public async Task SessaoDeEquipeNaoPrecisaDoTermo()
    {
        await using var fixture = await TestFixture.CreateAsync();
        await fixture.AddUserAsync("u1", PerfisAcesso.Equipe);

        var (_, proximoChamado) = await fixture.InvokeAsync("/api/equipe/membros", "u1", PerfisAcesso.Equipe);

        Assert.True(proximoChamado);
    }

    [Fact]
    public async Task RequisicaoAnonimaNaoEhAfetada()
    {
        await using var fixture = await TestFixture.CreateAsync();

        var (_, proximoChamado) = await fixture.InvokeAsync("/api/funcionarios", null, null);

        Assert.True(proximoChamado);
    }

    private sealed class TestFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly MemoryCache _cache = new(new MemoryCacheOptions());

        private TestFixture(SqliteConnection connection, AppDbContext dbContext)
        {
            _connection = connection;
            DbContext = dbContext;
        }

        public AppDbContext DbContext { get; }

        public static async Task<TestFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
            var dbContext = new AppDbContext(options);
            await dbContext.Database.EnsureCreatedAsync();
            return new TestFixture(connection, dbContext);
        }

        public async Task AddUserAsync(string userId, string perfil)
        {
            DbContext.Users.Add(new ApplicationUser
            {
                Id = userId,
                UserName = userId,
                NormalizedUserName = userId.ToUpperInvariant(),
                NomeCompleto = "Maria da Silva",
                Perfil = perfil,
                IdentificadorFuncionario = userId,
                SecurityStamp = Guid.NewGuid().ToString("N")
            });
            await DbContext.SaveChangesAsync();
        }

        public async Task AddAceiteAsync(string userId, string versao)
        {
            DbContext.TermoSigiloAceites.Add(new TermoSigiloAceite
            {
                UserId = userId,
                IdentificadorFuncionario = userId,
                Perfil = "adm",
                VersaoTermo = versao,
                HashTermo = "hash",
                NomeAssinado = "Maria da Silva"
            });
            await DbContext.SaveChangesAsync();
        }

        public async Task<(int Status, bool ProximoChamado)> InvokeAsync(string path, string? userId, string? perfil)
        {
            var proximoChamado = false;
            var middleware = new TermoSigiloMiddleware(_ =>
            {
                proximoChamado = true;
                return Task.CompletedTask;
            });

            var context = new DefaultHttpContext();
            context.Request.Path = path;
            context.Response.Body = new MemoryStream();

            if (userId is not null)
            {
                context.User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, userId),
                    new Claim("perfil", perfil!)
                ], "Bearer"));
            }

            await middleware.InvokeAsync(context, new TermoSigiloService(DbContext, _cache));
            return (context.Response.StatusCode, proximoChamado);
        }

        public async ValueTask DisposeAsync()
        {
            _cache.Dispose();
            await DbContext.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
