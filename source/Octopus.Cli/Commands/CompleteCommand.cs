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
                commandOutputProvider.PrintMessages = true;
                var completionMap = GetCompletionMap();
                var suggestions = CommandSuggester.SuggestCommandsFor(commandLineArguments, completionMap);
                foreach (var s in suggestions)
                {
                    commandOutputProvider.Information(s);
                }
            });
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