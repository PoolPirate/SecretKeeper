using CliWrap;

namespace SecretKeeper.Services;
public class SecretdService
{
    private string _secretdPath = "";

    public void Initialize(string secretdPath) 
        => _secretdPath = secretdPath;

    public async Task RollbackOneAsync()
    {
        var cmd = await Cli.Wrap(_secretdPath)
            .WithArguments(["rollback"])
            .ExecuteAsync();
    }
}
