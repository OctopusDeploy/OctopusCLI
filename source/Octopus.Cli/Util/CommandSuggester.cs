using System;
using System.Collections.Generic;
using System.Linq;

namespace Octopus.Cli.Util
{
   public static class CommandSuggester
    {
        public static IEnumerable<string> SuggestCommandsFor(
            string words,
            IList<string> completionItems)
        {
            return string.IsNullOrWhiteSpace(words)
                ? completionItems
                : completionItems.Where(item =>
                    item.StartsWith(words.ToLowerInvariant(), StringComparison.OrdinalIgnoreCase));
        }
    } 
}

