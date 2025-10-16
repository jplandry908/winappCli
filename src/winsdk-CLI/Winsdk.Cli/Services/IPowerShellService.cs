namespace Winsdk.Cli.Services;

internal interface IPowerShellService
{
    public Task<(int exitCode, string output)> RunCommandAsync(
        string command,
        bool elevated = false,
        Dictionary<string, string>? environmentVariables = null,
        CancellationToken cancellationToken = default);
}
