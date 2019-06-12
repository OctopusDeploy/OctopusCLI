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
            
            Assert.AreEqual("Test Project", createReleaseCommand.ProjectName);
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
            CommandLineArgs.Add("--tenantTag=bad");
            CommandLineArgs.Add($"--deployto={ValidEnvironment}");

            Func<Task> func = async () => await createReleaseCommand.Execute(CommandLineArgs.ToArray());
            func.ShouldThrow<CommandException>()
                .Where(ex => Regex.IsMatch(ex.Message, @"\btag\b", RegexOptions.IgnoreCase));
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

            Func<Task> func = async () => await createReleaseCommand.Execute(CommandLineArgs.ToArray());
            func.ShouldThrow<CommandException>()
                .Where(ex => Regex.IsMatch(ex.Message, @"tenant.*badTenant", RegexOptions.IgnoreCase));
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

            Func<Task> func = async () => await createReleaseCommand.Execute(CommandLineArgs.ToArray());
            func.ShouldThrow<CommandException>()
                .Where(ex => Regex.IsMatch(ex.Message, @"machine.*badMach", RegexOptions.IgnoreCase));
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

            Func<Task> func = async () => await createReleaseCommand.Execute(CommandLineArgs.ToArray());
            func.ShouldThrow<CommandException>().Where(ex =>
                Regex.IsMatch(ex.Message, @"environment.*badEnv", RegexOptions.IgnoreCase));
        }
        
    }
}