using PacketDotNet;
using Sakin.Common.Models;
using Sakin.Common.Serialization;
using Sakin.Common.Utilities;
using SharpPcap;
using PacketDotNet;

namespace Sakin.Core.Sensor.Utils
{
    /// <summary>
    /// Factory for creating event envelopes from captured network packets
    /// </summary>
    public class NetworkEventEnvelopeFactory
    {
        private readonly EventEnvelopeSerializer _serializer;
        private readonly string _deviceId;
        private readonly string _sensorId;

        public NetworkEventEnvelopeFactory(string deviceId, string sensorId)
        {
            _serializer = new EventEnvelopeSerializer();
            _deviceId = deviceId;
            _sensorId = sensorId;
        }

        /// <summary>
        /// Creates an event envelope from a captured packet
        /// </summary>
        public EventEnvelope CreateFromPacket(RawCapture rawCapture, ICaptureDevice device)
        {
            var packet = PacketDotNet.Packet.ParsePacket(rawCapture.LinkLayerType, rawCapture.Data);
            
            if (packet.PayloadPacket is not IPPacket ipPacket)
            {
                return CreateUnknownEvent(rawCapture, device);
            }

            var normalizedEvent = CreateNormalizedEventFromPacket(ipPacket, rawCapture);
            var rawEventData = CreateRawEventData(rawCapture, device, packet);

            return _serializer.CreateEnvelope(
                normalizedEvent,
                _sensorId,
                SourceType.NetworkSensor,
                rawEventData
            );
        }

        /// <summary>
        /// Creates a normalized event from an IP packet
        /// </summary>
        private NormalizedEvent CreateNormalizedEventFromPacket(IPPacket ipPacket, RawCapture rawCapture)
        {
            var protocol = MapProtocol(ipPacket.Protocol);
            var eventType = DetermineEventType(ipPacket, protocol);
            var severity = DetermineSeverity(ipPacket, protocol);

            var normalizedEvent = new NetworkEvent
            {
                Timestamp = rawCapture.Timestamp,
                EventType = eventType,
                Severity = severity,
                SourceIp = ipPacket.SourceAddress.ToString(),
                DestinationIp = ipPacket.DestinationAddress.ToString(),
                Protocol = protocol,
                DeviceName = _deviceId,
                SensorId = _sensorId,
                Metadata = new Dictionary<string, object>()
            };

            // Add port information if available
            if (ipPacket is TcpPacket tcpPacket)
            {
                normalizedEvent = normalizedEvent with
                {
                    SourcePort = tcpPacket.SourcePort,
                    DestinationPort = tcpPacket.DestinationPort
                };

                // Check for HTTP/HTTPS traffic
                if (tcpPacket.PayloadData != null && tcpPacket.PayloadData.Length > 0)
                {
                    var httpInfo = ExtractHttpInfo(tcpPacket.PayloadData);
                    if (httpInfo != null)
                    {
                        normalizedEvent = normalizedEvent with
                        {
                            HttpUrl = httpInfo.Url,
                            HttpMethod = httpInfo.Method,
                            HttpStatusCode = httpInfo.StatusCode,
                            UserAgent = httpInfo.UserAgent,
                            BytesSent = httpInfo.BytesSent,
                            BytesReceived = httpInfo.BytesReceived,
                            PacketCount = 1
                        };
                    }
                }

                // Check for TLS SNI
                if (tcpPacket.DestinationPort == 443 || tcpPacket.SourcePort == 443)
                {
                    var sni = ExtractSNI(tcpPacket.PayloadData);
                    if (!string.IsNullOrEmpty(sni))
                    {
                        normalizedEvent = normalizedEvent with { Sni = sni };
                    }
                }
            }
            else if (ipPacket is UdpPacket udpPacket)
            {
                normalizedEvent = normalizedEvent with
                {
                    SourcePort = udpPacket.SourcePort,
                    DestinationPort = udpPacket.DestinationPort
                };

                // Check for DNS traffic
                if (udpPacket.DestinationPort == 53 || udpPacket.SourcePort == 53)
                {
                    var dnsQuery = ExtractDNSQuery(udpPacket.PayloadData);
                    if (!string.IsNullOrEmpty(dnsQuery))
                    {
                        normalizedEvent = normalizedEvent with
                        {
                            EventType = EventType.DnsQuery,
                            Payload = dnsQuery
                        };
                    }
                }
            }

            return normalizedEvent;
        }

        /// <summary>
        /// Creates raw event data for the envelope
        /// </summary>
        private object CreateRawEventData(RawCapture rawCapture, ICaptureDevice device, Packet packet)
        {
            return new
            {
                interfaceName = device.Name,
                interfaceDescription = device.Description,
                linkLayerType = rawCapture.LinkLayerType.ToString(),
                captureLength = rawCapture.Data.Length,
                timestamp = rawCapture.Timestamp,
                packetLength = packet.Length,
                protocol = packet.GetType().Name,
                hasPayload = packet.HasPayloadPacket,
                payloadLength = packet.PayloadPacket?.Bytes?.Length ?? 0
            };
        }

