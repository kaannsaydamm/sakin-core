using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using Sakin.Core.Sensor.Configuration;
using Sakin.Core.Sensor.Handlers;
using Sakin.Core.Sensor.Messaging;
using Sakin.Core.Sensor.Utils;
using SharpPcap;

namespace Sakin.Core.Sensor.Services
{
    public class NetworkSensorService : BackgroundService
    {
        private readonly IDatabaseHandler _databaseHandler;
        private readonly IPackageInspector _packageInspector;
        private readonly IEventPublisher _eventPublisher;
        private readonly PostgresOptions _postgresOptions;
        private readonly ILogger<NetworkSensorService> _logger;
        private NpgsqlConnection? _dbConnection;

        public NetworkSensorService(
            IDatabaseHandler databaseHandler,
            IPackageInspector packageInspector,
            IEventPublisher eventPublisher,
            IOptions<PostgresOptions> postgresOptions,
            ILogger<NetworkSensorService> logger)
        {
            _databaseHandler = databaseHandler;
            _packageInspector = packageInspector;
            _eventPublisher = eventPublisher;
            _postgresOptions = postgresOptions.Value;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var interfaces = CaptureDeviceList.Instance;
            if (interfaces.Count == 0)
            {
                _logger.LogWarning("No network devices found");
                return;
            }

            if (_postgresOptions.WriteEnabled)
            {
                _dbConnection = _databaseHandler.InitDB();
                if (_dbConnection == null)
                {
                    _logger.LogWarning("PostgreSQL connection error. Postgres writes disabled.");
                }
                else
                {
                    _logger.LogInformation("PostgreSQL connection established");
                }
            }
            else
            {
                _logger.LogInformation("PostgreSQL writes disabled by configuration");
            }

            var wg = new ManualResetEvent(false);
            _packageInspector.MonitorTraffic(interfaces, _dbConnection, wg);

            await Task.Run(() => wg.WaitOne(), stoppingToken);
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Network sensor service stopping, flushing events...");
            
            try
            {
                await _eventPublisher.FlushAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error flushing events during shutdown");
            }

            await base.StopAsync(cancellationToken);
        }

        public override void Dispose()
        {
            _dbConnection?.Close();
            _dbConnection?.Dispose();
            base.Dispose();
        }
    }
}
