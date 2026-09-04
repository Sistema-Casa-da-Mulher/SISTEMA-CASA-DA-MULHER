using CasaMulher.Api.Services;

namespace CasaMulher.Api.Tests.Services;

public sealed class HmlDbSnapshotStateTests
{
    [Fact]
    public void FalhaDeRestoreBloqueiaUpload()
    {
        var state = new HmlDbSnapshotState();

        state.MarkRestoreError("GitHub respondeu HTTP 401.");

        Assert.True(state.UploadsBloqueados);
        Assert.Equal("GitHub respondeu HTTP 401.", state.MotivoUploadsBloqueados);
        Assert.False(state.UltimoRestoreSucesso);
    }

    [Fact]
    public void RestoreValidoLiberaUpload()
    {
        var state = new HmlDbSnapshotState();
        state.MarkRestoreError("falha anterior");

        state.MarkRestoreSuccess(DateTimeOffset.UtcNow);

        Assert.False(state.UploadsBloqueados);
        Assert.Null(state.MotivoUploadsBloqueados);
        Assert.True(state.UltimoRestoreSucesso);
    }

    [Fact]
    public void RepositorioSemPrimeiroSnapshotPermiteUploadInicial()
    {
        var state = new HmlDbSnapshotState();

        state.MarkRestoreSemSnapshot();

        Assert.False(state.UploadsBloqueados);
        Assert.True(state.RestoreExecutado);
    }
}
