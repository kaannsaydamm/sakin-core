using Microsoft.Extensions.Logging;

namespace Sakin.Common.Logging
{
    public static class LoggerExtensions
    {
        public static void LogPacketCapture(this ILogger logger, string sourceIp, string destinationIp, string protocol)
        {
            logger.LogDebug("Packet captured: {SourceIp} -> {DestinationIp} [{Protocol}]", sourceIp, destinationIp, protocol);
        }

        public static void LogSniCapture(this ILogger logger, string sni, string sourceIp, string destinationIp)
        {
            logger.LogInformation("SNI captured: {Sni} from {SourceIp} to {DestinationIp}", sni, sourceIp, destinationIp);
        }

        public static void LogDatabaseError(this ILogger logger, Exception exception, string operation)
        {
            logger.LogError(exception, "Database operation failed: {Operation}", operation);
        }

        public static void LogNetworkInterfaceDetected(this ILogger logger, string interfaceName, string description)
        {
            logger.LogInformation("Network interface detected: {InterfaceName} - {Description}", interfaceName, description);
        }

        public static void LogNetworkInterfaceSkipped(this ILogger logger, string interfaceName, string reason)
        {
            logger.LogInformation("Skipping network interface {InterfaceName}: {Reason}", interfaceName, reason);
        }
    }
}
