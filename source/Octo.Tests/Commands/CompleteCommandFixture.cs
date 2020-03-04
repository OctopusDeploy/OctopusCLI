using System;
using System.IO;
using System.Threading.Tasks;
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
                new CommandAttribute("help")
            });
            commandLocator.Find("help").Returns(new HelpCommand(commandLocator, commandOutputProvider));
            commandLocator.Find("test").Returns(new TestCommand(commandOutputProvider));
            completeCommand = new CompleteCommand(commandLocator, commandOutputProvider);
        }

        [Test]
        public async Task ShouldReturnSubCommandSuggestions()
        {
            await completeCommand.Execute(new[] { "he" });
            
            output.ToString()
                .Should()
                .Contain("help")
                .And.NotContain("test");
        }

        [Test]
        public async Task ShouldReturnParameterSuggestions()
        {
            await completeCommand.Execute(new[] {"test", "--ap"});
            output.ToString()
                .Should()
                .Contain("--apiKey");
        }

        [Test]
        public async Task ShouldReturnCommonOptionsWhenSingleEmptyParameter()
        {
           await completeCommand.Execute(new[] {"--"});
           output.ToString()
               .Should()
               .Contain("--helpOutputFormat");
        }
        
        [Test]
        public async Task ShouldReturnOptionSuggestions()
        {
           await completeCommand.Execute(new[] {"--helpOut"});
           output.ToString()
               .Should()
               .Contain("--helpOutputFormat")
               .And.NotContain("--help\n");
        }

        [Test]
        public async Task ShouldReturnAllSubCommandsWhenEmptyArguments()
        {
           await completeCommand.Execute(new[] {""});
           output.ToString()
               .Should()
               .Contain("help")
               .And.Contain("test");
        }

        [Test]
        public async Task ShouldStopSubCommandCompletionAfterOptionSuggestion()
        {
            await completeCommand.Execute(new[] {"test", "--api", "API-KEY", "--u"});
            output.ToString()
                .Should()
                .Contain("--url");
        }

        [TearDown]
        public void TearDown()
        {
            Console.SetOut(originalOutput);
        }
    }

    [Command("test", Description = "test command")]
    public class TestCommand : CommandBase, ICommand
    {
        public TestCommand(ICommandOutputProvider commandOutputProvider) : base(commandOutputProvider)
        {
            var options = Options.For("Test group");
            options.Add("apiKey", "api key", v => ApiKey = v);
            options.Add("url", "url", v => Url = v);
        }

        public string Url { get; set; }

        public string ApiKey { get; set; }
        public Task Execute(string[] commandLineArguments)
        {
            return Task.Run(() => 0);
        }
    }
}