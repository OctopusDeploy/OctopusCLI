using System;
using System.Collections.Generic;
using Octopus.Cli.Util;

namespace Octopus.Cli.Infrastructure
{
    public class Options
    {
        public Options()
        {
            OptionSets = new Dictionary<string, OptionSet>();
        }

        public Dictionary<string, OptionSet> OptionSets { get; }

        public OptionSet For(string groupName)
        {
            if (!OptionSets.ContainsKey(groupName))
            {
                var o = new OptionSet();
                OptionSets[groupName] = o;
                return o;
            }

            return OptionSets[groupName];
        }

        public List<string> Parse(IEnumerable<string> arguments)
        {
            var combined = new OptionSet();
            foreach (var group in OptionSets.Keys)
                combined.AddRange(OptionSets[group]);

            return combined.Parse(arguments);
        }
    }
}
