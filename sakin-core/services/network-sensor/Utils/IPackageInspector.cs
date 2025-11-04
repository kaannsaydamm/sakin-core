using Npgsql;
using SharpPcap;

namespace Sakin.Core.Sensor.Utils
{
    public interface IPackageInspector
    {
        void MonitorTraffic(IEnumerable<ICaptureDevice> interfaces, NpgsqlConnection? dbConnection, ManualResetEvent wg);
    }
}
