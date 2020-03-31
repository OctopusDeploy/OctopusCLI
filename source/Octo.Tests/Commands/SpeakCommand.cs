using System.Threading.Tasks;
using NUnit.Framework;
using Octopus.Cli.Commands;
using Octopus.Cli.Infrastructure;
using Octopus.Cli.Util;

namespace Octo.Tests.Commands
{
    public class SpeakCommand : CommandBase
    {
        public override Task Execute(string[] commandLineArguments)
        {
            return Task.Run(() => Assert.Fail("This should not be called"));
        }

        public SpeakCommand(ICommandOutputProvider commandOutputProvider) : base(commandOutputProvider)
        {
            var options = Options.For("default");
            options.Add<string>("message=", "The message to speak", m => { });
        }
    }
}