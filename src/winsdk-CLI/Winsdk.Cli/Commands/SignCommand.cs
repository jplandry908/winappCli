using System.CommandLine;
using Winsdk.Cli.Services;

namespace Winsdk.Cli.Commands;

internal class SignCommand : Command
{
    public SignCommand() : base("sign", "Sign a file/package with a certificate")
    {
        var filePathArgument = new Argument<string>("file-path")
        {
            Description = "Path to the file/package to sign"
        };
        var certPathArgument = new Argument<string>("cert-path")
        {
            Description = "Path to the certificate file (PFX format)"
        };
        var passwordOption = new Option<string>("--password")
        {
            Description = "Certificate password",
            DefaultValueFactory = (argumentResult) => "password"
        };
        var timestampOption = new Option<string>("--timestamp")
        {
            Description = "Timestamp server URL"
        };

        Arguments.Add(filePathArgument);
        Arguments.Add(certPathArgument);
        Options.Add(passwordOption);
        Options.Add(timestampOption);
        Options.Add(Program.VerboseOption);

        SetAction(async (parseResult, ct) =>
        {
            var configService = new ConfigService(Directory.GetCurrentDirectory());
            var directoryService = new WinsdkDirectoryService();
            var nugetService = new NugetService();
            var cacheService = new PackageCacheService(directoryService);
            var packageService = new PackageInstallationService(configService, nugetService, cacheService);
            var buildToolsService = new BuildToolsService(configService, directoryService, packageService);
            var powerShellService = new PowerShellService();
            var certificateService = new CertificateService(buildToolsService, powerShellService);
        
            var filePath = parseResult.GetRequiredValue(filePathArgument);
            var certPath = parseResult.GetRequiredValue(certPathArgument);
            var password = parseResult.GetValue(passwordOption);
            var timestamp = parseResult.GetValue(timestampOption);
            var verbose = parseResult.GetValue(Program.VerboseOption);

            try
            {
                await certificateService.SignFileAsync(filePath, certPath, password, timestamp, verbose, ct);

                Console.WriteLine($"🔐 Signed file: {filePath}");
                return 0;
            }
            catch (Exception error)
            {
                Console.Error.WriteLine($"❌ Failed to sign file: {error.Message}");
                return 1;
            }
        });
    }
}
