using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Cli.Commands.Releases;
using Octopus.Cli.Infrastructure;
using Octopus.Cli.Tests.Helpers;
using Octopus.Cli.Util;

namespace Octo.Tests.Commands
{
    public class CreateReleaseCommandFixture : ApiCommandFixtureBase
    {
        CreateReleaseCommand createReleaseCommand;
        IPackageVersionResolver versionResolver;
        IReleasePlanBuilder releasePlanBuilder;

        [SetUp]
        public void SetUp()
        {
            versionResolver = Substitute.For<IPackageVersionResolver>();
            releasePlanBuilder = Substitute.For<IReleasePlanBuilder>();           
        }

        [Test]
        public void ShouldLoadOptionsFromFile()
        {
            createReleaseCommand = new CreateReleaseCommand(RepositoryFactory, new OctopusPhysicalFileSystem(Log), versionResolver, releasePlanBuilder, ClientFactory, CommandOutputProvider);

            Assert.Throws<CouldNotFindException>(delegate {
                createReleaseCommand.Execute("--configfile=Commands/Resources/CreateRelease.config.txt");
            });
            
            Assert.AreEqual("Test Project", createReleaseCommand.ProjectNameOrId);
            Assert.AreEqual("1.0.0", createReleaseCommand.VersionNumber);
            Assert.AreEqual("Test config file.", createReleaseCommand.ReleaseNotes);
        }
        
        [Test]
        public void ShouldThrowForBadTag()
        {
            createReleaseCommand = new CreateReleaseCommand(RepositoryFactory, new OctopusPhysicalFileSystem(Log), versionResolver, releasePlanBuilder, ClientFactory, CommandOutputProvider);
            
            CommandLineArgs.Add("--server=https://test-server-url/api/");
            CommandLineArgs.Add("--apikey=API-test");
            CommandLineArgs.Add("--project=Test Project");
            CommandLineArgs.Add("--releaseNumber=1.0.0");
            CommandLineArgs.Add("--tenantTag=badset/badtag");
            CommandLineArgs.Add($"--deployto={ValidEnvironment}");

            var ex = Assert.ThrowsAsync<CommandException>(() => createReleaseCommand.Execute(CommandLineArgs.ToArray()));
            ex.Message.Should().Be("Unable to find matching tag from canonical tag name `badset/badtag`");
        }
        
        [Test]
        public void ShouldThrowForBadTenant()
        {
            createReleaseCommand = new CreateReleaseCommand(RepositoryFactory, new OctopusPhysicalFileSystem(Log), versionResolver, releasePlanBuilder, ClientFactory, CommandOutputProvider);
            
            CommandLineArgs.Add("--server=https://test-server-url/api/");
            CommandLineArgs.Add("--apikey=API-test");
            CommandLineArgs.Add("--project=Test Project");
            CommandLineArgs.Add("--releaseNumber=1.0.0");
            CommandLineArgs.Add("--tenant=badTenant");
            CommandLineArgs.Add($"--deployto={ValidEnvironment}");

            var ex = Assert.ThrowsAsync<CommandException>(() => createReleaseCommand.Execute(CommandLineArgs.ToArray()));
            ex.Message.Should().Be("The tenant \"badTenant\" does not exist or the account does not have access.");
        }
        
        [Test]
        public void ShouldThrowForBadMachine()
        {
            createReleaseCommand = new CreateReleaseCommand(RepositoryFactory, new OctopusPhysicalFileSystem(Log), versionResolver, releasePlanBuilder, ClientFactory, CommandOutputProvider);
            
            CommandLineArgs.Add("--server=https://test-server-url/api/");
            CommandLineArgs.Add("--apikey=API-test");
            CommandLineArgs.Add("--project=Test Project");
            CommandLineArgs.Add("--releaseNumber=1.0.0");
            CommandLineArgs.Add("--specificmachines=badMach");
            CommandLineArgs.Add($"--deployto={ValidEnvironment}");

            var ex = Assert.ThrowsAsync<CommandException>(() => createReleaseCommand.Execute(CommandLineArgs.ToArray()));
            ex.Message.Should().Be("The following specific machines could not be found: badMach");
        }
        
                
        [Test]
        public void ShouldThrowForBadEnvironment()
        {
            createReleaseCommand = new CreateReleaseCommand(RepositoryFactory, new OctopusPhysicalFileSystem(Log), versionResolver, releasePlanBuilder, ClientFactory, CommandOutputProvider);
            
            CommandLineArgs.Add("--server=https://test-server-url/api/");
            CommandLineArgs.Add("--apikey=API-test");
            CommandLineArgs.Add("--project=Test Project");
            CommandLineArgs.Add("--releaseNumber=1.0.0");
            CommandLineArgs.Add("--deployto=badEnv");

            var ex = Assert.ThrowsAsync<CommandException>(() => createReleaseCommand.Execute(CommandLineArgs.ToArray()));
            ex.Message.Should().Be("The environment \"badEnv\" does not exist or the account does not have access.");
        }
        
    }
}