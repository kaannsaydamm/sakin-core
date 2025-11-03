using SharpPcap;

namespace SAKINCore
{
    using SAKINCore.Handlers;
    using SAKINCore.Utils;

    class Program
    {
#pragma warning disable IDE0060 // Kullanılmayan parametreyi kaldırma
        static void Main(string[] args)
#pragma warning restore IDE0060 // Kullanılmayan parametreyi kaldırma
        {
            // Ağıt cihazlarını al
            var interfaces = CaptureDeviceList.Instance;
            if (interfaces.Count == 0)
            {
                Console.WriteLine("No network devices found.");
                return;
            }

            // PostgreSQL veritabanı bağlantısı kur
            var dbConnection = DatabaseHandler.InitDB();
            if (dbConnection == null)
            {
                Console.WriteLine("PostgreSQL connection error.");
                return;
            }
            // Programın kapanmadan önce veritabanı bağlantısını kapat
            AppDomain.CurrentDomain.ProcessExit += (sender, e) => dbConnection.Close();

            // Trafiği izlemeye başla
            var wg = new ManualResetEvent(false);
            PackageInspector.MonitorTraffic(interfaces, dbConnection, wg);
            wg.WaitOne();
        }
    }
}
