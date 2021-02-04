using System;
using System.Collections.Generic;
using System.Linq;

namespace Octopus.Cli.Commands
{
    public static class CommandBaseExtensions
    {
        public static IEnumerable<string> GetOptionNames(this CommandBase command)
        {
            return command.Options.OptionSets
                .SelectMany(keyValuePair => keyValuePair.Value)
                .SelectMany(option => option.Names)
                .Select(str => $"--{str}");
        }
    }
}
