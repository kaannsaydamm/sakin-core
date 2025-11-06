using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sakin.Syslog.Configuration;
using Sakin.Syslog.Messaging;
using Sakin.Syslog.Models;
using Sakin.Syslog.Services;

namespace Sakin.Syslog.Services
{
    public class SyslogListenerService : BackgroundService
    {
        private readonly SyslogOptions _options;
        private readonly ILogger<SyslogListenerService> _logger;
        private readonly ISyslogPublisher _publisher;
        private readonly SyslogParser _parser;
        private CancellationToken _stoppingToken;
        
        private UdpClient? _udpClient;
        private TcpListener? _tcpListener;
        private List<Task> _tcpTasks = new();
        
        public SyslogListenerService(
            IOptions<SyslogOptions> options,
            ILogger<SyslogListenerService> logger,
            ISyslogPublisher publisher,
            SyslogParser parser)
        {
            _options = options.Value;
            _logger = logger;
            _publisher = publisher;
            _parser = parser;
        }
        
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _stoppingToken = stoppingToken;
            
            _logger.LogInformation(
                "Starting Syslog listener. UDP Port={UdpPort}, TCP Port={TcpPort}, TCP Enabled={TcpEnabled}, BufferSize={BufferSize}",
                _options.UdpPort,
                _options.TcpPort,
                _options.TcpEnabled,
                _options.BufferSize);
            
            var tasks = new List<Task>();
            
            // Start UDP listener
            tasks.Add(Task.Run(() => StartUdpListener(stoppingToken), stoppingToken));
            
            // Start TCP listener if enabled
            if (_options.TcpEnabled)
            {
                tasks.Add(Task.Run(() => StartTcpListener(stoppingToken), stoppingToken));
            }
            
            try
            {
                await Task.WhenAll(tasks).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Syslog listener service stopping");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in syslog listener service");
            }
        }
        
        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping Syslog listener service");
            
            // Stop UDP client
            _udpClient?.Dispose();
            
            // Stop TCP listener
            _tcpListener?.Stop();
            
            // Wait for TCP tasks to complete
            try
            {
                await Task.WhenAll(_tcpTasks).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error waiting for TCP tasks to complete");
            }
            
            // Flush any pending messages
            await _publisher.FlushAsync(cancellationToken).ConfigureAwait(false);
            
            await base.StopAsync(cancellationToken).ConfigureAwait(false);
        }
        
        private async Task StartUdpListener(CancellationToken cancellationToken)
        {
            try
            {
                _udpClient = new UdpClient(_options.UdpPort);
                _logger.LogInformation("UDP listener started on port {Port}", _options.UdpPort);
                
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        var result = await _udpClient.ReceiveAsync().ConfigureAwait(false);
                        _ = Task.Run(() => ProcessUdpMessage(result, cancellationToken), cancellationToken);
                    }
                    catch (ObjectDisposedException)
                    {
                        // Expected when stopping
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error receiving UDP message");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start UDP listener on port {Port}", _options.UdpPort);
            }
            finally
            {
                _udpClient?.Dispose();
            }
        }
        
        private async Task StartTcpListener(CancellationToken cancellationToken)
        {
            try
            {
                _tcpListener = new TcpListener(IPAddress.Any, _options.TcpPort);
                _tcpListener.Start();
                _logger.LogInformation("TCP listener started on port {Port}", _options.TcpPort);
                
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        var tcpClient = await _tcpListener.AcceptTcpClientAsync().ConfigureAwait(false);
                        var tcpTask = Task.Run(() => HandleTcpClient(tcpClient, cancellationToken), cancellationToken);
                        
                        // Clean up completed tasks
                        _tcpTasks.RemoveAll(t => t.IsCompleted);
                        _tcpTasks.Add(tcpTask);
                    }
                    catch (ObjectDisposedException)
                    {
                        // Expected when stopping
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error accepting TCP connection");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start TCP listener on port {Port}", _options.TcpPort);
            }
            finally
            {
                _tcpListener?.Stop();
            }
        }
        
        private async Task ProcessUdpMessage(UdpReceiveResult result, CancellationToken cancellationToken)
        {
            try
            {
                var rawMessage = Encoding.UTF8.GetString(result.Buffer);
                var remoteEndpoint = result.RemoteEndPoint.ToString();
                
                await ProcessMessage(rawMessage, remoteEndpoint, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing UDP message from {Endpoint}", result.RemoteEndPoint);
            }
        }
        
        private async Task HandleTcpClient(TcpClient tcpClient, CancellationToken cancellationToken)
        {
            var remoteEndpoint = tcpClient.Client.RemoteEndPoint?.ToString() ?? "unknown";
            
            try
            {
                using var stream = tcpClient.GetStream();
                using var reader = new StreamReader(stream, Encoding.UTF8);
                
                while (!cancellationToken.IsCancellationRequested && tcpClient.Connected)
                {
                    try
                    {
                        var rawMessage = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                        
                        if (rawMessage == null)
                        {
                            break; // Client disconnected
                        }
                        
                        await ProcessMessage(rawMessage, remoteEndpoint, cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error reading from TCP client {Endpoint}", remoteEndpoint);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling TCP client {Endpoint}", remoteEndpoint);
            }
            finally
            {
                tcpClient.Dispose();
            }
        }
        
        private async Task ProcessMessage(string rawMessage, string remoteEndpoint, CancellationToken cancellationToken)
        {
            try
            {
                // Trim whitespace and newlines
                rawMessage = rawMessage.Trim();
                
                if (string.IsNullOrEmpty(rawMessage))
                {
                    return;
                }
                
                var syslogMessage = _parser.Parse(rawMessage, remoteEndpoint);
                
                await _publisher.PublishAsync(syslogMessage, cancellationToken).ConfigureAwait(false);
                
                _logger.LogDebug(
                    "Processed syslog message from {Endpoint}: {Hostname} {Tag} {Message}",
                    remoteEndpoint,
                    syslogMessage.Hostname,
                    syslogMessage.Tag,
                    syslogMessage.Message.Truncate(100));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing syslog message from {Endpoint}: {Message}", remoteEndpoint, rawMessage.Truncate(200));
            }
        }
    }
    
    internal static class StringExtensions
    {
        public static string Truncate(this string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value;
            }
            
            return value.Length > maxLength ? value.Substring(0, maxLength) + "..." : value;
        }
    }
}