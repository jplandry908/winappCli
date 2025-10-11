using System.CommandLine;
using Winsdk.Cli.Services;

namespace Winsdk.Cli.Commands;

internal class CreateDebugIdentityCommand : Command
{
    public CreateDebugIdentityCommand() : base("create-debug-identity", "Create and install a temporary package for debugging. Must be called every time the appxmanifest.xml is modified for changes to take effect.")
    {
        var executableArgument = new Argument<string>("executable")
        {
            Description = "Path to the .exe that will need to run with identity"
        };
        var manifestOption = new Option<string>("--manifest")
        {
            Description = "Path to the appxmanifest.xml",
            DefaultValueFactory = (argumentResult) => ".\\appxmanifest.xml"
        };
        var noInstallOption = new Option<bool>("--no-install")
        {
            Description = "Do not install the package after creation."
        };
        var locationOption = new Option<string>("--location")
        {
            Description = "Root path of the application. Default is parent directory of the executable."
        };

        Arguments.Add(executableArgument);
        Options.Add(manifestOption);
        Options.Add(noInstallOption);
        Options.Add(locationOption);
        Options.Add(Program.VerboseOption);

        SetAction(async (parseResult, ct) =>
        {
            var executablePath = parseResult.GetRequiredValue(executableArgument);
            var manifest = parseResult.GetRequiredValue(manifestOption);
            var noInstall = parseResult.GetValue(noInstallOption);
            var location = parseResult.GetValue(locationOption);
            var verbose = parseResult.GetValue(Program.VerboseOption);

            var configService = new ConfigService(Directory.GetCurrentDirectory());
            var winsdkDirectoryService = new WinsdkDirectoryService();
            var nugetService = new NugetService();
            var packageCacheService = new PackageCacheService(winsdkDirectoryService);
            var packageService = new PackageInstallationService(configService, nugetService, packageCacheService);
            var buildToolsService = new BuildToolsService(configService, winsdkDirectoryService, packageService);
            var powerShellService = new PowerShellService();
            var certificateService = new CertificateService(buildToolsService, powerShellService);
            var cppWinrtService = new CppWinrtService();
            var packageLayoutService = new PackageLayoutService();
            var manifestService = new ManifestService();
            var devModeService = new DevModeService();
            var workspaceSetupService = new WorkspaceSetupService(configService, winsdkDirectoryService, packageService, buildToolsService, cppWinrtService, packageLayoutService, certificateService, powerShellService, nugetService, manifestService, devModeService);
            var msixService = new MsixService(winsdkDirectoryService, configService, buildToolsService, powerShellService, certificateService, packageCacheService, workspaceSetupService);

            if (!File.Exists(executablePath))
            {
                Console.Error.WriteLine($"Executable not found: {executablePath}");
                return 1;
            }

            try
            {
                var result = await msixService.AddMsixIdentityToExeAsync(executablePath, manifest, noInstall, location, verbose, ct);

                Console.WriteLine("✅ MSIX identity added successfully!");
                Console.WriteLine($"📦 Package: {result.PackageName}");
                Console.WriteLine($"👤 Publisher: {result.Publisher}");
                Console.WriteLine($"🆔 App ID: {result.ApplicationId}");
            }
            catch (Exception error)
            {
                Console.Error.WriteLine($"❌ Failed to add MSIX identity: {error.Message}");
                return 1;
            }

            return 0;
        });
    }
}
