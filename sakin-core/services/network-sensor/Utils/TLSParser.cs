using System.Text;

namespace Sakin.Core.Sensor.Utils
{
    public static class TLSParser
    {
        public static (bool, string) ParseTLSClientHello(byte[] payload)
        {
            if (payload.Length < 5)
            {
                Console.WriteLine("Payload is too short to be a valid TLS ClientHello");
                return (false, string.Empty);
            }

            if (payload[0] != 0x16)
            {
                Console.WriteLine("Not a TLS Handshake message");
                return (false, string.Empty);
            }

            if (payload.Length < 43 || payload[5] != 0x01)
            {
                Console.WriteLine("Not a TLS ClientHello message");
                return (false, string.Empty);
            }

            int offset = 6;
            ushort helloLength = (ushort)((payload[offset] << 8) | payload[offset + 1]);
            offset += 2;

            if (payload.Length < offset + helloLength)
            {
                Console.WriteLine($"Payload length is too short, expected length: {offset + helloLength}, actual length: {payload.Length}");
                return (false, string.Empty);
            }

            offset += 2;

            offset += 33;

            offset += 2 + payload[offset - 1];

            if ((offset - 1) > payload.Length)
            {
                Console.WriteLine("Index out of range");
                return (false, string.Empty);
            }
            offset += 1 + payload[offset - 1];

            if (payload.Length > offset)
            {
                while (offset + 4 <= payload.Length)
                {
                    ushort extType = (ushort)((payload[offset] << 8) | payload[offset + 1]);
                    int extLength = (payload[offset + 2] << 8) | payload[offset + 3];
                    offset += 4;

                    if (offset + extLength > payload.Length)
                    {
                        Console.WriteLine("Extension length exceeds available data");
                        return (false, string.Empty);
                    }

                    if (extType == 0x00)
                    {
                        if (offset + extLength <= payload.Length)
                        {
                            string sni = Encoding.ASCII.GetString(payload, offset, extLength);
                            Console.WriteLine($"Detected SNI: {sni}");
                            return (true, sni);
                        }
                        else
                        {
                            Console.WriteLine("Invalid SNI extension length");
                            return (false, string.Empty);
                        }
                    }

                    offset += extLength;
                }
            }

            Console.WriteLine("No SNI extension found");
            return (false, string.Empty);
        }
    }
}
