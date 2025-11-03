namespace Sakin.Common.Models
{
    public enum EventType
    {
        Unknown = 0,
        NetworkTraffic = 1,
        DnsQuery = 2,
        HttpRequest = 3,
        TlsHandshake = 4,
        SshConnection = 5,
        FileAccess = 6,
        ProcessExecution = 7,
        AuthenticationAttempt = 8,
        SystemLog = 9,
        SecurityAlert = 10
    }
}
