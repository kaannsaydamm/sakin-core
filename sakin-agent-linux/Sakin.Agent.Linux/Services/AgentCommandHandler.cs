using Microsoft.Extensions.Logging;
using Sakin.Agent.Linux.Configuration;
using Sakin.Common.Models.SOAR;
using Sakin.Messaging.Producer;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Sakin.Agent.Linux.Services;

public interface IAgentCommandHandler
{
    Task<AgentCommandResult> HandleCommandAsync(AgentCommandRequest request, CancellationToken cancellationToken = default);
}

public class AgentCommandHandler : IAgentCommandHandler
{
    private readonly AgentOptions _options;
    private readonly IKafkaProducer _kafkaProducer;
    private readonly ILogger<AgentCommandHandler> _logger;
    private readonly SoarKafkaTopics _kafkaTopics;

    public AgentCommandHandler(
        AgentOptions options,
        IKafkaProducer kafkaProducer,
        Microsoft.Extensions.Options.IOptions<SoarKafkaTopics> kafkaTopicsOptions,
        ILogger<AgentCommandHandler> logger)
    {
        _options = options;
        _kafkaProducer = kafkaProducer;
        _kafkaTopics = kafkaTopicsOptions.Value;
        _logger = logger;
    }

    public async Task<AgentCommandResult> HandleCommandAsync(AgentCommandRequest request, CancellationToken cancellationToken = default)
    {
        var startedAt = DateTime.UtcNow;
        var output = new StringBuilder();
        var success = false;
        var errorMessage = string.Empty;

        try
        {
            _logger.LogInformation(
                "Handling Linux agent command {Command} for agent {TargetAgentId} (CorrelationId: {CorrelationId})",
                request.Command,
                request.TargetAgentId,
                request.CorrelationId);

            // Validate command is for this agent
            if (request.TargetAgentId != _options.AgentId)
            {
                errorMessage = $"Command target agent '{request.TargetAgentId}' does not match this agent '{_options.AgentId}'";
                _logger.LogWarning(errorMessage);
                return new AgentCommandResult(request.CorrelationId, _options.AgentId, false, errorMessage, startedAt);
            }

            // Check command expiration
            if (request.ExpireAtUtc < DateTime.UtcNow)
            {
                errorMessage = $"Command expired at {request.ExpireAtUtc}";
                _logger.LogWarning(errorMessage);
                return new AgentCommandResult(request.CorrelationId, _options.AgentId, false, errorMessage, startedAt);
            }

            // Execute command based on type
            var (cmdSuccess, cmdOutput, cmdError) = await ExecuteSecureCommandAsync(request, cancellationToken);
            success = cmdSuccess;
            output.Append(cmdOutput);
            errorMessage = cmdError;

            _logger.LogInformation(
                "Linux agent command {Command} completed. Success: {Success} (CorrelationId: {CorrelationId})",
                request.Command,
                success,
                request.CorrelationId);
        }
        catch (Exception ex)
        {
            success = false;
            errorMessage = ex.Message;
            _logger.LogError(ex,
                "Failed to handle Linux agent command {Command} (CorrelationId: {CorrelationId})",
                request.Command,
                request.CorrelationId);
        }

        var result = new AgentCommandResult(request.CorrelationId, _options.AgentId, success, output.ToString(), startedAt);

        // Publish result back to Kafka
        await PublishCommandResultAsync(result, cancellationToken);

        return result;
    }

    private async Task<(bool Success, string Output, string Error)> ExecuteSecureCommandAsync(AgentCommandRequest request, CancellationToken cancellationToken)
    {
        switch (request.Command)
        {
            case AgentCommandType.BlockIp:
                return await ExecuteBlockIpCommandAsync(request.Payload, cancellationToken);

            case AgentCommandType.UnblockIp:
                return await ExecuteUnblockIpCommandAsync(request.Payload, cancellationToken);

            case AgentCommandType.IsolateHost:
                return await ExecuteIsolateHostCommandAsync(cancellationToken);

            case AgentCommandType.RunAllowlistedScript:
                return await ExecuteAllowlistedScriptCommandAsync(request.Payload, cancellationToken);

            default:
                return (false, string.Empty, $"Unknown command type: {request.Command}");
        }
    }

    private async Task<(bool Success, string Output, string Error)> ExecuteBlockIpCommandAsync(string payload, CancellationToken cancellationToken)
    {
        if (!System.Net.IPAddress.TryParse(payload, out var ipAddress))
        {
            return (false, string.Empty, $"Invalid IP address: {payload}");
        }

        if (_options.DryRun)
        {
            return (true, $"DRY RUN: Would block IP {ipAddress} using iptables", string.Empty);
        }

        try
        {
            var isIPv6 = ipAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6;
            var iptablesPath = isIPv6 ? _options.Ip6tablesPath : _options.IptablesPath;
            
            var rule = $"-s {ipAddress} -j DROP";
            var command = $"{iptablesPath} -I INPUT {rule}";

            var result = await ExecuteShellCommandAsync(command, cancellationToken);
            if (result.Success)
            {
                // Persist the rule
                await PersistIptablesRuleAsync(isIPv6, cancellationToken);
                return (true, $"Successfully blocked IP {ipAddress} and persisted rule", string.Empty);
            }
            else
            {
                return result;
            }
        }
        catch (Exception ex)
        {
            return (false, string.Empty, $"Failed to block IP {ipAddress}: {ex.Message}");
        }
    }

