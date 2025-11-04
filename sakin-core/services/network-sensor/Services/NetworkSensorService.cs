using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sakin.Core.Sensor.Utils;
using SharpPcap;

namespace Sakin.Core.Sensor.Services
{
    public class NetworkSensorService : BackgroundService
    {
        private readonly IPackageInspector _packageInspector;
        private readonly ILogger<NetworkSensorService> _logger;

        public NetworkSensorService(IPackageInspector packageInspector, ILogger<NetworkSensorService> logger)
        {
            _packageInspector = packageInspector;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var interfaces = CaptureDeviceList.Instance;
            if (interfaces.Count == 0)
            {
                _logger.LogWarning("No network devices found.");
                return;
            }

            var wg = new ManualResetEvent(false);
            _packageInspector.MonitorTraffic(interfaces, wg);

            await Task.Run(() => wg.WaitOne(), stoppingToken);
        }
    }
}
