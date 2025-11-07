namespace Sakin.Common.Validation;

public class ValidationOptions
{
    public const string SectionName = "Validation";
    
    public int MaxEventSizeBytes { get; set; } = 65536; // 64KB
    public int MaxRegexTimeoutMs { get; set; } = 100;
    public bool EnforceUtf8 { get; set; } = true;
    public bool AllowControlCharacters { get; set; } = false;
    public int MaxFieldLength { get; set; } = 10000;
}
