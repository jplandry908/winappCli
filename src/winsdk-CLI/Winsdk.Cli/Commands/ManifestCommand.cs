using System.CommandLine;

namespace Winsdk.Cli.Commands;

internal class ManifestCommand : Command
{
    public ManifestCommand() : base("manifest", "AppxManifest.xml management")
    {
        Subcommands.Add(new ManifestGenerateCommand());
    }
}
