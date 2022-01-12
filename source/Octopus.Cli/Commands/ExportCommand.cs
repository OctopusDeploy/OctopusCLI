using System;
using System.Threading.Tasks;
using Octopus.Cli.Util;
using Octopus.CommandLine;
using Octopus.CommandLine.Commands;

namespace Octopus.Cli.Commands
{
    [Command("export", Description = "Exports an object to a JSON file. Deprecated. Please see https://g.octopushq.com/DataMigration for alternative options.")]
    public class ExportCommand : CommandBase
    {
        public override Task Execute(string[] commandLineArgs)
        {
            throw new Exception($"The {AssemblyExtensions.GetExecutableName()} import/export commands have been deprecated. See https://g.octopushq.com/DataMigration for alternative options.");
        }

        public ExportCommand(ICommandOutputProvider commandOutputProvider) : base(commandOutputProvider)
        {
        }
    }
}