    private async Task<(bool Success, string Output, string Error)> ExecuteUnblockIpCommandAsync(string payload, CancellationToken cancellationToken)
    {
        if (!System.Net.IPAddress.TryParse(payload, out var ipAddress))
        {
            return (false, string.Empty, $"Invalid IP address: {payload}");
        }

        if (_options.DryRun)
        {
            return (true, $"DRY RUN: Would unblock IP {ipAddress} using iptables", string.Empty);
        }

        try
        {
            var isIPv6 = ipAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6;
            var iptablesPath = isIPv6 ? _options.Ip6tablesPath : _options.IptablesPath;
            
            var rule = $"-s {ipAddress} -j DROP";
            var command = $"{iptablesPath} -D INPUT {rule}";

            var result = await ExecuteShellCommandAsync(command, cancellationToken);
            if (result.Success)
            {
                // Persist the rule
                await PersistIptablesRuleAsync(isIPv6, cancellationToken);
                return (true, $"Successfully unblocked IP {ipAddress} and persisted rule", string.Empty);
            }
            else
            {
                return result;
            }
        }
        catch (Exception ex)
        {
            return (false, string.Empty, $"Failed to unblock IP {ipAddress}: {ex.Message}");
        }
    }

    private async Task<(bool Success, string Output, string Error)> ExecuteIsolateHostCommandAsync(CancellationToken cancellationToken)
    {
        if (_options.DryRun)
        {
            return (true, "DRY RUN: Would isolate host by blocking all network traffic", string.Empty);
        }

        try
        {
            // Block all incoming traffic except loopback and established connections
            var commands = new[]
            {
                $"{_options.IptablesPath} -P INPUT DROP",
                $"{_options.IptablesPath} -A INPUT -i lo -j ACCEPT",
                $"{_options.IptablesPath} -A INPUT -m conntrack --ctstate ESTABLISHED,RELATED -j ACCEPT"
            };

            foreach (var command in commands)
            {
                var result = await ExecuteShellCommandAsync(command, cancellationToken);
                if (!result.Success)
                {
                    return result;
                }
            }

            // Persist the rules
            await PersistIptablesRuleAsync(false, cancellationToken);

            return (true, "Successfully isolated host by blocking network traffic", string.Empty);
        }
        catch (Exception ex)
        {
            return (false, string.Empty, $"Failed to isolate host: {ex.Message}");
        }
    }

    private async Task<(bool Success, string Output, string Error)> ExecuteAllowlistedScriptCommandAsync(string payload, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return (false, string.Empty, "Script name is required");
        }

        // Check if script is allowlisted
        if (!_options.AllowlistedScripts.Contains(payload))
        {
            return (false, string.Empty, $"Script '{payload}' is not in the allowlist");
        }

        var scriptPath = Path.Combine(_options.ScriptsDirectory, payload);
        if (!File.Exists(scriptPath))
        {
            return (false, string.Empty, $"Script file not found: {scriptPath}");
        }

        if (_options.DryRun)
        {
            return (true, $"DRY RUN: Would execute allowlisted script: {scriptPath}", string.Empty);
        }

        try
        {
            // Make script executable
            var chmodCommand = $"/bin/chmod +x {scriptPath}";
            var chmodResult = await ExecuteShellCommandAsync(chmodCommand, cancellationToken);
            if (!chmodResult.Success)
            {
                return (false, string.Empty, $"Failed to make script executable: {chmodResult.Error}");
            }

            // Execute the script
            var command = $"{scriptPath}";
            var result = await ExecuteShellCommandAsync(command, cancellationToken);
            return result;
        }
        catch (Exception ex)
        {
            return (false, string.Empty, $"Failed to execute script '{payload}': {ex.Message}");
        }
    }

    private async Task<(bool Success, string Output, string Error)> ExecuteShellCommandAsync(string command, CancellationToken cancellationToken)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "/bin/bash",
                Arguments = $"-c \"{command}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = startInfo };
            process.Start();

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync(cancellationToken);

            var success = process.ExitCode == 0;
            return (success, output, error);
        }
        catch (Exception ex)
        {
            return (false, string.Empty, $"Shell command execution failed: {ex.Message}");
        }
    }

    private async Task PersistIptablesRuleAsync(bool isIPv6, CancellationToken cancellationToken)
    {
        try
        {
            var saveCommand = isIPv6 
                ? $"{_options.Ip6tablesPath}-save > /etc/iptables/rules.v6"
                : $"{_options.IptablesSavePath} > /etc/iptables/rules.v4";

            await ExecuteShellCommandAsync(saveCommand, cancellationToken);
            _logger.LogDebug("Persisted iptables rules ({IsIPv6})", isIPv6);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist iptables rules ({IsIPv6})", isIPv6);
        }
    }

    private async Task PublishCommandResultAsync(AgentCommandResult result, CancellationToken cancellationToken)
    {
        try
        {
            var resultJson = JsonSerializer.Serialize(result);

            await _kafkaProducer.ProduceAsync(
                _kafkaTopics.AgentResult,
                result.CorrelationId.ToString(),
                resultJson,
                cancellationToken);

            _logger.LogDebug(
                "Published command result for correlation {CorrelationId}",
                result.CorrelationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to publish command result for correlation {CorrelationId}",
                result.CorrelationId);
        }
    }
}