using System;
using System.IO;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Cli.Commands;
using Octopus.Cli.Infrastructure;
using Octopus.Cli.Util;
using Serilog;

namespace Octo.Tests.Commands
{
    [TestFixture]
    public class CompleteCommandFixture
    {
        CompleteCommand completeCommand;
        StringWriter output;
        TextWriter originalOutput;
        private ICommandOutputProvider commandOutputProvider;
        private ILogger logger;

        [SetUp]
        public void SetUp()
        {
           originalOutput = Console.Out;
           output = new StringWriter();
           Console.SetOut(output);

           commandOutputProvider = new CommandOutputProvider(logger);
           ICommandLocator commands = Substitute.For<ICommandLocator>();
           commands.List().Returns(new []
           {
               new CommandAttribute("test"), 
               new CommandAttribute("list-environments")
           });
                   
           completeCommand = new CompleteCommand(commands, commandOutputProvider);
           logger = new LoggerConfiguration().WriteTo.TextWriter(output).CreateLogger();
        }

        [Test]
        public void ShouldReturnSubCommandSuggestions()
        {
            completeCommand.Execute(new[] { "list" });
            output.ToString()
                .Should()
                .Contain("list-environments");
        }
    }
}