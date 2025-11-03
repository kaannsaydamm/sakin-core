using System;
using System.Text;

namespace SAKINCore.Utils
{
    public static class TLSParser
    {
        // Parse a TLS ClientHello message and extract the SNI if available.
        public static (bool, string) ParseTLSClientHello(byte[] payload)
        {
            // Check if the payload contains a TLS handshake record (at least 5 bytes for record header)
            if (payload.Length < 5)
            {
                Console.WriteLine("Payload is too short to be a valid TLS ClientHello");
                return (false, string.Empty);
            }

            // Check if the record type is Handshake (0x16)
            if (payload[0] != 0x16)  // 0x16 is the TLS record type for Handshake
            {
                Console.WriteLine("Not a TLS Handshake message");
                return (false, string.Empty);
            }

            // Check if it is a ClientHello (HandshakeType == 0x01)
            if (payload.Length < 43 || payload[5] != 0x01)  // 0x01 is the ClientHello type
            {
                Console.WriteLine("Not a TLS ClientHello message");
                return (false, string.Empty);
            }

            // Extract the length of the message and check if there is enough data
            int offset = 6;
            ushort helloLength = (ushort)((payload[offset] << 8) | payload[offset + 1]);
            offset += 2;  // move past the length field

            // Ensure the total length of the payload is at least as long as the ClientHello message
            if (payload.Length < offset + helloLength)
            {
                Console.WriteLine($"Payload length is too short, expected length: {offset + helloLength}, actual length: {payload.Length}");
                return (false, string.Empty);
            }

            // Skip protocol version (2 bytes)
            offset += 2;

            // Skip random (32 bytes) and session ID length (1 byte) + session ID (variable)
            offset += 33;

            // Skip cipher suites length (2 bytes) + cipher suites (variable length)
            offset += 2 + payload[offset - 1];

            // Skip compression methods length (1 byte) + compression methods (variable length)
            if ((offset - 1) > payload.Length)
            {
                Console.WriteLine("Index out of range");
                return (false, string.Empty);
            }
            offset += 1 + payload[offset - 1];

            // Check if there are extensions, and if so, try to parse them
            if (payload.Length > offset)
            {
                // Now we are at the extensions area
                while (offset + 4 <= payload.Length)
                {
                    ushort extType = (ushort)((payload[offset] << 8) | payload[offset + 1]);
                    int extLength = (payload[offset + 2] << 8) | payload[offset + 3];
                    offset += 4;

                    // Ensure the extension length does not exceed available bytes
                    if (offset + extLength > payload.Length)
                    {
                        Console.WriteLine("Extension length exceeds available data");
                        return (false, string.Empty);
                    }

                    // Check if extension type is Server Name Indication (0x00)
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

                    // Move to next extension
                    offset += extLength;
                }
            }

            Console.WriteLine("No SNI extension found");
            return (false, string.Empty);
        }
    }
}
