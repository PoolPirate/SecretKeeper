using Cocona;
using Microsoft.Extensions.Logging;
using SecretKeeper.Models;
using SecretKeeper.Services;

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
    public async Task StartAsync(string secretd, string service, string? webhookUrl = null, ulong? discordUserId = null, int maxSecondsWithoutBlock = 100)
    {
        _nodeService = service;
        _secretdService.Initialize(secretd);
        if (webhookUrl is not null)
        {
            _notifierService.InitializeWebhookUrl(webhookUrl, discordUserId);
        }

        _logger.LogInformation("Starting to watch node...");

        _logWatcherService.OnConsensusFailure += HandleConsensusFailure;
        _logWatcherService.OnNewBlockHeight += HandleNewBlock;

        _logWatcherService.StartWatching(service);

        var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));

        bool hasSent = false;

        while (await timer.WaitForNextTickAsync())
        {
            var blockDelay = DateTimeOffset.UtcNow - _logWatcherService.LastBlockSeenAt;
            if (blockDelay.TotalSeconds > maxSecondsWithoutBlock)
            {
                _logger.LogWarning("No new block seen for {seconds} seconds", Math.Round(blockDelay.TotalSeconds, 1));

                if (!hasSent)
                {
                    await _notifierService.SendNotificationAsync($"Node Desync", $"No new block processed in {Math.Round(blockDelay.TotalSeconds, 1)} seconds");
                    hasSent = true;
                }
            }
            else if (hasSent)
            {
                await _notifierService.SendNotificationAsync($"Issue Resolved", "New block has been processed");
                hasSent = false;
            }
        }
    }

    private void HandleConsensusFailure(ConsensusFailureType type, string message)
    {
        _logger.LogWarning("Consensus failure occured: {type}", type);

        switch (type)
        {
            case ConsensusFailureType.SGX_ERROR_BUSY:
                Task.Run(RestartNodeAsync);
                break;
            case ConsensusFailureType.INVALID_APPHASH:
                Task.Run(RollbackAndRestartAsync);
                break;
            default:
                Task.Run(() => _notifierService.SendNotificationAsync("Unhandled Consensus Failure", message));
                break;
        }
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
            await _notifierService.SendNotificationAsync("Restarting Node Failed", ex.Message);
        }
    }

    private async Task RollbackAndRestartAsync()
    {
        _logger.LogInformation("Rollbacking and restarting node...");

        try
        {
            await _systemdService.StopService(_nodeService);
            await Task.Delay(2000);
            await _secretdService.RollbackOneAsync();
            await Task.Delay(2000);
            await _systemdService.StartService(_nodeService);
        }
        catch(Exception ex)
        {
            _logger.LogCritical(ex, "Exception occured while rollbacking and restarting node");
            await _notifierService.SendNotificationAsync("Rollback and Restart failed", ex.Message);
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
