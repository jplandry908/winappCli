using System.CommandLine;
using Winsdk.Cli.Services;

namespace Winsdk.Cli.Commands;

internal class InitCommand : Command
{
    public InitCommand() : base("init", "Initializes a directory with required assets (manifest, certs, libraries) for building a modern Windows app. ")
    {
        var baseDirectoryArgument = new Argument<string>("base-directory")
        {
            Description = "Base/root directory for the winsdk workspace, for consumption or installation.",
            Arity = ArgumentArity.ZeroOrOne
        };
        var configDirOption = new Option<string>("--config-dir")
        {
            Description = "Directory to read/store configuration (default: current directory)",
            DefaultValueFactory = (argumentResult) => Directory.GetCurrentDirectory()
        };
        var prereleaseOption = new Option<bool>("--prerelease")
        {
            Description = "Include prerelease packages from NuGet"
        };
        var ignoreConfigOption = new Option<bool>("--ignore-config", "--no-config")
        {
            Description = "Don't use configuration file for version management"
        };
        var noGitignoreOption = new Option<bool>("--no-gitignore")
        {
            Description = "Don't update .gitignore file"
        };
        var quietOption = new Option<bool>("--quiet", "-q")
        {
            Description = "Suppress progress messages"
        };
        var yesOption = new Option<bool>("--yes", "--no-prompt")
        {
            Description = "Assume yes to all prompts"
        };
        var noCertOption = new Option<bool>("--no-cert")
        {
            Description = "Skip development certificate generation"
        };
        var configOnlyOption = new Option<bool>("--config-only")
        {
            Description = "Only handle configuration file operations (create if missing, validate if exists). Skip package installation, certificate generation, and other workspace setup steps."
        };

        Arguments.Add(baseDirectoryArgument);
        Options.Add(configDirOption);
        Options.Add(prereleaseOption);
        Options.Add(ignoreConfigOption);
        Options.Add(noGitignoreOption);
        Options.Add(quietOption);
        Options.Add(yesOption);
        Options.Add(noCertOption);
        Options.Add(configOnlyOption);
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
            var prerelease = parseResult.GetValue(prereleaseOption);
            var ignoreConfig = parseResult.GetValue(ignoreConfigOption);
            var noGitignore = parseResult.GetValue(noGitignoreOption);
            var quiet = parseResult.GetValue(quietOption);
            var assumeYes = parseResult.GetValue(yesOption);
            var noCert = parseResult.GetValue(noCertOption);
            var configOnly = parseResult.GetValue(configOnlyOption);
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
                IncludeExperimental = prerelease,
                IgnoreConfig = ignoreConfig,
                NoGitignore = noGitignore,
                AssumeYes = assumeYes,
                RequireExistingConfig = false,
                ForceLatestBuildTools = true,
                NoCert = noCert,
                ConfigOnly = configOnly
            };

            return await workspaceSetupService.SetupWorkspaceAsync(options, ct);
        });
    }
}
