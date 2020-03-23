using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Octopus.Cli.Infrastructure;
using Octopus.Cli.Util;

namespace Octopus.Cli.Commands
{
    [Command("complete", Description = "Supports command line auto completion.")]
    public class CompleteCommand : CommandBase
    {
        private readonly ICommandLocator commands;

        public CompleteCommand(ICommandLocator commands, ICommandOutputProvider commandOutputProvider) : base(commandOutputProvider)
        {
            this.commands = commands;
        }

        public override Task Execute(string[] commandLineArguments)
        {
            return Task.Run(() =>
            {
                Options.Parse(commandLineArguments);
                if (printHelp)
                {
                    GetHelp(Console.Out, commandLineArguments);
                    return;
                }
                commandOutputProvider.PrintMessages = true;
                var completionMap = GetCompletionMap();
                var suggestions = CommandSuggester.SuggestCommandsFor(commandLineArguments, completionMap);
                foreach (var s in suggestions)
                {
                    commandOutputProvider.Information(s);
                }
            });
        }

        protected override void PrintDefaultHelpOutput(TextWriter writer, string executable, string commandName, string description)
        {
            if (commandOutputProvider.PrintMessages)
            {
                Console.ResetColor();
                writer.WriteLine(description);
                writer.WriteLine();
                writer.Write("Usage: ");
                Console.ForegroundColor = ConsoleColor.White;
                writer.WriteLine($"{executable} {commandName} <command> [<options>]");
                Console.ResetColor();
                writer.WriteLine();
                writer.WriteLine("Where <command> is the current command line to filter auto-completions.");
                writer.WriteLine();
                writer.WriteLine("Where [<options>] is any of: ");
                writer.WriteLine();
            }
            commandOutputProvider.PrintCommandOptions(Options, writer);
        }

        private IReadOnlyDictionary<string, string[]> GetCompletionMap()
        {
            var commandMetadatas = commands.List();
            return commandMetadatas.ToDictionary(
                c => c.Name,
                c =>
                {
                    var subCommand = (CommandBase) commands.Find(c.Name);
                    return subCommand.GetOptionNames().ToArray();
                });
        }
    }
}