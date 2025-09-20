using System.CommandLine;
using Winsdk.Cli.Services;

namespace Winsdk.Cli.Commands;

internal class GetGlobalWinsdkCommand : Command
{
    public GetGlobalWinsdkCommand() : base("get-global-winsdk", "Get the path to the global .winsdk directory")
    {
        Options.Add(Program.VerboseOption);

        SetAction((parseResult, ct) =>
        {
            var verbose = parseResult.GetValue(Program.VerboseOption);

            try
            {
                var configService = new ConfigService(Directory.GetCurrentDirectory());
                var buildToolsService = new BuildToolsService(configService);

                // Find the global .winsdk directory
                var winsdkDir = buildToolsService.FindWinsdkDirectory();
                
                if (string.IsNullOrEmpty(winsdkDir) || !Directory.Exists(winsdkDir))
                {
                    if (verbose)
                    {
                        Console.Error.WriteLine($"❌ Global .winsdk directory not found");
                        Console.Error.WriteLine($"   Make sure to run 'winsdk setup' first");
                    }
                    return Task.FromResult(1);
                }

                // Output just the path for easy consumption by scripts
                Console.WriteLine(winsdkDir);
                return Task.FromResult(0);
            }
            catch (Exception ex)
            {
                if (verbose)
                {
                    Console.Error.WriteLine($"❌ Error getting global winsdk directory: {ex.Message}");
                }
                else
                {
                    Console.Error.WriteLine($"Global winsdk directory not found");
                }
                return Task.FromResult(1);
            }
        });
    }
}