using CasaMulher.Api.Data;
using CasaMulher.Api.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CasaMulher.Api.Services;

public sealed class TermoSigiloService
{
    private readonly AppDbContext _dbContext;
    private readonly IMemoryCache _cache;

    public TermoSigiloService(AppDbContext dbContext, IMemoryCache cache)
    {
        _dbContext = dbContext;
        _cache = cache;
    }

    public async Task<bool> AceitePendenteAsync(string userId, string? perfilSessao, CancellationToken cancellationToken = default)
    {
        if (!TermoSigilo.PerfilExigeAceite(perfilSessao))
        {
            return false;
        }

        return await ObterDataAceiteAsync(userId, cancellationToken) is null;
    }

    public async Task<DateTime?> ObterDataAceiteAsync(string userId, CancellationToken cancellationToken = default)
    {
        // Só o aceite é cacheado: a pendência sempre é reconsultada no banco.
        if (_cache.TryGetValue(ChaveCache(userId), out DateTime aceitoEm))
        {
            return aceitoEm;
        }

        var aceite = await _dbContext.TermoSigiloAceites
            .AsNoTracking()
            .Where(registro => registro.UserId == userId && registro.VersaoTermo == TermoSigilo.Versao)
            .OrderBy(registro => registro.AceitoEm)
            .Select(registro => (DateTime?)registro.AceitoEm)
            .FirstOrDefaultAsync(cancellationToken);

        if (aceite is not null)
        {
            MarcarAceite(userId, aceite.Value);
        }

        return aceite;
    }

    public void MarcarAceite(string userId, DateTime aceitoEm)
    {
        _cache.Set(ChaveCache(userId), aceitoEm, TimeSpan.FromMinutes(30));
    }

    private static string ChaveCache(string userId) => $"termo-sigilo:{TermoSigilo.Versao}:{userId}";
}
