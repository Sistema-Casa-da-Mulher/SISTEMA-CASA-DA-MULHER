using CasaMulher.Api.Services;
using Microsoft.Extensions.Configuration;

namespace CasaMulher.Api.Tests.Services;

public class FrontendUrlServiceTests
{
    [Fact]
    public void ObterBaseUrl_PriorizaPortalEqpBaseUrl()
    {
        var service = CriarService(new Dictionary<string, string?>
        {
            ["PORTAL_EQP_BASE_URL"] = " https://portal.example.com/ ",
            ["RENDER_EXTERNAL_URL"] = "https://render.example.com",
            ["Frontend:BaseUrl"] = "https://frontend.example.com"
        });

        Assert.Equal("https://portal.example.com", service.ObterBaseUrl());
    }

    [Fact]
    public void ObterBaseUrl_UsaRenderExternalUrlComoFallback()
    {
        var service = CriarService(new Dictionary<string, string?>
        {
            ["RENDER_EXTERNAL_URL"] = "https://render.example.com/",
            ["Frontend:BaseUrl"] = "https://frontend.example.com"
        });

        Assert.Equal("https://render.example.com", service.ObterBaseUrl());
    }

    [Fact]
    public void ObterBaseUrl_IgnoraValorInvalidoEUsaFrontendConfigurado()
    {
        var service = CriarService(new Dictionary<string, string?>
        {
            ["PORTAL_EQP_BASE_URL"] = "portal-sem-esquema",
            ["Frontend:BaseUrl"] = "http://localhost:5500/projetocasadamulher/telas/"
        });

        Assert.Equal(
            "http://localhost:5500/projetocasadamulher/telas",
            service.ObterBaseUrl());
    }

    [Fact]
    public void ObterBaseUrl_RejeitaEsquemaQueNaoSejaHttp()
    {
        var service = CriarService(new Dictionary<string, string?>
        {
            ["PORTAL_EQP_BASE_URL"] = "file:///tmp/portal"
        });

        Assert.Null(service.ObterBaseUrl());
    }

    [Fact]
    public void CriarLink_CombinaBaseECaminhoSemBarrasDuplicadas()
    {
        var service = CriarService(new Dictionary<string, string?>
        {
            ["PORTAL_EQP_BASE_URL"] = "https://portal.example.com/"
        });

        Assert.Equal(
            "https://portal.example.com/cadastro.html?codigo=abc",
            service.CriarLink("/cadastro.html?codigo=abc"));
    }

    [Fact]
    public void CriarLink_RetornaNuloSemBaseValida()
    {
        var service = CriarService(new Dictionary<string, string?>());

        Assert.Null(service.CriarLink("cadastro.html"));
    }

    private static FrontendUrlService CriarService(Dictionary<string, string?> valores)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(valores)
            .Build();

        return new FrontendUrlService(configuration);
    }
}
