namespace Winsdk.Cli.Services;

internal class ManifestService
{
    private readonly BuildToolsService _buildToolsService;

    public ManifestService(BuildToolsService buildToolsService)
    {
        _buildToolsService = buildToolsService;
    }

    public async Task GenerateManifestAsync(
        string directory, 
        string? packageName, 
        string? publisherName, 
        string version,
        string description,
        string? executable, 
        bool sparse, 
        string? logoPath, 
        bool yes, 
        bool verbose,
        CancellationToken cancellationToken = default)
    {
        if (verbose)
        {
            Console.WriteLine($"Generating manifest in directory: {directory}");
        }

        // Check if manifest already exists
        var manifestPath = Path.Combine(directory, "appxmanifest.xml");
        if (File.Exists(manifestPath))
        {
            throw new InvalidOperationException($"Manifest already exists at: {manifestPath}");
        }

        // Interactive mode if not --yes (get defaults for prompts)
        var defaults = new SystemDefaultsService();
        packageName ??= defaults.GetDefaultPackageName(directory);
        publisherName ??= defaults.GetDefaultPublisherCN();
        executable ??= $"{packageName}.exe";

        // Interactive mode if not --yes
        if (!yes)
        {
            packageName = PromptForValue("Package name", packageName);
            publisherName = PromptForValue("Publisher name", publisherName);
            version = PromptForValue("Version", version);
            description = PromptForValue("Description", description);
            executable = PromptForValue("Executable", executable);

            if (logoPath == null)
            {
                logoPath = PromptForValue("Logo path (optional)", "");
                if (string.IsNullOrWhiteSpace(logoPath))
                {
                    logoPath = null;
                }
            }
        }

        if (verbose)
        {
            Console.WriteLine($"Logo path: {logoPath ?? "None"}");
        }

        // Generate complete manifest using shared service
        await ManifestTemplateService.GenerateCompleteManifestAsync(
            directory,
            packageName,
            publisherName,
            version,
            executable,
            sparse,
            description,
            verbose,
            cancellationToken);

        // If logo path is provided, copy it as additional asset
        if (!string.IsNullOrEmpty(logoPath) && File.Exists(logoPath))
        {
            await CopyLogoAsAdditionalAssetAsync(directory, logoPath, verbose, cancellationToken);
        }
    }

    private async Task CopyLogoAsAdditionalAssetAsync(string directory, string logoPath, bool verbose, CancellationToken cancellationToken = default)
    {
        var assetsDir = Path.Combine(directory, "Assets");
        Directory.CreateDirectory(assetsDir);

        var logoFileName = Path.GetFileName(logoPath);
        var destinationPath = Path.Combine(assetsDir, logoFileName);

        using var sourceStream = new FileStream(logoPath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true);
        using var destinationStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: true);
        
        await sourceStream.CopyToAsync(destinationStream, cancellationToken);

        if (verbose)
        {
            Console.WriteLine($"Logo copied to: {destinationPath}");
        }
    }

    private string PromptForValue(string prompt, string defaultValue)
    {
        Console.Write($"{prompt} ({defaultValue}): ");
        var input = Console.ReadLine();
        return string.IsNullOrWhiteSpace(input) ? defaultValue : input.Trim();
    }
}
