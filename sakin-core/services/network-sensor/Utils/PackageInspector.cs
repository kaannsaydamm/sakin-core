using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using PacketDotNet;
using Sakin.Common.Utilities;
using Sakin.Core.Sensor.Configuration;
using Sakin.Core.Sensor.Handlers;
using Sakin.Core.Sensor.Messaging;
using SharpPcap;

namespace Sakin.Core.Sensor.Utils
{
    public class PackageInspector : IPackageInspector
    {
        private readonly IDatabaseHandler _databaseHandler;
        private readonly IEventPublisher _eventPublisher;
        private readonly PostgresOptions _postgresOptions;
        private readonly ILogger<PackageInspector> _logger;

        public PackageInspector(
            IDatabaseHandler databaseHandler,
            IEventPublisher eventPublisher,
            IOptions<PostgresOptions> postgresOptions,
            ILogger<PackageInspector> logger)
        {
            _databaseHandler = databaseHandler;
            _eventPublisher = eventPublisher;
            _postgresOptions = postgresOptions.Value;
            _logger = logger;
        }

        public void MonitorTraffic(IEnumerable<ICaptureDevice> interfaces, NpgsqlConnection? dbConnection, ManualResetEvent wg)
        {
            var tasks = new List<Task>();

            foreach (var dev in interfaces)
            {
                _logger.LogInformation("Detected network interface: {Name} - {Description}", dev.Name, dev.Description);

                if (dev.Name.Contains("Loopback"))
                {
                    _logger.LogInformation("Skipping loopback network interface: {Name} - {Description}", dev.Name, dev.Description);
                    continue;
                }

                tasks.Add(Task.Run(() => ProcessPackets(dev, dbConnection, wg)));
            }

            Task.WhenAll(tasks);
        }

        private void ProcessPackets(ICaptureDevice device, NpgsqlConnection? dbConnection, ManualResetEvent wg)
        {
            try
            {
                device.Open(DeviceModes.Promiscuous);

                _logger.LogInformation("Successfully opened network interface: {Name}", device.Name);

                device.OnPacketArrival += (sender, e) =>
                {
                    try
                    {
                        var capture = e.GetPacket();
                        var basePacket = capture.GetPacket();

                        if (basePacket.PayloadPacket is not IPPacket ethPacket)
                        {
                            return;
                        }

                        if (ethPacket is not IPv4Packet ipPacket)
                        {
                            return;
                        }

                        DateTime timestamp = DateTime.UtcNow;
                        var srcIp = ipPacket.SourceAddress.ToString();
                        var dstIp = ipPacket.DestinationAddress.ToString();
                        var protocol = ipPacket.Protocol.ToString();

                        byte[]? payloadBytes = null;
                        string? payloadPreview = null;
                        string? sni = null;
                        int? srcPort = null;
                        int? dstPort = null;

                        if (ipPacket.PayloadPacket is TcpPacket tcpPacket)
                        {
                            srcPort = tcpPacket.SourcePort;
                            dstPort = tcpPacket.DestinationPort;
                        }
                        else if (ipPacket.PayloadPacket is UdpPacket udpPacket)
                        {
                            srcPort = udpPacket.SourcePort;
                            dstPort = udpPacket.DestinationPort;
                        }

                        if (basePacket.HasPayloadPacket && basePacket.PayloadPacket?.Bytes?.Length > 0)
                        {
                            payloadBytes = basePacket.PayloadPacket.Bytes;
                            var decodedPayload = Encoding.ASCII.GetString(payloadBytes);
                            payloadPreview = decodedPayload.Length > 100 ? decodedPayload[..100] : decodedPayload;

                            const string pattern = @"(https?://[^\s]+|www\.[^\s]+)";
                            if (!string.IsNullOrWhiteSpace(decodedPayload))
                            {
                                var validDomains = ExtractValidDomains(decodedPayload, pattern);

                                foreach (var domain in validDomains)
                                {
                                    var cleanSni = StringHelper.CleanString(domain);
                                    _logger.LogInformation("Captured SNI: {Sni}", cleanSni);
                                    sni = cleanSni;

                                    if (_postgresOptions.WriteEnabled && dbConnection != null)
                                    {
                                        FireAndForget(
                                            _databaseHandler.SaveSNIAsync(dbConnection, cleanSni, srcIp, dstIp, protocol, timestamp),
                                            "saving SNI to Postgres");
                                    }
                                }
                            }
                        }

                        var captureLength = capture.Data?.Length ?? payloadBytes?.Length ?? 0;

                        var metadata = new Dictionary<string, object>
                        {
                            { "deviceName", device.Name },
                            { "deviceDescription", device.Description ?? string.Empty },
                            { "captureLength", captureLength },
                            { "payloadLength", payloadBytes?.Length ?? 0 },
                            { "sensorId", device.Name }
                        };

                        if (!string.IsNullOrWhiteSpace(sni))
                        {
                            metadata["sni"] = sni;
                        }

                        var packetEventData = new PacketEventData
                        {
                            SourceIp = srcIp,
                            DestinationIp = dstIp,
                            Protocol = protocol.ToUpperInvariant(),
                            Timestamp = timestamp,
                            SourcePort = srcPort,
                            DestinationPort = dstPort,
                            RawPayload = payloadBytes != null ? Convert.ToBase64String(payloadBytes) : string.Empty,
                            PayloadPreview = payloadPreview,
                            Sni = sni,
                            Metadata = metadata
                        };

                        FireAndForget(
                            _eventPublisher.PublishPacketEventAsync(packetEventData),
                            "publishing packet event to Kafka");

                        if (_postgresOptions.WriteEnabled && dbConnection != null)
                        {
                            FireAndForget(
                                _databaseHandler.SavePacketAsync(dbConnection, srcIp, dstIp, protocol.ToUpperInvariant(), timestamp),
                                "saving packet metadata to Postgres");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing packet");
                    }
                };

                device.StartCapture();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing packets on {DeviceName}: {Message}", device.Name, ex.Message);
            }
        }

        private static bool IsTLSClientHello(byte[] payload, out string sni, out bool status)
        {
            sni = string.Empty;
            status = false;

            (status, sni) = TLSParser.ParseTLSClientHello(payload);

            return status;
        }

        private void FireAndForget(Task task, string operationName)
        {
            task.ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    _logger.LogError(t.Exception, "Error during async operation: {OperationName}", operationName);
                }
            }, TaskScheduler.Default);
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
