using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Packaging;
using Octopus.Cli.Commands;

namespace Octopus.Cli.Util
{
   public static class CommandSuggester
    {
        public static IEnumerable<string> SuggestCommandsFor(
            string[] words,
            IReadOnlyDictionary<string, string[]> completionItems)
        {
            // some shells will pass the command name as invoked on the command line.
            // If so, strip them from the beginning of the array
            words = words
                    .Take(2).Except(new[] {"octo", "complete"}, StringComparer.OrdinalIgnoreCase)
                    .Union(words.Skip(2))
                    .Where(word => string.IsNullOrWhiteSpace(word) == false)
                    .ToArray();
            
            var numberOfArgs = words.Length;
            var hasSubCommand = numberOfArgs > 1;
            var searchTerm = 
                numberOfArgs > 0 
                    ? words.Last() 
                    : "";
            var suggestions = new List<string>();
            var isOptionSearch = searchTerm.StartsWith("--");
            var allSubCommands = completionItems.Keys.ToList();
            
            if (isOptionSearch)
            {
                if (hasSubCommand)
                {
                    // e.g. `octo subcommand --searchTerm`
                    var subCommandName = words.Where(w => IsSubCommand(w, allSubCommands)).Last();
                    suggestions.AddRange(GetSubCommandOptions(completionItems, subCommandName, searchTerm));
                }
                else
                {
                    // e.g. `octo --searchTerm`
                    return GetOctoOptions(completionItems, searchTerm);
                }
            }
            else if (ZeroOrOneSubCommands(words, allSubCommands))
            {
                // e.g. `octo searchterm` or just `octo`
                suggestions.AddRange(completionItems.Keys.Where(s =>
                    s.StartsWith(searchTerm, StringComparison.OrdinalIgnoreCase)));
            }

            return suggestions;
        }

        private static bool ZeroOrOneSubCommands(string[] words, List<string> allSubCommands)
        {
            return words.Where(w => IsSubCommand(w, allSubCommands)).Count() <= 1;
        }

        private static IEnumerable<string> GetSubCommandOptions(IReadOnlyDictionary<string, string[]> completionItems, string subCommandName, string searchTerm)
        {
            if (completionItems.TryGetValue(subCommandName, out var options))
            {
                return options
                    .Where(name => name.StartsWith(searchTerm))
                    .ToList();
            }

            return new string[] {};
        }

        private static IEnumerable<string> GetOctoOptions(IReadOnlyDictionary<string, string[]> completionItems, string searchTerm)
        {
            // If you type 'octo', you'll be redirected to the 'help' command, so show these options
            return GetSubCommandOptions(completionItems, "help", searchTerm);
        }

        private static bool IsSubCommand(string arg, List<string> subCommandList)
        {
            if (arg == null) return false;
            if (arg.StartsWith("--")) return false;
            return subCommandList.Contains(arg);
        }
    } 
}

