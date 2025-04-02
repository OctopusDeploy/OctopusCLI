using System;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Cli.Diagnostics;
using Octopus.CommandLine.Commands;
using Serilog.Events;

namespace Octo.Tests.Diagnostics
{
    [TestFixture]
    public sealed class LogUtilitiesFixture
    {
        [Test]
        public void ShouldThrowIfUnknownLogLevelIsProvided()
        {
            var result = Assert.Throws<CommandException>(() => LogUtilities.ParseLogLevel("z"));
            Assert.That(result.Message, Is.EquivalentTo("Unrecognized loglevel 'z'. Valid options are verbose, debug, information, warning, error and fatal. " +
                "Defaults to 'debug'."));
        }

        [TestCase("fatal", LogEventLevel.Fatal)]
        [TestCase("error", LogEventLevel.Error)]
        [TestCase("warning", LogEventLevel.Warning)]
        [TestCase("information", LogEventLevel.Information)]
        [TestCase("debug", LogEventLevel.Debug)]
        [TestCase("verbose", LogEventLevel.Verbose)]
        public void ShouldParseLogLevelCorrectly(string value, LogEventLevel level)
        {
            var result = LogUtilities.ParseLogLevel(value);
            Assert.That(result, Is.EqualTo(level));
        }
    }
}
