using Sakin.Common.Utilities;
using Xunit;

namespace Sakin.Common.Tests.Utilities
{
    public class TLSParserTests
    {
        [Fact]
        public void ParseTLSClientHello_ReturnsFailure_WhenPayloadTooShort()
        {
            byte[] payload = new byte[4];

            var (success, sni) = TLSParser.ParseTLSClientHello(payload);

            Assert.False(success);
            Assert.Equal(string.Empty, sni);
        }

        [Fact]
        public void ParseTLSClientHello_ReturnsFailure_WhenNotTLSHandshake()
        {
            byte[] payload = new byte[50];
            payload[0] = 0x15;

            var (success, sni) = TLSParser.ParseTLSClientHello(payload);

            Assert.False(success);
            Assert.Equal(string.Empty, sni);
        }

        [Fact]
        public void ParseTLSClientHello_ReturnsFailure_WhenNotClientHello()
        {
            byte[] payload = new byte[50];
            payload[0] = 0x16;
            payload[5] = 0x02;

            var (success, sni) = TLSParser.ParseTLSClientHello(payload);

            Assert.False(success);
            Assert.Equal(string.Empty, sni);
        }

        [Fact]
        public void ParseTLSClientHello_HandlesEmptyPayload()
        {
            byte[] payload = Array.Empty<byte>();

            var (success, sni) = TLSParser.ParseTLSClientHello(payload);

            Assert.False(success);
            Assert.Equal(string.Empty, sni);
        }
    }
}
