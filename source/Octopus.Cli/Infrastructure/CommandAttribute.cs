using System;

namespace Octopus.Cli.Infrastructure
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public class CommandAttribute : Attribute, ICommandMetadata
    {
        public CommandAttribute(string name, params string[] aliases)
        {
            Name = name;
            Aliases = aliases;
        }

        public string Name { get; set; }
        public string[] Aliases { get; set; }
        public string Description { get; set; }
    }
}
