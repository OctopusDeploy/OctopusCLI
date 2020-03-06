using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Octopus.Cli.Infrastructure;
using Octopus.Cli.Model;
using Octopus.Cli.Util;

namespace Octopus.Cli.Commands
{
    [Command("complete", Description = "Supports command line auto completion.")]
    public class CompleteCommand : CommandBase, ICommand
    {
        private ICommandLocator commands;

        public CompleteCommand(ICommandLocator commands, ICommandOutputProvider commandOutputProvider) : base(commandOutputProvider)
        {
            this.commands = commands;
        }

        public Task Execute(string[] commandLineArguments)
        {
            Options.Parse(commandLineArguments);
            commandOutputProvider.PrintMessages = true;
            var completionMap = GetCompletionMap();
            var suggestions = CommandSuggester.SuggestCommandsFor(commandLineArguments, completionMap);
            foreach (var s in suggestions)
            {
                commandOutputProvider.Information(s);
            }
            
            return Task.CompletedTask;
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