        /// <summary>
        /// Creates an unknown event for non-IP packets
        /// </summary>
        private EventEnvelope CreateUnknownEvent(RawCapture rawCapture, ICaptureDevice device)
        {
            var normalizedEvent = new NormalizedEvent
            {
                Timestamp = rawCapture.Timestamp,
                EventType = EventType.Unknown,
                Severity = Severity.Info,
                SourceIp = string.Empty,
                DestinationIp = string.Empty,
                Protocol = Protocol.Unknown,
                DeviceName = _deviceId,
                SensorId = _sensorId,
                Metadata = new Dictionary<string, object>
                {
                    { "packetType", "Non-IP" },
                    { "captureLength", rawCapture.Data.Length }
                }
            };

            var rawEventData = new
            {
                interfaceName = device.Name,
                interfaceDescription = device.Description,
                linkLayerType = rawCapture.LinkLayerType.ToString(),
                captureLength = rawCapture.Data.Length,
                timestamp = rawCapture.Timestamp,
                packetType = "Non-IP"
            };

            return _serializer.CreateEnvelope(
                normalizedEvent,
                _sensorId,
                SourceType.NetworkSensor,
                rawEventData
            );
        }

        private Protocol MapProtocol(PacketDotNet.IPProtocol ipProtocol)
        {
            return ipProtocol switch
            {
                PacketDotNet.IPProtocol.Tcp => Protocol.TCP,
                PacketDotNet.IPProtocol.Udp => Protocol.UDP,
                PacketDotNet.IPProtocol.Icmp => Protocol.ICMP,
                PacketDotNet.IPProtocol.Igmp => Protocol.Unknown,
                _ => Protocol.Unknown
            };
        }

        private EventType DetermineEventType(IPPacket ipPacket, Protocol protocol)
        {
            if (ipPacket is TcpPacket tcpPacket)
            {
                if (tcpPacket.DestinationPort == 80 || tcpPacket.SourcePort == 80 ||
                    tcpPacket.DestinationPort == 8080 || tcpPacket.SourcePort == 8080)
                {
                    return EventType.HttpRequest;
                }
                if (tcpPacket.DestinationPort == 443 || tcpPacket.SourcePort == 443)
                {
                    return EventType.TlsHandshake;
                }
                if (tcpPacket.DestinationPort == 22 || tcpPacket.SourcePort == 22)
                {
                    return EventType.SshConnection;
                }
            }

            if (ipPacket is UdpPacket udpPacket)
            {
                if (udpPacket.DestinationPort == 53 || udpPacket.SourcePort == 53)
                {
                    return EventType.DnsQuery;
                }
            }

            return EventType.NetworkTraffic;
        }

        private Severity DetermineSeverity(IPPacket ipPacket, Protocol protocol)
        {
            // Basic severity determination logic
            // Could be enhanced with threat intelligence
            return Severity.Info;
        }

        private HttpInfo? ExtractHttpInfo(byte[] payloadData)
        {
            if (payloadData == null || payloadData.Length == 0)
                return null;

            try
            {
                var payload = System.Text.Encoding.ASCII.GetString(payloadData);
                
                // Simple HTTP request parsing
                var lines = payload.Split('\n', '\r');
                if (lines.Length == 0) return null;

                var requestLine = lines[0];
                var parts = requestLine.Split(' ');
                if (parts.Length >= 3)
                {
                    var method = parts[0];
                    var url = parts[1];
                    
                    if (Enum.TryParse<HttpMethod>(method, true, out _))
                    {
                        return new HttpInfo
                        {
                            Method = method,
                            Url = url.StartsWith("http") ? url : $"http://unknown{url}",
                            BytesSent = payloadData.Length,
                            BytesReceived = 0,
                            PacketCount = 1
                        };
                    }
                }
            }
            catch
            {
                // Ignore parsing errors
            }

            return null;
        }

        private string? ExtractSNI(byte[] payloadData)
        {
            if (payloadData == null || payloadData.Length == 0)
                return null;

            try
            {
                var (success, sni) = TLSParser.ParseTLSClientHello(payloadData);
                return success ? StringHelper.CleanString(sni) : null;
            }
            catch
            {
                return null;
            }
        }

        private string? ExtractDNSQuery(byte[] payloadData)
        {
            if (payloadData == null || payloadData.Length < 12)
                return null;

            try
            {
                // Basic DNS query extraction (simplified)
                // Skip DNS header (12 bytes)
                var questionStart = 12;
                if (questionStart >= payloadData.Length)
                    return null;

                var domainBytes = new List<byte>();
                var i = questionStart;
                
                while (i < payloadData.Length && payloadData[i] != 0)
                {
                    var length = payloadData[i];
                    if (length == 0 || i + length >= payloadData.Length)
                        break;
                    
                    i++;
                    for (var j = 0; j < length && i < payloadData.Length; j++)
                    {
                        domainBytes.Add(payloadData[i++]);
                    }
                    
                    if (i < payloadData.Length && payloadData[i] != 0)
                    {
                        domainBytes.Add((byte)'.');
                    }
                }

                if (domainBytes.Count > 0)
                {
                    return System.Text.Encoding.ASCII.GetString(domainBytes.ToArray());
                }
            }
            catch
            {
                // Ignore parsing errors
            }

            return null;
        }

        private record HttpInfo
        {
            public string Method { get; init; } = string.Empty;
            public string Url { get; init; } = string.Empty;
            public int? StatusCode { get; init; }
            public string? UserAgent { get; init; }
            public long BytesSent { get; init; }
            public long BytesReceived { get; init; }
            public int PacketCount { get; init; }
        }
    }
}