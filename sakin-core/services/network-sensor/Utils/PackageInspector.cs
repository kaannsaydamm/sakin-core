using System.Text;
using System.Text.RegularExpressions;
using Npgsql;
using PacketDotNet;
using Sakin.Common.Models;
using Sakin.Common.Utilities;
using Sakin.Core.Sensor.Handlers;
using SharpPcap;

namespace Sakin.Core.Sensor.Utils
{
    public class PackageInspector : IPackageInspector
    {
        private readonly IDatabaseHandler _databaseHandler;
        private readonly NetworkEventEnvelopeFactory _envelopeFactory;

        public PackageInspector(IDatabaseHandler databaseHandler, NetworkEventEnvelopeFactory envelopeFactory)
        {
            _databaseHandler = databaseHandler;
            _envelopeFactory = envelopeFactory;
        }

        public void MonitorTraffic(IEnumerable<ICaptureDevice> interfaces, NpgsqlConnection dbConnection, ManualResetEvent wg)
        {
            var tasks = new List<Task>();

            foreach (var dev in interfaces)
            {
                Console.WriteLine($"Detected network interface: {dev.Name} - {dev.Description}");

                if (dev.Name.Contains("Loopback"))
                {
                    Console.WriteLine($"Skipping loopback network interface: {dev.Name} - {dev.Description}");
                    continue;
                }

                tasks.Add(Task.Run(() => ProcessPackets(dev, dbConnection, wg)));
            }

            Task.WhenAll(tasks);
        }

        /// <summary>
        /// Processes a single event envelope
        /// </summary>
        public async Task ProcessEventEnvelopeAsync(EventEnvelope envelope, NpgsqlConnection dbConnection)
        {
            try
            {
                // Save basic packet information
                await _databaseHandler.SavePacketAsync(
                    dbConnection,
                    envelope.Normalized.SourceIp,
                    envelope.Normalized.DestinationIp,
                    envelope.Normalized.Protocol.ToString().ToUpper(),
                    envelope.Normalized.Timestamp
                );

                // Save SNI information if available
                if (envelope.Normalized is NetworkEvent networkEvent && !string.IsNullOrEmpty(networkEvent.Sni))
                {
                    await _databaseHandler.SaveSNIAsync(
                        dbConnection,
                        networkEvent.Sni,
                        networkEvent.SourceIp,
                        networkEvent.DestinationIp,
                        networkEvent.Protocol.ToString(),
                        networkEvent.Timestamp
                    );
                }

                // Log envelope processing
                Console.WriteLine($"Processed envelope {envelope.Id} - {envelope.Normalized.EventType} from {envelope.Source}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing envelope {envelope.Id}: {ex.Message}");
            }
        }

        private void ProcessPackets(ICaptureDevice device, NpgsqlConnection dbConnection, ManualResetEvent wg)
        {
            try
            {
                device.Open(DeviceModes.Promiscuous);

                Console.WriteLine($"Successfully opened network interface: {device.Name}");

                device.OnPacketArrival += async (sender, e) =>
                {
                    try
                    {
                        // Create event envelope from captured packet
                        var envelope = _envelopeFactory.CreateFromPacket(e.GetPacket(), device);
                        
                        // Process the envelope
                        await ProcessEventEnvelopeAsync(envelope, dbConnection);

                        // Legacy processing for backward compatibility
                        if (e.GetPacket().GetPacket().PayloadPacket is IPPacket ethPacket)
                        {
                            if (ethPacket is IPv4Packet ipPacket)
                            {
                                DateTime timestamp = DateTime.Now;
                                var srcIP = ipPacket.SourceAddress.ToString();
                                var dstIP = ipPacket.DestinationAddress.ToString();
                                var protocol = ipPacket.Protocol.ToString();

                                if (e.GetPacket().GetPacket().HasPayloadPacket && e.GetPacket().GetPacket()?.PayloadPacket?.Bytes?.Length > 0)
                                {
                                    byte[] payload = e.GetPacket().GetPacket().PayloadPacket.Bytes;
                                    string decodedPayload = Encoding.ASCII.GetString(payload);
                                    string pattern = @"(https?://[^\s]+|www\.[^\s]+)";

                                    if (!string.IsNullOrWhiteSpace(decodedPayload))
                                    {
                                        var validDomains = ExtractValidDomains(decodedPayload, pattern);

                                        foreach (var domain in validDomains)
                                        {
                                            string cleanSni = StringHelper.CleanString(domain);
                                            Console.WriteLine($"Captured {cleanSni}");
                                            await _databaseHandler.SaveSNIAsync(dbConnection, cleanSni, srcIP, dstIP, protocol, timestamp);
                                        }
                                    }
                                }
                                else
                                {
                                    Console.WriteLine($"Captured encrypted HTTPS traffic from {srcIP} to {dstIP}");
                                }

                                await _databaseHandler.SavePacketAsync(dbConnection, srcIP, dstIP, protocol.ToUpper(), timestamp);
                            }
                            else
                            {
                                Console.WriteLine("Non-IPv4 packet captured.");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error processing packet on {device.Name}: {ex.Message}");
                    }
                };

                device.StartCapture();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing packets on {device.Name}: {ex.Message}");
            }
        }

        private static bool IsTLSClientHello(byte[] payload, out string sni, out bool status)
        {
            sni = string.Empty;
            status = false;

            (status, sni) = TLSParser.ParseTLSClientHello(payload);

            return status;
        }

        private static List<string> ExtractValidDomains(string text, string pattern)
        {
            List<string> validDomains = [];

            var matches = Regex.Matches(text, pattern);

            foreach (Match match in matches)
            {
                validDomains.Add(match.Value);
            }

            return validDomains;
        }
    }
}
