using CliWrap;
using CliWrap.Buffered;
using Microsoft.Extensions.Logging;

namespace SecretKeeper.Services;
public class SecretdService
{
    private string _secretdPath = "";
    private string? _secretdHome = null;

    public void Initialize(string secretdPath, string? secretdHome)
    {
        _secretdPath = secretdPath;
        _secretdHome = secretdHome;
    }

    public async Task RollbackOneAsync()
    {
        var args = MakeArgs();
        args.Add("rollback");

        var cmd = await Cli.Wrap(_secretdPath)
            .WithArguments(args)
            .ExecuteBufferedAsync();
    }

    private List<string> MakeArgs()
    {
        var args = new List<string>();

        if (_secretdHome is not null)
        {
            args.Add("--home");
            args.Add(_secretdHome);
        }

        return args;
    }
}
