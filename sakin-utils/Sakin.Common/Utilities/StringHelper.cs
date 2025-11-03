using System.Text;

namespace Sakin.Common.Utilities
{
    public static class StringHelper
    {
        public static string CleanString(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return string.Empty;
            }

            var cleaned = new StringBuilder();
            foreach (var c in input)
            {
                if (c >= 32 && c <= 126)
                {
                    cleaned.Append(c);
                }
            }
            return cleaned.ToString();
        }

        public static string SanitizeInput(string input, int maxLength = 255)
        {
            if (string.IsNullOrEmpty(input))
            {
                return string.Empty;
            }

            var cleaned = CleanString(input);
            return cleaned.Length > maxLength ? cleaned[..maxLength] : cleaned;
        }
    }
}
