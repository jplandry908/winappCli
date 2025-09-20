using System.CommandLine;
using Winsdk.Cli.Services;

namespace Winsdk.Cli.Commands;

internal class ManifestGenerateCommand : Command
{
    public ManifestGenerateCommand() : base("generate", "Generate a manifest in directory")
    {
        var directoryArgument = new Argument<string>("directory")
        {
            Description = "Directory to generate manifest in",
            Arity = ArgumentArity.ZeroOrOne,
            DefaultValueFactory = (argumentResult) => Directory.GetCurrentDirectory()
        };

        var packageNameOption = new Option<string>("--package-name")
        {
            Description = "Package name (default: folder name)"
        };

        var publisherNameOption = new Option<string>("--publisher-name")
        {
            Description = "Publisher CN (default: CN=<current user>)"
        };

        var versionOption = new Option<string>("--version")
        {
            Description = "Version",
            DefaultValueFactory = (argumentResult) => "1.0.0.0"
        };

        var descriptionOption = new Option<string>("--description")
        {
            Description = "Description",
            DefaultValueFactory = (argumentResult) => "My Application"
        };

        var executableOption = new Option<string?>("--executable")
        {
            Description = "Executable path/name (default: <package-name>.exe)"
        };

        var sparseOption = new Option<bool>("--sparse")
        {
            Description = "Generate sparse package manifest"
        };

        var logoPathOption = new Option<string?>("--logo-path")
        {
            Description = "Path to logo image file"
        };

        var yesOption = new Option<bool>("--yes", "--y")
        {
            Description = "Skip interactive prompts and use default values"
        };

        var verboseOption = new Option<bool>("--verbose", "--v")
        {
            Description = "Enable verbose output"
        };

        Arguments.Add(directoryArgument);
        Options.Add(packageNameOption);
        Options.Add(publisherNameOption);
        Options.Add(versionOption);
        Options.Add(descriptionOption);
        Options.Add(executableOption);
        Options.Add(sparseOption);
        Options.Add(logoPathOption);
        Options.Add(yesOption);
        Options.Add(verboseOption);

        SetAction(async (parseResult, ct) =>
        {
            var directory = parseResult.GetRequiredValue(directoryArgument);
            var packageName = parseResult.GetValue(packageNameOption);
            var publisherName = parseResult.GetValue(publisherNameOption);
            var version = parseResult.GetRequiredValue(versionOption);
            var description = parseResult.GetRequiredValue(descriptionOption);
            var executable = parseResult.GetValue(executableOption);
            var sparse = parseResult.GetValue(sparseOption);
            var logoPath = parseResult.GetValue(logoPathOption);
            var yes = parseResult.GetValue(yesOption);
            var verbose = parseResult.GetValue(verboseOption);

            var manifestService = new ManifestService();

            await manifestService.GenerateManifestAsync(
                directory,
                packageName,
                publisherName,
                version,
                description,
                executable,
                sparse,
                logoPath,
                yes,
                verbose,
                ct);

            Console.WriteLine($"Manifest generated successfully in: {directory}");
        });
    }
}
