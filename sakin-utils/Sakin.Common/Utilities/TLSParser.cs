using System.Text;

namespace Sakin.Common.Utilities
{
    public static class TLSParser
    {
        public static (bool Success, string Sni) ParseTLSClientHello(byte[] payload)
        {
            if (payload.Length < 5)
            {
                return (false, string.Empty);
            }

            if (payload[0] != 0x16)
            {
                return (false, string.Empty);
            }

            if (payload.Length < 43 || payload[5] != 0x01)
            {
                return (false, string.Empty);
            }

            int offset = 6;
            ushort helloLength = (ushort)((payload[offset] << 8) | payload[offset + 1]);
            offset += 2;

            if (payload.Length < offset + helloLength)
            {
                return (false, string.Empty);
            }

            offset += 2;
            offset += 33;
            offset += 2 + payload[offset - 1];

            if ((offset - 1) > payload.Length)
            {
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
                        return (false, string.Empty);
                    }

                    if (extType == 0x00)
                    {
                        if (offset + extLength <= payload.Length)
                        {
                            string sni = Encoding.ASCII.GetString(payload, offset, extLength);
                            return (true, sni);
                        }
                        else
                        {
                            return (false, string.Empty);
                        }
                    }

                    offset += extLength;
                }
            }

            return (false, string.Empty);
        }
    }
}
