using CasaMulher.Api.Data;
using CasaMulher.Api.Models;
using CasaMulher.Api.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace CasaMulher.Api.Tests.Services;

/// <summary>Conta de teste SOC-000001: assume a funcionária fictícia do seed, mas nunca uma conta de verdade.</summary>
public sealed class ContaTesteServicoSocialSeederTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private ServiceProvider _services = null!;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AppDbContext>(o => o.UseSqlite(_connection));
        services.AddIdentityCore<ApplicationUser>().AddRoles<IdentityRole>().AddEntityFrameworkStores<AppDbContext>();
        _services = services.BuildServiceProvider();
        await using var scope = _services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _services.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private async Task<ApplicationUser> AdicionarAsync(string email, bool ativo, string? senhaHash)
    {
        await using var scope = _services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var u = new ApplicationUser
        {
            UserName = ContaTesteServicoSocialSeeder.Identificador, NormalizedUserName = ContaTesteServicoSocialSeeder.Identificador,
            IdentificadorFuncionario = ContaTesteServicoSocialSeeder.Identificador, Email = email, NormalizedEmail = email.ToUpperInvariant(),
            NomeCompleto = "Funcionária Fictícia 20", Perfil = PerfisAcesso.AssistenteSocial, Ativo = ativo, PasswordHash = senhaHash,
            DoisFatoresObrigatorio = true, SecurityStamp = Guid.NewGuid().ToString("N")
        };
        db.Users.Add(u);
        await db.SaveChangesAsync();
        return u;
    }

    private async Task<ApplicationUser> LerAsync()
    {
        await using var scope = _services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.AsNoTracking()
            .SingleAsync(u => u.IdentificadorFuncionario == ContaTesteServicoSocialSeeder.Identificador);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("hash-antigo-qualquer")]
    public async Task FuncionariaFicticiaDoSeedViraContaDeTeste(string? hashAntigo)
    {
        await AdicionarAsync("soc-000001@example.invalid", ativo: false, hashAntigo);

        await ContaTesteServicoSocialSeeder.GarantirAsync(_services, NullLogger.Instance);

        var conta = await LerAsync();
        Assert.True(conta.Ativo);
        Assert.False(conta.DoisFatoresObrigatorio);
        Assert.Equal(ContaTesteServicoSocialSeeder.Email, conta.Email);
        Assert.Equal(ContaTesteServicoSocialSeeder.CressFicticio, conta.Cress);
        Assert.NotEqual(hashAntigo, conta.PasswordHash);
        Assert.False(string.IsNullOrEmpty(conta.PasswordHash));
    }

    [Fact]
    public async Task ContaDeVerdadeComOMesmoIdNaoEhAlterada()
    {
        var real = await AdicionarAsync("assistente.real@exemplo.org", ativo: true, "hash-da-profissional");

        await ContaTesteServicoSocialSeeder.GarantirAsync(_services, NullLogger.Instance);

        var conta = await LerAsync();
        Assert.Equal("assistente.real@exemplo.org", conta.Email);
        Assert.Equal("hash-da-profissional", conta.PasswordHash);
        Assert.Equal(real.NomeCompleto, conta.NomeCompleto);
    }

    [Fact]
    public async Task SemContaCriaAContaDeTesteUmaVez()
    {
        await ContaTesteServicoSocialSeeder.GarantirAsync(_services, NullLogger.Instance);
        var primeira = await LerAsync();
        await ContaTesteServicoSocialSeeder.GarantirAsync(_services, NullLogger.Instance);
        var segunda = await LerAsync();

        Assert.True(primeira.Ativo);
        Assert.Equal(primeira.PasswordHash, segunda.PasswordHash);
        Assert.Equal(primeira.SecurityStamp, segunda.SecurityStamp);
    }
}
