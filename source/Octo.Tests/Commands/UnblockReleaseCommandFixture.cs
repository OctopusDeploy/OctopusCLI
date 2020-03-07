using System;
using System.Reflection;
using System.Threading.Tasks;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Cli.Commands;
using Octopus.Cli.Commands.Releases;
using Octopus.Cli.Infrastructure;
using Octopus.Client.Exceptions;
using Octopus.Client.Model;
using Octopus.Client.Serialization;

namespace Octo.Tests.Commands
{
    [TestFixture]
    public class UnblockReleaseCommandFixture : ApiCommandFixtureBase
    {
        UnblockReleaseCommand unblockReleaseCommand;
        ProjectResource projectResource;
        ReleaseResource releaseResource;

        [SetUp]
        public void SetUp()
        {
            unblockReleaseCommand = new UnblockReleaseCommand(ClientFactory, RepositoryFactory, FileSystem, CommandOutputProvider);

            projectResource = new ProjectResource
            {
                Id = "Projects-1",
                Name = "Test Project",
                SpaceId = "Spaces-1"
            };
            Repository.Projects.FindByName(projectResource.Name).Returns(projectResource);

            releaseResource = new ReleaseResource
            {
                Id = "Releases-1",
                ProjectId = projectResource.Id,
                SpaceId = projectResource.SpaceId,
                Version = "0.0.1"
            };
            Repository.Projects.GetReleaseByVersion(projectResource, releaseResource.Version).Returns(releaseResource);
        }

        [Test]
        public void ShouldBeSubClassOfCorrectBaseClass()
        {
            typeof(UnblockReleaseCommand).IsSubclassOf(typeof(ApiCommand)).Should().BeTrue();
        }

        [Test]
        public void ShouldImplementCorrectInterface()
        {
            typeof(ISupportFormattedOutput).IsAssignableFrom(typeof(UnblockReleaseCommand)).Should().BeTrue();
        }

        [Test]
        public void ShouldBeAttachedWithCorrectAttribute()
        {
            var attribute = typeof(UnblockReleaseCommand).GetCustomAttribute<CommandAttribute>();

            attribute.Should().NotBeNull();

            attribute.Name.Should().Be("unblock-release");

            attribute.Aliases.Length.Should().Be(1);
            attribute.Aliases.Should().Contain("resume-release-progression");
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("    ")]
        public void ShouldValidateProjectIdOrNameParameterForMissing(string projectIdOrName)
        {
            CommandLineArgs.Add($"--project={projectIdOrName}");
            CommandLineArgs.Add($"--version={releaseResource.Version}");

            Func<Task> exec = () => unblockReleaseCommand.Execute(CommandLineArgs.ToArray());
            exec.ShouldThrow<CommandException>()
                .WithMessage("Please specify a project name or ID using the parameter: --project=XYZ");
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("    ")]
        public void ShouldValidateReleaseVersionNumberParameterForMissing(string releaseVersionNumber)
        {
            CommandLineArgs.Add($"--project={projectResource.Name}");
            CommandLineArgs.Add($"--version={releaseVersionNumber}");

            Func<Task> exec = () => unblockReleaseCommand.Execute(CommandLineArgs.ToArray());
            exec.ShouldThrow<CommandException>()
                .WithMessage("Please specify a release version number using the version parameter: --version=1.0.5");
        }

        [TestCase("abcdef666")]
        [TestCase("a.23.c")]
        [TestCase("1.f.b")]
        public void ShouldValidateReleaseVersionNumberParameterForFormat(string releaseVersionNumber)
        {
            CommandLineArgs.Add($"--project={projectResource.Name}");
            CommandLineArgs.Add($"--version={releaseVersionNumber}");

            Func<Task> exec = () => unblockReleaseCommand.Execute(CommandLineArgs.ToArray());
            exec.ShouldThrow<CommandException>()
                .WithMessage("Please provide a valid release version format, you can refer to https://semver.org/ for a valid format: --version=1.0.5");
        }

        [TestCase("version")]
        [TestCase("releaseNumber")]
        public async Task ShouldSupportBothReleaseNumberAndVersionArgForReleaseVersionNumberProperty(string argName)
        {
            CommandLineArgs.Add($"--project={projectResource.Name}");
            CommandLineArgs.Add($"--{argName}={releaseResource.Version}");

            await unblockReleaseCommand.Execute(CommandLineArgs.ToArray());

            unblockReleaseCommand.ReleaseVersionNumber.Should().Be(releaseResource.Version);
            await Repository.Projects.Received(1).GetReleaseByVersion(projectResource, unblockReleaseCommand.ReleaseVersionNumber);
        }

        [Test]
        public void ShouldThrowCorrectException_WhenReleaseNotFound()
        {
            Repository.Projects.GetReleaseByVersion(projectResource, releaseResource.Version).Returns((ReleaseResource)null);

            CommandLineArgs.Add($"--project={projectResource.Name}");
            CommandLineArgs.Add($"--version={releaseResource.Version}");

            Func<Task> exec = () => unblockReleaseCommand.Execute(CommandLineArgs.ToArray());

            exec.ShouldThrow<OctopusResourceNotFoundException>();
        }

        [Test]
        public async Task ShouldUnblockReleaseCorrectly()
        {
            CommandLineArgs.Add($"--project={projectResource.Name}");
            CommandLineArgs.Add($"--version={releaseResource.Version}");

            await unblockReleaseCommand.Execute(CommandLineArgs.ToArray());

            unblockReleaseCommand.ProjectNameOrId.Should().Be(projectResource.Name);
            unblockReleaseCommand.ReleaseVersionNumber.Should().Be(releaseResource.Version);

            await Repository.Projects.Received(1).FindByName(projectResource.Name);
            await Repository.Projects.Received(1).GetReleaseByVersion(projectResource, releaseResource.Version);
            await Repository.Defects.Received(1).ResolveDefect(releaseResource);
        }

        [Test]
        public void ShouldPrintDefaultOutputCorrectly()
        {
            unblockReleaseCommand.PrintDefaultOutput();

            LogOutput.ToString().Trim().Should().Be("Unblocked successfully.");
        }

        [Test]
        public async Task ShouldPrintJsonOutputCorrectly()
        {
            CommandLineArgs.Add($"--project={projectResource.Name}");
            CommandLineArgs.Add($"--version={releaseResource.Version}");
            CommandLineArgs.Add("--outputFormat=json");

            await unblockReleaseCommand.Execute(CommandLineArgs.ToArray());

            var logOutput = LogOutput.ToString().Trim();
            var expectedLogOutput = JsonSerialization.SerializeObject(new
            {
                projectResource.SpaceId,
                Project = new { projectResource.Id, projectResource.Name },
                Release = new { releaseResource.Id, releaseResource.Version, IsPreventedFromProgressing = false }
            });

            logOutput.Should().Be(expectedLogOutput);
        }
    }
}