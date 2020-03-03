using System;
using System.Collections.Generic;
using System.Linq;

namespace Octopus.Cli.Util
{
   public static class CommandSuggester
    {
        public static IEnumerable<string> SuggestCommandsFor(
            string[] words,
            IReadOnlyDictionary<string, string[]> completionItems)
        {
            words = words.Except(new[] { "octo", "complete" }).ToArray();
            var numberOfArgs = words.Length;
            var searchTerm = numberOfArgs > 0 ? words.Last() ?? "" : "";
            var suggestions = new List<string>();
            var isOptionSearch = searchTerm.StartsWith("--");

            if (isOptionSearch)
            {
                var hasSubCommand = numberOfArgs > 1;
                var subCommandName =
                    hasSubCommand
                        ? words.Where(IsSubCommand).Last()
                        : "help"; // HelpCommand is a suitable fallback
                
                if (completionItems.TryGetValue(subCommandName, out var options))
                {
                    suggestions.AddRange(
                        options
                            .Where(name => name.StartsWith(searchTerm))
                            .ToList());
                }
            }
            else if (words.Where(IsSubCommand).Count() <= 1)
            {
                suggestions.AddRange(completionItems.Keys.Where(s =>
                    s.StartsWith(searchTerm, StringComparison.OrdinalIgnoreCase)));
            }

            return suggestions;
        }
        
        private static bool IsSubCommand(string arg)
        {
            if (arg == null) return false;
            return !arg.StartsWith("--");
        }
    } 
}

