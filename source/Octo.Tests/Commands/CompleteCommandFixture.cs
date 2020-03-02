using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Cli.Commands;
using Octopus.Cli.Infrastructure;
using Octopus.Cli.Tests.Helpers;
using Octopus.Cli.Util;
using Serilog;

namespace Octo.Tests.Commands
{
    [TestFixture]
    public class CompleteCommandFixture 
    {
        CompleteCommand completeCommand;
        private ICommandOutputProvider commandOutputProvider;
        private ILogger logger;

        private TextWriter originalOutput;
        private StringWriter output;
        private ICommandLocator commandLocator;

        [SetUp]
        public void SetUp()
        {
            originalOutput = Console.Out;
            output = new StringWriter();
            Console.SetOut(output);

            commandLocator = Substitute.For<ICommandLocator>();
            logger = new LoggerConfiguration().WriteTo.TextWriter(output).CreateLogger();
            commandOutputProvider = new CommandOutputProvider(logger);
            commandLocator.List().Returns(new []
            {
                new CommandAttribute("test"), 
                new CommandAttribute("list-environments")
            });
               
            completeCommand = new CompleteCommand(commandLocator, commandOutputProvider);
        }

        [Test]
        public async Task ShouldReturnSubCommandSuggestions()
        {
            await completeCommand.Execute(new[] { "list" });
            
            output.ToString()
                .Should()
                .Contain("list-environments")
                .And.NotContain("test");
        }

        [TearDown]
        public void TearDown()
        {
            Console.SetOut(originalOutput);
        }
    }
}