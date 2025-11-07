using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace Sakin.Common.Validation;

public class InputValidator
{
    private readonly ValidationOptions _options;

    public InputValidator(IOptions<ValidationOptions> options)
    {
        _options = options.Value;
    }

    public bool ValidateEventSize(string payload)
    {
        var sizeBytes = Encoding.UTF8.GetByteCount(payload);
        return sizeBytes <= _options.MaxEventSizeBytes;
    }

    public bool ValidateUtf8(string input)
    {
        if (!_options.EnforceUtf8)
        {
            return true;
        }

        try
        {
            var bytes = Encoding.UTF8.GetBytes(input);
            var decoded = Encoding.UTF8.GetString(bytes);
            return decoded == input;
        }
        catch
        {
            return false;
        }
    }

    public bool ValidateNoControlCharacters(string input)
    {
        if (_options.AllowControlCharacters)
        {
            return true;
        }

        foreach (var c in input)
        {
            if (char.IsControl(c) && c != '\n' && c != '\r' && c != '\t')
            {
                return false;
            }
        }
        return true;
    }

    public bool ValidateFieldLength(string input)
    {
        return input.Length <= _options.MaxFieldLength;
    }

    public bool ValidateRegexSafe(string pattern)
    {
        try
        {
            var timeout = TimeSpan.FromMilliseconds(_options.MaxRegexTimeoutMs);
            _ = new Regex(pattern, RegexOptions.None, timeout);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public string SanitizeInput(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        var sanitized = new StringBuilder(input.Length);
        foreach (var c in input)
        {
            if (char.IsControl(c) && c != '\n' && c != '\r' && c != '\t')
            {
                continue;
            }
            sanitized.Append(c);
        }

        var result = sanitized.ToString();
        if (result.Length > _options.MaxFieldLength)
        {
            result = result[.._options.MaxFieldLength];
        }

        return result;
    }

    public string MaskPii(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        var emailPattern = new Regex(@"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b", 
            RegexOptions.Compiled, 
            TimeSpan.FromMilliseconds(100));
        
        return emailPattern.Replace(input, match =>
        {
            var email = match.Value;
            var parts = email.Split('@');
            if (parts.Length != 2)
            {
                return email;
            }

            var localPart = parts[0];
            var domain = parts[1];
            
            var maskedLocal = localPart.Length > 2 
                ? $"{localPart[0]}***{localPart[^1]}" 
                : "***";
            
            var domainParts = domain.Split('.');
            var maskedDomain = domainParts.Length > 1
                ? $"{domainParts[0][0]}***@***{domainParts[^1]}"
                : "***";
            
            return $"{maskedLocal}@{maskedDomain}";
        });
    }
}
