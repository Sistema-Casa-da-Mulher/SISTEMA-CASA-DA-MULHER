using CasaMulher.Api.Data;
using CasaMulher.Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CasaMulher.Api.Tests.Services;

public sealed class AssistidasServiceTests
{
    [Theory]
    [InlineData("529.982.247-25", true)]
    [InlineData("11144477735", true)]
    [InlineData("12345678900", false)]
    [InlineData("11111111111", false)]
    [InlineData("5299822472", false)]
    public void ValidaCpfPelosDigitos(string cpf, bool esperado)
    {
        Assert.Equal(esperado, AssistidasService.CpfValido(cpf));
    }

    [Theory]
    [InlineData("Culinaria", "Culinária e Autonomia", true)]
    [InlineData("Culinaria", "culinaria", true)]
    [InlineData("Danca", "Dança Circular", true)]
    [InlineData("Danca", "Culinária e Autonomia", false)]
    [InlineData("Juridica", "Culinária e Autonomia", false)]
    public void CursoDaProfessoraCorrespondeAoDirecionamento(string direcionamento, string curso, bool esperado)
    {
        Assert.Equal(esperado, AssistidasService.CursoCorresponde(direcionamento, curso));
    }

    [Fact]
    public async Task SeedDeHomologacaoEhImportadoUmaVez()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await dbContext.Database.EnsureCreatedAsync();
        var service = new AssistidasService(dbContext, NullLogger<AssistidasService>.Instance);

        var documento = new HomologacaoSeedDocument
        {
            Recepcao =
            [
                new HomologacaoSeedRecepcao(1, "Ana Ficticia", "529.982.247-25", "(11) 90000-0000", "1990-01-01", "Juridica", "", "Rua A", "Recepção", ""),
                new HomologacaoSeedRecepcao(2, "Duplicada", "52998224725", "", "", "", "", "", "", "")
            ]
        };

        Assert.Equal(1, await service.ImportarSeedAsync(documento));
        Assert.Equal(0, await service.ImportarSeedAsync(documento));

        var importada = await dbContext.Assistidas.SingleAsync();
        Assert.NotNull(importada.EncaminhadaJuridicoEm);
        Assert.Equal("52998224725", importada.Cpf);
    }
}
