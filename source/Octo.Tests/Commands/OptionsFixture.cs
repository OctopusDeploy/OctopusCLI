using System.Security.Cryptography.X509Certificates;
using NUnit.Framework;
using Octopus.Cli.Commands.Package;
using Octopus.Cli.Infrastructure;

namespace Octo.Tests.Commands
{
    [TestFixture]
    public class OptionsFixture
    {
        [Test]
        [TestCase("--apiKey=abc123", "-foo=bar")]
        [TestCase("--apikey=abc123", "/foo=bar")]
        [TestCase("-apikey=abc123", "--foo=bar")]
        [TestCase("--apikey=abc123", "-foo=bar")]
        [TestCase("/apikey=abc123", "--foo=bar")]
        public void ShouldBeCaseInsensitive(string parameter1, string parameter2)
        {
            var apiKey = string.Empty;
            var foo = string.Empty;

            var optionSet = new OptionSet();
            optionSet.Add<string>("apiKey=", "API key", v => apiKey = v);
            optionSet.Add<string>("foo=", "Foo", v => foo = v);

            optionSet.Parse(new[] {parameter1, parameter2});

            Assert.That(apiKey, Is.EqualTo("abc123"));
            Assert.That(foo, Is.EqualTo("bar"));
        }

        [Test]
        public void ShowsValidValuesForEnums()
        {
            var optionSet = new OptionSet();
            optionSet.Add<PackageFormat>("packageFormat=", "Package Format", v => { });

            var ex = Assert.Throws<CommandException>(() => optionSet.Parse(new[] {"--packageFormat", "invalidvalue"}));
            Assert.That(ex.Message, Is.EqualTo("Could not convert string `invalidvalue' to type PackageFormat for option `--packageFormat'. Valid values are Zip, Nupkg and Nuget."));
        }
    }
}