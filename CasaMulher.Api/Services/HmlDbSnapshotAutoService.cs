using CasaMulher.Api.Services;

namespace CasaMulher.Api.Services;

public sealed class HmlDbSnapshotAutoService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<HmlDbSnapshotAutoService> _logger;
    private readonly IConfiguration _configuration;
    private readonly HmlDbStorageInfo _storage;

    public HmlDbSnapshotAutoService(
        IServiceProvider serviceProvider,
        ILogger<HmlDbSnapshotAutoService> logger,
        IConfiguration configuration,
        HmlDbStorageInfo storage)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _configuration = configuration;
        _storage = storage;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalMinutes = _configuration.GetValue("HML_DB_SNAPSHOT_INTERVAL_MINUTES", 10);
        var interval = TimeSpan.FromMinutes(intervalMinutes);
        
        // Registra o estado inicial antes da primeira espera. Assim, qualquer
        // alteração feita nos primeiros minutos de vida da aplicação será
        // detectada no primeiro ciclo, em vez de ser tratada como baseline.
        var lastWriteTime = ObterUltimaAlteracaoBanco();

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(interval, stoppingToken);

            var autoEnabled = _configuration.GetValue("HML_DB_SNAPSHOT_AUTO_ENABLED", true);
            if (!autoEnabled)
            {
                continue;
            }

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var snapshotService = scope.ServiceProvider.GetRequiredService<HmlDbSnapshotService>();
                
                if (!snapshotService.Configured) continue;
                var currentWriteTime = ObterUltimaAlteracaoBanco();
                if (currentWriteTime is null) continue;

                // Se o arquivo SQLite foi modificado
                if (lastWriteTime is null || currentWriteTime > lastWriteTime)
                {
                    _logger.LogInformation("Mudança detectada no banco SQLite. Iniciando auto-snapshot.");
                    await snapshotService.CreateAndUploadAsync(stoppingToken, "auto_timer");
                    lastWriteTime = currentWriteTime;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha na execução do auto-snapshot (pode ser conflito de geração ou erro de rede).");
            }
        }
    }

    private DateTime? ObterUltimaAlteracaoBanco()
    {
        DateTime? ultimaAlteracao = null;
        var arquivos = new[]
        {
            _storage.DatabasePath,
            _storage.DatabasePath + "-wal",
            _storage.DatabasePath + "-journal"
        };

        foreach (var arquivo in arquivos)
        {
            if (!File.Exists(arquivo)) continue;

            var alteracao = new FileInfo(arquivo).LastWriteTimeUtc;
            if (ultimaAlteracao is null || alteracao > ultimaAlteracao)
            {
                ultimaAlteracao = alteracao;
            }
        }

        return ultimaAlteracao;
    }
}
