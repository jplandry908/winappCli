namespace Winsdk.Cli.Services;

internal interface IPackageInstallationService
{
    void InitializeWorkspace(string rootDirectory);
    
    Task<Dictionary<string, string>> InstallPackagesAsync(
        string rootDirectory,
        IEnumerable<string> packages,
        bool includeExperimental = false,
        bool ignoreConfig = false,
        CancellationToken cancellationToken = default);
    
    Task<bool> EnsurePackageAsync(
        string rootDirectory,
        string packageName,
        string? version = null,
        bool includeExperimental = false,
        CancellationToken cancellationToken = default);
}
