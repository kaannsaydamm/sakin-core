using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Npgsql;
using PacketDotNet;
using SAKINCore.Handlers;
using SharpPcap;

namespace SAKINCore.Utils
{
    public static class PackageInspector
    {
        // Girdi stringini temizler, sadece yazılabilir karakterleri tutar
        private static string CleanString(string input)
        {
            var cleaned = new StringBuilder();
            foreach (var c in input)
            {
                if (c >= 32 && c <= 126)  // Yalnızca yazılabilir ASCII karakterleri tut
                {
                    cleaned.Append(c);
                }
            }
            return cleaned.ToString();
        }

        // Ağı izleyen ve paketleri işleyen fonksiyon
        public static void MonitorTraffic(IEnumerable<ICaptureDevice> interfaces, NpgsqlConnection dbConnection, ManualResetEvent wg)
        {
            var tasks = new List<Task>();

            foreach (var dev in interfaces)
            {
                Console.WriteLine($"Detected network interface: {dev.Name} - {dev.Description}");

                // Eğer loopback interface ise, atla
                if (dev.Name.Contains("Loopback"))
                {
                    Console.WriteLine($"Skipping loopback network interface: {dev.Name} - {dev.Description}");
                    continue;
                }

                // Her bir arayüz için bir iş parçacığı başlat
                tasks.Add(Task.Run(() => ProcessPackets(dev, dbConnection, wg)));
            }

            // Tüm görevlerin tamamlanmasını bekle
            Task.WhenAll(tasks);
        }

        // Paketleri işleme fonksiyonu
        private static void ProcessPackets(ICaptureDevice device, NpgsqlConnection dbConnection, ManualResetEvent wg)
        {
            try
            {
                // Ağı dinlemeye başla
                device.Open(DeviceModes.Promiscuous);

                Console.WriteLine($"Successfully opened network interface: {device.Name}");

                // Paket kaynağını oluştur
                device.OnPacketArrival += ((sender, e) =>
                {
                    // IPv4 paketini kontrol et
                    if (e.GetPacket().GetPacket().PayloadPacket is IPPacket ethPacket)
                    {
                        if (ethPacket is IPv4Packet ipPacket)
                        {
                            DateTime timestamp = DateTime.Now;
                            var srcIP = ipPacket.SourceAddress.ToString();
                            var dstIP = ipPacket.DestinationAddress.ToString();
                            var protocol = ipPacket.Protocol.ToString();
                            var protocolEnum = ipPacket.Protocol;

                            if (e.GetPacket().GetPacket().HasPayloadPacket && e.GetPacket().GetPacket()?.PayloadPacket?.Bytes?.Length > 0)
                            {

                                // TODO: Temporal solution, fix
                                int? offset = e.GetPacket().GetPacket()?.PayloadPacket?.BytesSegment?.Offset;
                                byte[] payload = e.GetPacket().GetPacket().PayloadPacket.Bytes;
                                int? lenght = e.GetPacket()?.GetPacket()?.PayloadPacket?.Bytes?.Length;

                                string decodedPayload = Encoding.ASCII.GetString(payload);

                                string pattern = @"(https?://[^\s]+|www\.[^\s]+)";

                                if (!string.IsNullOrWhiteSpace(decodedPayload))
                                {
                                    var validDomains = ExtractValidDomains(decodedPayload, pattern);

                                    foreach (var domain in validDomains)
                                    {
                                        // SNI register
                                        string cleanSni = CleanString(domain);
                                        Console.WriteLine($"Captured {cleanSni}");
                                        DatabaseHandler.SaveSNIAsync(dbConnection, cleanSni, srcIP, dstIP, protocol, timestamp).Wait();
                                    }
                                }

                                // this is for advanced debugging.
                                //for (int i = 0; i < payload.Length; i++)
                                //{
                                //    byte currentByte = payload[i];
                                //    char currentChar = (char)currentByte
                                //    Console.WriteLine($"Byte[{i}]: 0x{currentByte:X2} => Char: '{currentChar}'");
                                //}

                                //this is will be developed.
                                // TLS ClientHello paketi varsa, SNI'yi kontrol et
                                //if (IsTLSClientHello(payload, out string sni, out bool status))
                                //{
                                //    if (status == false) return;
                                //    if (!string.IsNullOrEmpty(sni))
                                //    {
                                //        Console.WriteLine($"Captured TLS ClientHello with SNI: {sni}");
                                //        string cleanSni = CleanString(sni);

                                //        // SNI verisini veritabanına kaydet
                                //        DatabaseHandler.SaveSNIAsync(dbConnection, cleanSni, srcIP, dstIP, protocol, timestamp).Wait();
                                //    }
                                //    else
                                //    {
                                //        Console.WriteLine("SNI is empty in ClientHello message.");
                                //    }
                                //}
                                //else
                                //{
                                //    Console.WriteLine("Captured non-TLS or unsupported message.");
                                //}
                            }
                            else
                            {
                                Console.WriteLine($"Captured encrypted HTTPS traffic from {srcIP} to {dstIP}");
                            }

                            // Paket verisini veritabanına kaydet
                            DatabaseHandler.SavePacketAsync(dbConnection, srcIP, dstIP, protocol.ToUpper(), timestamp).Wait();
                        }
                        else
                        {
                            Console.WriteLine("Non-IPv4 packet captured.");
                        }
                    }
                });

                // Paketleri yakalamaya başla
                device.StartCapture();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing packets on {device.Name}: {ex.Message}");
            }
        }

        // TLS ClientHello kontrolü ve SNI çıkartma fonksiyonu
        private static bool IsTLSClientHello(byte[] payload, out string sni, out bool status)
        {
            sni = string.Empty;
            status = false;

            (status, sni) = TLSParser.ParseTLSClientHello(payload);

            return status;
        }

        static List<string> ExtractValidDomains(string text, string pattern)
        {
            List<string> validDomains = [];

            // Use Regex to find all matches in the input text
            var matches = Regex.Matches(text, pattern);

            foreach (Match match in matches)
            {
                validDomains.Add(match.Value);
            }

            return validDomains;
        }
    }
}
