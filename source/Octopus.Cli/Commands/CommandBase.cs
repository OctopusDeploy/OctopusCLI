using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Octopus.Cli.Infrastructure;
using Octopus.Cli.Model;
using Octopus.Cli.Util;

namespace Octopus.Cli.Commands
{
    public abstract class CommandBase
    {
        protected readonly ICommandOutputProvider commandOutputProvider;
        protected bool printHelp;
        protected readonly ISupportFormattedOutput formattedOutputInstance;

        protected CommandBase(ICommandOutputProvider commandOutputProvider)
        {
            this.commandOutputProvider = commandOutputProvider;
            
            var options = Options.For("Common options");
            options.Add<bool>("help", "[Optional] Print help for a command", x => printHelp = true);
            options.Add<OutputFormat>("helpOutputFormat=", $"[Optional] Output format for help, valid options are {Enum.GetNames(typeof(OutputFormat)).ReadableJoin("or")}", s => HelpOutputFormat = s);
            formattedOutputInstance = this as ISupportFormattedOutput;
            if (formattedOutputInstance != null)
            {
                options.Add<OutputFormat>("outputFormat=", $"[Optional] Output format, valid options are {Enum.GetNames(typeof(OutputFormat)).ReadableJoin("or")}", s => OutputFormat = s);
            }
            else
            {
                commandOutputProvider.PrintMessages = true;
            }
        }

        protected internal Options Options { get; } = new Options();

        public OutputFormat OutputFormat { get; set; }

        public OutputFormat HelpOutputFormat { get; set; }

        public virtual void GetHelp(TextWriter writer, string[] args)
        {
            var typeInfo = this.GetType().GetTypeInfo();

            var executable = AssemblyExtensions.GetExecutableName();
            var commandAttribute = typeInfo.GetCustomAttribute<CommandAttribute>();
            string commandName;
            var description = string.Empty;
            if (commandAttribute == null)
            {
                commandName = args.FirstOrDefault();
            }
            else
            {
                commandName = commandAttribute.Name;
                description = commandAttribute.Description;
            }

            commandOutputProvider.PrintMessages = HelpOutputFormat == OutputFormat.Default;
            if (HelpOutputFormat  == OutputFormat.Json)
            {
                PrintJsonHelpOutput(writer, commandName, description);
            }
            else
            {
                PrintDefaultHelpOutput(writer, executable, commandName, description);
            }
        }

        private void PrintDefaultHelpOutput(TextWriter writer, string executable, string commandName, string description)
        {
            commandOutputProvider.PrintCommandHelpHeader(executable, commandName, description, writer);
            commandOutputProvider.PrintCommandOptions(Options, writer);
        }

        private void PrintJsonHelpOutput(TextWriter writer, string commandName, string description)
        {
            commandOutputProvider.Json(new
            {
                Command = commandName,
                Description = description,
                Options = Options.OptionSets.OrderByDescending(x => x.Key).Select(g => new
                {
                    @Group = g.Key,
                    Parameters = g.Value.Select(p => new
                    {
                        Name = p.Names.First(),
                        Usage = string.Format("{0}{1}{2}", p.Prototype.Length == 1 ? "-" : "--", p.Prototype,
                            p.Prototype.EndsWith("=") ? "VALUE" : string.Empty),
                        p.Description,
                        Type = p.Type.Name,
                        Sensitive = p.Sensitive ? (bool?)true : null,
                        Values = (p.Type.IsEnum) ? Enum.GetNames(p.Type) : null 
                    })
                })
            }, writer);
        }
    }
}