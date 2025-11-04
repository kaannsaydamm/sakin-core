using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using PacketDotNet;
using Sakin.Common.Models;
using Sakin.Common.Utilities;
using Sakin.Core.Sensor.Messaging;
using SharpPcap;

namespace Sakin.Core.Sensor.Utils
{
    public class PackageInspector : IPackageInspector
    {
        private readonly IEventPublisher _publisher;
        private readonly ILogger<PackageInspector> _logger;

        public PackageInspector(IEventPublisher publisher, ILogger<PackageInspector> logger)
        {
            _publisher = publisher;
            _logger = logger;
        }

        public void MonitorTraffic(IEnumerable<ICaptureDevice> interfaces, ManualResetEvent wg)
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

                tasks.Add(Task.Run(() => ProcessPackets(dev, wg)));
            }

            Task.WhenAll(tasks);
        }

        private void ProcessPackets(ICaptureDevice device, ManualResetEvent wg)
        {
            try
            {
                device.Open(DeviceModes.Promiscuous);
                _logger.LogInformation("Successfully opened network interface: {Name}", device.Name);

                device.OnPacketArrival += ((sender, e) =>
                {
                    if (e.GetPacket().GetPacket().PayloadPacket is IPPacket ethPacket)
                    {
                        if (ethPacket is IPv4Packet ipPacket)
                        {
                            var timestamp = DateTime.UtcNow;
                            var srcIP = ipPacket.SourceAddress.ToString();
                            var dstIP = ipPacket.DestinationAddress.ToString();
                            var proto = MapProtocol(ipPacket.Protocol);

                            string? httpUrl = null;
                            int payloadLength = 0;

                            if (e.GetPacket().GetPacket().HasPayloadPacket && e.GetPacket().GetPacket()?.PayloadPacket?.Bytes?.Length > 0)
                            {
                                byte[] payload = e.GetPacket().GetPacket().PayloadPacket.Bytes;
                                payloadLength = payload.Length;
                                string decodedPayload = Encoding.ASCII.GetString(payload);
                                string pattern = @"(https?://[^\s]+|www\.[^\s]+)";

                                if (!string.IsNullOrWhiteSpace(decodedPayload))
                                {
                                    var validDomains = ExtractValidDomains(decodedPayload, pattern);

                                    // Publish one event per domain found
                                    if (validDomains.Count > 0)
                                    {
                                        foreach (var domain in validDomains)
                                        {
                                            httpUrl = StringHelper.CleanString(domain);
                                            var evt = CreateNetworkEvent(device, timestamp, srcIP, dstIP, proto, httpUrl, payloadLength);
                                            _publisher.Enqueue(evt);
                                        }
                                        return; // already published events for this packet
                                    }
                                }
                            }
                            else
                            {
                                _logger.LogDebug("Captured packet without payload from {Src} to {Dst}", srcIP, dstIP);
                            }

                            // Publish a generic network event for the packet
                            var genericEvt = CreateNetworkEvent(device, timestamp, srcIP, dstIP, proto, httpUrl, payloadLength);
                            _publisher.Enqueue(genericEvt);
                        }
                        else
                        {
                            _logger.LogDebug("Non-IPv4 packet captured.");
                        }
                    }
                });

                device.StartCapture();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing packets on {Device}", device.Name);
            }
        }

        private static NetworkEvent CreateNetworkEvent(ICaptureDevice device, DateTime timestamp, string srcIP, string dstIP, Protocol protocol, string? httpUrl, int payloadLength)
        {
            return new NetworkEvent
            {
                Timestamp = timestamp,
                SourceIp = srcIP,
                DestinationIp = dstIP,
                Protocol = protocol,
                HttpUrl = httpUrl,
                PacketCount = 1,
                BytesSent = 0,
                BytesReceived = payloadLength,
                DeviceName = device.Name,
                Metadata = new Dictionary<string, object>
                {
                    ["interfaceDescription"] = device.Description ?? string.Empty
                }
            };
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

        private static Protocol MapProtocol(ProtocolType protocolType)
        {
            return protocolType switch
            {
                ProtocolType.Tcp => Protocol.TCP,
                ProtocolType.Udp => Protocol.UDP,
                ProtocolType.Icmp => Protocol.ICMP,
                _ => Protocol.Unknown
            };
        }
    }
}
