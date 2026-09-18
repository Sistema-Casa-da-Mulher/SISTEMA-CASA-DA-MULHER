namespace CasaMulher.Api.Services;

public interface IFrontendUrlService
{
    string? ObterBaseUrl();

    string? CriarLink(string caminhoRelativo);
}

public sealed class FrontendUrlService : IFrontendUrlService
{
    private readonly IConfiguration _configuration;

    public FrontendUrlService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string? ObterBaseUrl()
    {
        var candidatos = new[]
        {
            _configuration["PORTAL_EQP_BASE_URL"],
            _configuration["RENDER_EXTERNAL_URL"],
            _configuration["Frontend:BaseUrl"]
        };

        foreach (var candidato in candidatos)
        {
            if (string.IsNullOrWhiteSpace(candidato))
            {
                continue;
            }

            var normalizado = candidato.Trim().TrimEnd('/');

            if (Uri.TryCreate(normalizado, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                return normalizado;
            }
        }

        return null;
    }

    public string? CriarLink(string caminhoRelativo)
    {
        var baseUrl = ObterBaseUrl();

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return null;
        }

        return $"{baseUrl}/{caminhoRelativo.TrimStart('/')}";
    }
}
