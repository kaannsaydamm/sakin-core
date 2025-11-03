using Microsoft.Extensions.Hosting;
using Npgsql;
using Sakin.Core.Sensor.Handlers;
using Sakin.Core.Sensor.Utils;
using SharpPcap;

namespace Sakin.Core.Sensor.Services
{
    public class NetworkSensorService : BackgroundService
    {
        private readonly IDatabaseHandler _databaseHandler;
        private readonly IPackageInspector _packageInspector;
        private NpgsqlConnection? _dbConnection;

        public NetworkSensorService(IDatabaseHandler databaseHandler, IPackageInspector packageInspector)
        {
            _databaseHandler = databaseHandler;
            _packageInspector = packageInspector;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var interfaces = CaptureDeviceList.Instance;
            if (interfaces.Count == 0)
            {
                Console.WriteLine("No network devices found.");
                return;
            }

            _dbConnection = _databaseHandler.InitDB();
            if (_dbConnection == null)
            {
                Console.WriteLine("PostgreSQL connection error.");
                return;
            }

            var wg = new ManualResetEvent(false);
            _packageInspector.MonitorTraffic(interfaces, _dbConnection, wg);

            await Task.Run(() => wg.WaitOne(), stoppingToken);
        }

        public override void Dispose()
        {
            _dbConnection?.Close();
            _dbConnection?.Dispose();
            base.Dispose();
        }
    }
}
