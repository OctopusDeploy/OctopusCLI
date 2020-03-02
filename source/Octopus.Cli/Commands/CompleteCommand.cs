using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Octopus.Cli.Infrastructure;
using Octopus.Cli.Model;
using Octopus.Cli.Util;

namespace Octopus.Cli.Commands
{
    [Command("complete", Description = "Find the most likely subcommand completion based on a partial subcommand.")]
    public class CompleteCommand : CommandBase, ICommand
    {
        private ICommandLocator commands;

        public CompleteCommand(ICommandLocator commands, ICommandOutputProvider commandOutputProvider) : base(commandOutputProvider)
        {
            this.commands = commands;
        }

        public Task Execute(string[] commandLineArguments)
        {
            return Task.Run(() =>
            {
                Options.Parse(commandLineArguments);
                
                commandOutputProvider.PrintMessages = OutputFormat == OutputFormat.Default;
                
                if (commandLineArguments.Length > 1) throw new CommandException("Unexpected parameters, please specify a single search term.");

                var searchTerm = commandLineArguments.Length > 0 ? commandLineArguments.Last() : "";
                var suggestions = CommandSuggester.SuggestCommandsFor(searchTerm, GetCompletionMap());
                foreach (var suggestion in suggestions)
                {
                    commandOutputProvider.Information(suggestion);
                }
            });
        }

        private List<string> GetCompletionMap()
        {
            return commands.List().Select(command => command.Name).ToList();
        }
    }
}