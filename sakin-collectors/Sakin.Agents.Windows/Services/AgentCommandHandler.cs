using Microsoft.Extensions.Logging;
using Sakin.Agents.Windows.Configuration;
using Sakin.Common.Models.SOAR;
using Sakin.Messaging.Producer;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.Json;

namespace Sakin.Agents.Windows.Services;

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
                "Handling agent command {Command} for agent {TargetAgentId} (CorrelationId: {CorrelationId})",
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
                "Agent command {Command} completed. Success: {Success} (CorrelationId: {CorrelationId})",
                request.Command,
                success,
                request.CorrelationId);
        }
        catch (Exception ex)
        {
            success = false;
            errorMessage = ex.Message;
            _logger.LogError(ex,
                "Failed to handle agent command {Command} (CorrelationId: {CorrelationId})",
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
            return (true, $"DRY RUN: Would block IP {ipAddress}", string.Empty);
        }

        try
        {
            var ruleName = $"SAKIN_Block_{ipAddress}";
            var powerShellScript = $@"
                try {{
                    New-NetFirewallRule -DisplayName '{ruleName}' -Direction Inbound -RemoteAddress '{ipAddress}' -Action Block -Enable True
                    Write-Output 'Successfully created firewall rule to block {ipAddress}'
                }} catch {{
                    Write-Error ""Failed to create firewall rule: $($_.Exception.Message)""
                    exit 1
                }}";

            var result = await ExecutePowerShellAsync(powerShellScript, cancellationToken);
            return result;
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
            return (true, $"DRY RUN: Would unblock IP {ipAddress}", string.Empty);
        }

        try
        {
            var ruleName = $"SAKIN_Block_{ipAddress}";
            var powerShellScript = $@"
                try {{
                    Remove-NetFirewallRule -DisplayName '{ruleName}' -ErrorAction SilentlyContinue
                    Write-Output 'Successfully removed firewall rule for {ipAddress}'
                }} catch {{
                    Write-Output 'No existing firewall rule found for {ipAddress}'
                }}";

            var result = await ExecutePowerShellAsync(powerShellScript, cancellationToken);
            return result;
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
            return (true, "DRY RUN: Would isolate host by disabling network adapters", string.Empty);
        }

        try
        {
            var powerShellScript = @"
                try {
                    $adapters = Get-NetAdapter | Where-Object {$_.Status -eq 'Up' -and $_.Name -notlike '*Loopback*'}
                    foreach ($adapter in $adapters) {
                        Disable-NetAdapter -Name $adapter.Name -Confirm:$false
                        Write-Output ""Disabled network adapter: $($adapter.Name)""
                    }
                    Write-Output 'Host isolation completed'
                } catch {
                    Write-Error ""Failed to isolate host: $($_.Exception.Message)""
                    exit 1
                }";

            var result = await ExecutePowerShellAsync(powerShellScript, cancellationToken);
            return result;
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
            var scriptContent = await File.ReadAllTextAsync(scriptPath, cancellationToken);
            var result = await ExecutePowerShellAsync(scriptContent, cancellationToken);
            return result;
        }
        catch (Exception ex)
        {
            return (false, string.Empty, $"Failed to execute script '{payload}': {ex.Message}");
        }
    }

    private async Task<(bool Success, string Output, string Error)> ExecutePowerShellAsync(string script, CancellationToken cancellationToken)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-ExecutionPolicy Bypass -Command -",
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = startInfo };
            process.Start();

            await process.StandardInput.WriteAsync(script);
            process.StandardInput.Close();

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync(cancellationToken);

            var success = process.ExitCode == 0;
            return (success, output, error);
        }
        catch (Exception ex)
        {
            return (false, string.Empty, $"PowerShell execution failed: {ex.Message}");
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