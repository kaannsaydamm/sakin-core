namespace Sakin.Common.Security;

public class TlsOptions
{
    public const string SectionName = "TLS";
    
    public bool Enabled { get; set; } = false;
    public string CertificatePath { get; set; } = "/secrets/certs";
    public string CertificateFileName { get; set; } = "service.crt";
    public string PrivateKeyFileName { get; set; } = "service.key";
    public string CaCertificateFileName { get; set; } = "ca.crt";
    public bool VerifyServerCertificate { get; set; } = true;
    public CertificateValidationMode CertificateValidationMode { get; set; } = CertificateValidationMode.RequireValidCertificate;
}

public enum CertificateValidationMode
{
    None,
    RequireValidCertificate,
    RequireValidCertificateWithPinning
}
