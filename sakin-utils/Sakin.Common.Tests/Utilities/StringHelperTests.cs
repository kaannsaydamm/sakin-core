using Sakin.Common.Utilities;
using Xunit;

namespace Sakin.Common.Tests.Utilities
{
    public class StringHelperTests
    {
        [Fact]
        public void CleanString_RemovesNonPrintableCharacters()
        {
            string input = "Hello\x00\x01World\x7F";
            string expected = "HelloWorld";

            string result = StringHelper.CleanString(input);

            Assert.Equal(expected, result);
        }

        [Fact]
        public void CleanString_PreservesValidCharacters()
        {
            string input = "Hello World 123!@#$%^&*()";
            string expected = "Hello World 123!@#$%^&*()";

            string result = StringHelper.CleanString(input);

            Assert.Equal(expected, result);
        }

        [Fact]
        public void CleanString_HandlesEmptyString()
        {
            string input = string.Empty;
            string expected = string.Empty;

            string result = StringHelper.CleanString(input);

            Assert.Equal(expected, result);
        }

        [Fact]
        public void CleanString_HandlesNullString()
        {
            string? input = null;
            string expected = string.Empty;

            string result = StringHelper.CleanString(input!);

            Assert.Equal(expected, result);
        }

        [Fact]
        public void SanitizeInput_TruncatesLongStrings()
        {
            string input = new string('a', 300);
            int maxLength = 255;

            string result = StringHelper.SanitizeInput(input, maxLength);

            Assert.Equal(maxLength, result.Length);
        }

        [Fact]
        public void SanitizeInput_CleansAndTruncates()
        {
            string input = "Hello\x00World\x01" + new string('!', 250);
            int maxLength = 255;

            string result = StringHelper.SanitizeInput(input, maxLength);

            Assert.Equal(maxLength, result.Length);
            Assert.All(result.ToCharArray(), c => Assert.InRange(c, (char)32, (char)126));
            Assert.Contains("Hello", result);
            Assert.Contains("World", result);
        }
    }
}
