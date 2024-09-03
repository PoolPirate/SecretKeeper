using Cocona;
using Microsoft.Extensions.Logging;
using SecretKeeper.Models;
using SecretKeeper.Services;
using System.Runtime.InteropServices;

namespace SecretKeeper.Commands;
public class StartCommand
{
    private readonly SecretdService _secretdService;
    private readonly SystemdService _systemdService;
    private readonly LogWatcherService _logWatcherService;
    private readonly ILogger _logger;
    private readonly NotifierService _notifierService;

    private string _nodeService = null!;

    public StartCommand(SecretdService secretdService, SystemdService systemdService, LogWatcherService logWatcherService, 
        ILogger<StartCommand> logger, NotifierService notifierService)
    {
        _secretdService = secretdService;
        _systemdService = systemdService;
        _logWatcherService = logWatcherService;
        _logger = logger;
        _notifierService = notifierService;
    }

    [Command("start")]
    public async Task StartAsync(string secretd, string service, string? secretdHome = null, string? webhookUrl = null, ulong? discordUserId = null, int maxSecondsWithoutBlock = 100)
    {
        _nodeService = service;
        _secretdService.Initialize(secretd, secretdHome);
        if (webhookUrl is not null)
        {
            _notifierService.InitializeWebhookUrl(webhookUrl, discordUserId);
        }

        _logger.LogInformation("Starting to watch node...");

        bool isProcessingNodeFailure = false;

        _logWatcherService.OnConsensusFailure += (type, message) =>
        {
            _logger.LogDebug("Starting to process consensus failure");
            isProcessingNodeFailure = true;
            _ = HandleConsensusFailureAsync(type, message).ContinueWith(async handleTask =>
            {
                int extraDelay = handleTask.Result switch
                {
                    ConsensusFailureType.INVALID_APPHASH => 120000,
                    _ => 0
                };
                await Task.Delay(extraDelay);

                isProcessingNodeFailure = false;
                _logger.LogDebug("Finished processing consensus failure");
            });
        };

        _logWatcherService.OnNewBlockHeight += HandleNewBlock;

        _logWatcherService.StartWatching(service);

        var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));

        bool hasSent = false;

        while (await timer.WaitForNextTickAsync())
        {
            if(isProcessingNodeFailure)
            {
                continue;
            }

            var blockDelay = DateTimeOffset.UtcNow - _logWatcherService.LastBlockSeenAt;
            if (blockDelay.TotalSeconds > maxSecondsWithoutBlock)
            {
                _logger.LogWarning("No new block seen for {seconds} seconds", Math.Round(blockDelay.TotalSeconds, 1));

                if (!hasSent)
                {
                    await _notifierService.SendNotificationAsync($"Node Desync", $"No new block processed in {Math.Round(blockDelay.TotalSeconds, 1)} seconds", true);
                    hasSent = true;
                }
            }
            else if (hasSent)
            {
                await _notifierService.SendNotificationAsync($"Issue Resolved", "New block has been processed", false);
                hasSent = false;
            }
        }
    }

    private async Task<ConsensusFailureType> HandleConsensusFailureAsync(ConsensusFailureType type, string message)
    {
        _logger.LogWarning("Consensus failure occured: {type}", type);

        switch (type)
        {
            case ConsensusFailureType.SGX_ERROR_BUSY:
                await _notifierService.SendNotificationAsync("SGX Busy Consensus Failure", "Restarting node...", false);
                await RestartNodeAsync();
                break;
            case ConsensusFailureType.INVALID_APPHASH:
                await _notifierService.SendNotificationAsync("Invalid AppHash Consensus Failure", "Rollbacking and restarting node...", false);
                await RollbackAndRestartAsync();
                break;
            case ConsensusFailureType.SOFTWARE_UPGRADE:
                await _notifierService.SendNotificationAsync("REQUIRE SOFTWARE UPGRADE", message, true);
                break;
            default:
                await _notifierService.SendNotificationAsync("Unhandled Consensus Failure", message, true);
                break;
        }

        return type;
    }

    private async Task RestartNodeAsync()
    {
        _logger.LogInformation("Restarting node...");

        try
        {
            await _systemdService.StopService(_nodeService);
            await Task.Delay(2000);
            await _systemdService.StartService(_nodeService);
        }
        catch(Exception ex)
        {
            _logger.LogCritical(ex, "Exception occured while restarting node");
            await _notifierService.SendNotificationAsync("Restarting Node Failed", ex.Message, true);
        }
    }

    private async Task RollbackAndRestartAsync()
    {
        _logger.LogInformation("Rollbacking and restarting node...");

        try
        {
            await _systemdService.StopService(_nodeService);
            await Task.Delay(5000);
            await _secretdService.RollbackOneAsync();
            await Task.Delay(5000);
            await _systemdService.StartService(_nodeService);
        }
        catch(Exception ex)
        {
            _logger.LogCritical(ex, "Exception occured while rollbacking and restarting node");
            await _notifierService.SendNotificationAsync("Rollback and Restart failed", ex.Message, true);
        }
    }

    private void HandleNewBlock(ulong blockHeight)
    {
        if (blockHeight % 10 == 0)
        {
            _logger.LogInformation("Processed blocks {from} - {to}", blockHeight - 10, blockHeight);
        }
    }
}
