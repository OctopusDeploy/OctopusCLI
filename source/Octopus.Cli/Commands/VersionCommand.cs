using System;
using System.Threading.Tasks;
using Octopus.Cli.Infrastructure;
using Octopus.Cli.Util;

namespace Octopus.Cli.Commands
{
    [Command("version", "v", "ver", Description = "Outputs Octopus CLI version.")]
    public class VersionCommand : CommandBase
    {
        public VersionCommand(ICommandOutputProvider commandOutputProvider) : base(commandOutputProvider)
        {
        }

        public override Task Execute(string[] commandLineArgs)
        {
            return Task.Run(() =>
            {
                Options.Parse(commandLineArgs);

                if (printHelp)
                    GetHelp(Console.Out, commandLineArgs);
                else
                    Console.WriteLine($"{typeof(CliProgram).GetInformationalVersion()}");
            });
        }

    }
}
