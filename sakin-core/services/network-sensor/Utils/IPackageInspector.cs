using SharpPcap;

namespace Sakin.Core.Sensor.Utils
{
    public interface IPackageInspector
    {
        void MonitorTraffic(IEnumerable<ICaptureDevice> interfaces, ManualResetEvent wg);
    }
}
