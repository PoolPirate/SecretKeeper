using CliWrap;
using CliWrap.EventStream;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;

namespace SecretKeeper.Services;
public class SystemdService
{
    private readonly ILogger _logger;

    public SystemdService(ILogger<SystemdService> logger)
    {
        _logger = logger;
    }

    public async Task StartService(string name)
    {
        _logger.LogDebug("Starting service {name}", name);
        await Cli.Wrap("/sbin/service")
                .WithArguments([name, "start"])
                .ExecuteAsync();
    }

    public async Task StopService(string name)
    {
        _logger.LogDebug("Stopping service {name}", name);
        await Cli.Wrap("/sbin/service")
                .WithArguments([name, "stop"])
                .ExecuteAsync();
    }

    public async IAsyncEnumerable<string> GetLogsAsync(string name, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var output = Cli.Wrap("/bin/journalctl")
            .WithArguments(["-u", name, "-f"])
            .ListenAsync(cancellationToken);

        await foreach(var e  in output)
        {
            switch(e)
            {
                case StandardOutputCommandEvent std:
                    yield return std.Text;
                    break;
                default:
                    continue;
            }
        }
    }
}
