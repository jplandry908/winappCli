namespace Winsdk.Cli.Services;

internal interface IWorkspaceSetupService
{
    public string? FindWindowsAppSdkMsixDirectory(Dictionary<string, string>? usedVersions = null);
}
