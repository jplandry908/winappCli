using System.CommandLine;
using Winsdk.Cli.Services;

namespace Winsdk.Cli.Commands;

internal class RestoreCommand : Command
{
    public RestoreCommand() : base("restore", "Restore packages from winsdk.yaml and ensure workspace is ready")
    {
        var baseDirectoryArgument = new Argument<string>("base-directory")
        {
            Description = "Base/root directory for the winsdk workspace",
            Arity = ArgumentArity.ZeroOrOne
        };
        
        var configDirOption = new Option<string>("--config-dir")
        {
            Description = "Directory to read configuration from (default: current directory)",
            DefaultValueFactory = (argumentResult) => Directory.GetCurrentDirectory()
        };
        
        var quietOption = new Option<bool>("--quiet", "-q")
        {
            Description = "Suppress progress messages"
        };

        Arguments.Add(baseDirectoryArgument);
        Options.Add(configDirOption);
        Options.Add(quietOption);
        Options.Add(Program.VerboseOption);

        SetAction(async (parseResult, ct) =>
        {
            var configService = new ConfigService(Directory.GetCurrentDirectory());
            var winsdkDirectoryService = new WinsdkDirectoryService();
            var nugetService = new NugetService();
            var cacheService = new PackageCacheService(winsdkDirectoryService);
            var packageInstallationService = new PackageInstallationService(configService, nugetService, cacheService);
            var buildToolsService = new BuildToolsService(configService, winsdkDirectoryService, packageInstallationService);
            var cppWinrtService = new CppWinrtService();
            var packageLayoutService = new PackageLayoutService();
            var powerShellService = new PowerShellService();
            var certificateService = new CertificateService(buildToolsService, powerShellService);
            var manifestService = new ManifestService();
            var devModeService = new DevModeService();
            var workspaceSetupService = new WorkspaceSetupService(configService, winsdkDirectoryService, packageInstallationService, buildToolsService, cppWinrtService, packageLayoutService, certificateService, powerShellService, nugetService, manifestService, devModeService);

            var baseDirectory = parseResult.GetValue(baseDirectoryArgument);
            var configDir = parseResult.GetRequiredValue(configDirOption);
            var quiet = parseResult.GetValue(quietOption);
            var verbose = parseResult.GetValue(Program.VerboseOption);

            if (quiet && verbose)
            {
                Console.Error.WriteLine($"Cannot specify both --quiet and --verbose options together.");
                return 1;
            }

            var options = new WorkspaceSetupOptions
            {
                BaseDirectory = baseDirectory ?? Directory.GetCurrentDirectory(),
                ConfigDir = configDir,
                Quiet = quiet,
                Verbose = verbose,
                RequireExistingConfig = true,
                ForceLatestBuildTools = false // Will be determined from config
            };

            return await workspaceSetupService.SetupWorkspaceAsync(options, ct);
        });
    }
}
