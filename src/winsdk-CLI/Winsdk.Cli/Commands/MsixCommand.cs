using System.CommandLine;

namespace Winsdk.Cli.Commands;

internal class MsixCommand : Command
{
    public MsixCommand() : base("msix", "MSIX package management")
    {
        Subcommands.Add(new MsixAddIdentityCommand());
    }
}
