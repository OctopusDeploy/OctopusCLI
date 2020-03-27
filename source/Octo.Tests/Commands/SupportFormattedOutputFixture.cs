using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Newtonsoft.Json;
using NUnit.Framework;
using Octopus.Cli.Infrastructure;

namespace Octo.Tests.Commands
{
    [TestFixture]
    public class SupportFormattedOutputFixture : ApiCommandFixtureBase
    {
        [Test]
        public void FormattedOutput_ShouldAddOutputOption()
        {
            var sw = new StringWriter();
            var command =  new DummyApiCommandWithFormattedOutputSupport(ClientFactory, RepositoryFactory, FileSystem, CommandOutputProvider);

            command.GetHelp(sw, new []{ "command" });

            sw.ToString().Should().ContainEquivalentOf("--output");
        }

        [Test]
        public async Task FormattedOutput_FormatSetToJson()
        {
            var command =
                new DummyApiCommandWithFormattedOutputSupport(ClientFactory, RepositoryFactory, FileSystem, CommandOutputProvider);

            CommandLineArgs.Add("--outputFormat=json");

            await command.Execute(CommandLineArgs.ToArray()).ConfigureAwait(false);

            command.PrintJsonOutputCalled.ShouldBeEquivalentTo(true);
        }
        
        [Test]
        public void FormattedOutput_FormatInvalid()
        {
            var command = new DummyApiCommandWithFormattedOutputSupport(ClientFactory, RepositoryFactory, FileSystem, CommandOutputProvider);
            CommandLineArgs.Add("--helpOutputFormat=blah");

            var exception = Assert.ThrowsAsync<CommandException>(async () => await command.Execute(CommandLineArgs.ToArray()).ConfigureAwait(false));

            command.PrintJsonOutputCalled.Should().BeFalse();
            command.PrintDefaultOutputCalled.Should().BeFalse();
            exception.Message.Should().Be("Could not convert string `blah' to type OutputFormat for option `--helpOutputFormat'. Valid values are Default and Json.");
        }

        [Test]
        public async Task JsonFormattedOutputHelp_ShouldBeWellFormed()
        {
            var command = new DummyApiCommandWithFormattedOutputSupport(ClientFactory, RepositoryFactory, FileSystem, CommandOutputProvider);

            CommandLineArgs.Add("--helpOutputFormat=json");
            CommandLineArgs.Add("--help");

            await command.Execute(CommandLineArgs.ToArray()).ConfigureAwait(false);

            var logOutput = LogOutput.ToString();
            Console.WriteLine(logOutput);
            JsonConvert.DeserializeObject(logOutput);
            logOutput.Should().Contain("--helpOutputFormat=VALUE");
            logOutput.Should().Contain("--help");
            logOutput.Should().Contain("dummy-command");
            logOutput.Should().Contain("this is the command's description");
        }
        
        [Test]
        public async Task PlainTextFormattedOutputHelp_ShouldBeWellFormed()
        {
            var command = new DummyApiCommandWithFormattedOutputSupport(ClientFactory, RepositoryFactory, FileSystem, CommandOutputProvider);

            CommandLineArgs.Add("--help");

            await command.Execute(CommandLineArgs.ToArray()).ConfigureAwait(false);

            var logOutput = LogOutput.ToString();
            Console.WriteLine(logOutput);
            logOutput.Should().Contain("--helpOutputFormat=VALUE");
            logOutput.Should().Contain("--help");
            logOutput.Should().Contain("dummy-command");
            logOutput.Should().Contain("this is the command's description");
        }
    }
}