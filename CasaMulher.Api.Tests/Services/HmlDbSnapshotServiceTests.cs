using System.Net;
using CasaMulher.Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;

namespace CasaMulher.Api.Tests.Services;

public sealed class HmlDbSnapshotServiceTests
{
    [Fact]
    public async Task GitHub401NaoDerrubaStartupEBloqueiaUploads()
    {
        var state = new HmlDbSnapshotState();
        var service = CriarService(HttpStatusCode.Unauthorized, state);

        var restaurado = await service.TryRestoreAtStartupAsync();

        Assert.False(restaurado);
        Assert.True(state.RestoreExecutado);
        Assert.True(state.UploadsBloqueados);
        Assert.Contains("HTTP 401", state.UltimoErroRestore);
    }

    [Fact]
    public async Task Manifest404PermiteCriacaoDoPrimeiroSnapshot()
    {
        var state = new HmlDbSnapshotState();
        var service = CriarService(HttpStatusCode.NotFound, state);

        var restaurado = await service.TryRestoreAtStartupAsync();

        Assert.False(restaurado);
        Assert.True(state.RestoreExecutado);
        Assert.False(state.UploadsBloqueados);
        Assert.Null(state.UltimoErroRestore);
    }

    [Fact]
    public async Task TokenComAspasEPrefixoBearerEhNormalizado()
    {
        var handler = new RecordingHandler(HttpStatusCode.NotFound);
        var configuration = CriarConfiguration("  \"Bearer token-limpo\"  ");
        var github = new GitHubPrivateFileService(new HttpClient(handler), configuration);

        await github.ReadAsync("manifest.json");

        Assert.Equal("Bearer", handler.AuthorizationScheme);
        Assert.Equal("token-limpo", handler.AuthorizationParameter);
    }

    private static HmlDbSnapshotService CriarService(
        HttpStatusCode statusCode,
        HmlDbSnapshotState state)
    {
        var configuration = CriarConfiguration("token-de-teste");
        var github = new GitHubPrivateFileService(
            new HttpClient(new RecordingHandler(statusCode)),
            configuration);
        var environment = new TestWebHostEnvironment();
        var storage = new HmlDbStorageInfo(
            Path.Combine(Path.GetTempPath(), $"casa-mulher-test-{Guid.NewGuid():N}.db"),
            true);

        return new HmlDbSnapshotService(
            github,
            configuration,
            environment,
            storage,
            state,
            NullLogger<HmlDbSnapshotService>.Instance);
    }

    private static IConfiguration CriarConfiguration(string readToken) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["HML_DB_SNAPSHOT_ENABLED"] = "true",
                ["HML_DB_SNAPSHOT_KEY"] = "chave-presente",
                ["GITHUB_EQP_READ_TOKEN"] = readToken,
                ["GITHUB_EQP_WRITE_TOKEN"] = "write-token"
            })
            .Build();

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;

        public RecordingHandler(HttpStatusCode statusCode)
        {
            _statusCode = statusCode;
        }

        public string? AuthorizationScheme { get; private set; }
        public string? AuthorizationParameter { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            AuthorizationScheme = request.Headers.Authorization?.Scheme;
            AuthorizationParameter = request.Headers.Authorization?.Parameter;
            return Task.FromResult(new HttpResponseMessage(_statusCode));
        }
    }

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "CasaMulher.Api.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = Path.GetTempPath();
        public string EnvironmentName { get; set; } = "Staging";
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
