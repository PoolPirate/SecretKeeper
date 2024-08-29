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
    public event Action<ConsensusFailureType, string>? OnConsensusFailure;

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
    }

    private void HandleConsensusFailure(string message)
    {
        if (message.Contains("SGX_ERROR_BUSY"))
        {
            OnConsensusFailure?.Invoke(ConsensusFailureType.SGX_ERROR_BUSY, message);
        }
        else if (message.Contains("wrong Block.Header.AppHash") || message.Contains("wrong Block.Header.LastResultsHash"))
        {
            OnConsensusFailure?.Invoke(ConsensusFailureType.INVALID_APPHASH, message);
        }
        else if (message.Contains("UPGRADE"))
        {
            OnConsensusFailure?.Invoke(ConsensusFailureType.SOFTWARE_UPGRADE, message);
        }
        else
        {
            OnConsensusFailure?.Invoke(ConsensusFailureType.UNKNOWN, message);
        } 
    }

    [GeneratedRegex("executed block height=(\\d*)")]
    private static partial Regex BlockProcessedRegex();

    [GeneratedRegex("CONSENSUS FAILURE!!! err=(\"[^\"]*\")")]
    private static partial Regex ConsensusFailureMessage();

    [GeneratedRegex("CONSENSUS FAILURE!!! err=(\\S*)")]
    private static partial Regex ConsensusFailureCode();
}
