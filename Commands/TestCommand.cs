using Cocona;
using Microsoft.Extensions.Logging;
using SecretKeeper.Services;

namespace SecretKeeper.Commands;
public class TestCommand
{
    private readonly SystemdService _systemdService;
    private readonly SecretdService _secretdService;
    private readonly ILogger _logger;

    public TestCommand(SystemdService systemdService, ILogger<TestCommand> logger, SecretdService secretdService)
    {
        _systemdService = systemdService;
        _logger = logger;
        _secretdService = secretdService;
    }

    [Command("test")]
    public async Task TestAsync(string secretd, string service, string? secretdHome = null)
    {
        _secretdService.Initialize(secretd, secretdHome);

        _logger.LogInformation("Stopping {service}", service);
        await _systemdService.StopService(service);

        await Task.Delay(10000);

        _logger.LogInformation("Rollbacking Node");
        await _secretdService.RollbackOneAsync();

        await Task.Delay(10000);

        _logger.LogInformation("Starting {service}", service);
        await _systemdService.StartService(service);
        await Task.Delay(10000);

        await foreach(var log in _systemdService.GetLogsAsync(service, default))
        {
            _logger.LogInformation("{message}", log);
        }
    }
}
