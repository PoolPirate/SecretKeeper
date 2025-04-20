using Microsoft.Extensions.Logging;
using SecretKeeper.Models;
using System.Text.RegularExpressions;

namespace SecretKeeper.Services;
public partial class LogWatcherService
{
    private readonly SystemdService _systemdService;
    private readonly ILogger _logger;

    public DateTimeOffset LastBlockSeenAt;

    public event Action<ulong>? OnNewBlockHeight;
    public event Action<NodeFailureType, string>? OnNodeFailure;

    public LogWatcherService(SystemdService systemdService, ILogger<LogWatcherService> logger)
    {
        _systemdService = systemdService;
        _logger = logger;
    }

    public void StartWatching(string service) 
        => _ = Task.Run(async () =>
        {
            LastBlockSeenAt = DateTimeOffset.UtcNow;

            try
            {
                var logs = _systemdService.GetLogsAsync(service, default);

                await foreach(var log in logs)
                {
                    ProcessLog(log);
                }
            }
            catch(Exception ex)
            {
                _logger.LogCritical(ex, "LogWatcher encountered an exception");
            }
        });

    private void ProcessLog(string message)
    {
        var blockMatch = BlockProcessedRegex().Match(message);

        if (blockMatch.Success)
        {
            LastBlockSeenAt = DateTimeOffset.UtcNow;
            OnNewBlockHeight?.Invoke(ulong.Parse(blockMatch.Groups[1].ValueSpan));
            return;
        }

        var consensusFailureMatch = ConsensusFailureMessage().Match(message);

        if (consensusFailureMatch.Success)
        {
            HandleConsensusFailure(consensusFailureMatch.Groups[1].Value);
            return;
        }

        var consensusFailureCodeMatch = ConsensusFailureCode().Match(message);

        if (consensusFailureCodeMatch.Success)
        {
            HandleConsensusFailure(consensusFailureCodeMatch.Groups[1].Value);
            return;
        }

        var panicMatch = PanicRegex().Match(message);

        if (panicMatch.Success)
        {
            HandlePanic(panicMatch.Groups[1].Value);
            return;
        }
    }

    private void HandleConsensusFailure(string message)
    {
        if (message.Contains("SGX_ERROR_BUSY") || message.Contains("error submitting validator set to enclave"))
        {
            OnNodeFailure?.Invoke(NodeFailureType.SGX_ERROR_BUSY, message);
        }
        else if(message.Contains("SGX_ERROR_ENCLAVE_CRASHED"))
        {
            OnNodeFailure?.Invoke(NodeFailureType.SGX_ERROR_ENCLAVE_CRASHED, message);
        }
        else if (message.Contains("wrong Block.Header.AppHash") 
            || message.Contains("wrong Block.Header.LastResultsHash")
            || message.Contains("invalid proof for encrypted random"))
        {
            OnNodeFailure?.Invoke(NodeFailureType.INVALID_APPHASH, message);
        }
        else if (message.Contains("UPGRADE"))
        {
            OnNodeFailure?.Invoke(NodeFailureType.SOFTWARE_UPGRADE, message);
        }
        else
        {
            OnNodeFailure?.Invoke(NodeFailureType.UNKNOWN_CONSENSUS_FAILURE, message);
        } 
    }

    private void HandlePanic(string message)
    {
        if (message.Contains("couldn't find validators"))
        {
            OnNodeFailure?.Invoke(NodeFailureType.VALIDATORS_NOT_FOUND, message);
        }
        else
        {
            OnNodeFailure?.Invoke(NodeFailureType.UNKNOWN_PANIC, message);
        }
    }

    [GeneratedRegex("executed block app_hash=\\w{64} height=(\\d+)")]
    private static partial Regex BlockProcessedRegex();

    [GeneratedRegex("CONSENSUS FAILURE!!! err=(\"[^\"]*\")")]
    private static partial Regex ConsensusFailureMessage();

    [GeneratedRegex("CONSENSUS FAILURE!!! err=(\\S*)")]
    private static partial Regex ConsensusFailureCode();

    [GeneratedRegex("panic: (.*):")]
    private static partial Regex PanicRegex();
}
