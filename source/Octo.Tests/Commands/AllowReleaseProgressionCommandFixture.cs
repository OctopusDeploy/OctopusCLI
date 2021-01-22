using System;
using System.Reflection;
using System.Threading.Tasks;
using Autofac;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Cli.Commands;
using Octopus.Cli.Commands.Releases;
using Octopus.Cli.Infrastructure;
using Octopus.Client.Exceptions;
using Octopus.Client.Extensibility;
using Octopus.Client.Model;
using Octopus.Client.Serialization;

namespace Octo.Tests.Commands
{
    [TestFixture]
    public class AllowReleaseProgressionCommandFixture : ApiCommandFixtureBase
    {
        AllowReleaseProgressionCommand allowReleaseProgressionCommand;
        ProjectResource projectResource;
        ReleaseResource releaseResource;

        [SetUp]
        public void SetUp()
        {
            allowReleaseProgressionCommand = new AllowReleaseProgressionCommand(ClientFactory, RepositoryFactory, FileSystem, CommandOutputProvider);

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

            var defects = new[] { new DefectResource("Test Defect", DefectStatus.Resolved) };
            Repository.Defects.GetDefects(releaseResource).Returns(new ResourceCollection<DefectResource>(defects, new LinkCollection()));
        }

        [Test]
        public void ShouldBeSubClassOfCorrectBaseClass()
        {
            typeof(AllowReleaseProgressionCommand).IsSubclassOf(typeof(ApiCommand)).Should().BeTrue();
        }

        [Test]
        public void ShouldImplementCorrectInterface()
        {
            typeof(AllowReleaseProgressionCommand).IsAssignableTo<ISupportFormattedOutput>().Should().BeTrue();
        }

        [Test]
        public void ShouldBeAttachedWithCorrectAttribute()
        {
            var attribute = typeof(AllowReleaseProgressionCommand).GetCustomAttribute<CommandAttribute>();

            attribute.Should().NotBeNull();

            attribute.Name.Should().Be("allow-releaseprogression");
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("    ")]
        public void ShouldValidateProjectIdOrNameParameterForMissing(string projectIdOrName)
        {
            CommandLineArgs.Add($"--project={projectIdOrName}");
            CommandLineArgs.Add($"--version={releaseResource.Version}");

            Func<Task> exec = () => allowReleaseProgressionCommand.Execute(CommandLineArgs.ToArray());
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

            Func<Task> exec = () => allowReleaseProgressionCommand.Execute(CommandLineArgs.ToArray());
            exec.ShouldThrow<CommandException>()
                .WithMessage("Please specify a release version number using the version parameter: --version=1.0.5");
        }

        [TestCase("9999999999999999999999999999999999999"), Description("Version number larger than an int")]
        public void ShouldValidateReleaseVersionNumberParameterForFormat(string releaseVersionNumber)
        {
            CommandLineArgs.Add($"--project={projectResource.Name}");
            CommandLineArgs.Add($"--version={releaseVersionNumber}");

            Func<Task> exec = () => allowReleaseProgressionCommand.Execute(CommandLineArgs.ToArray());
            exec.ShouldThrow<CommandException>()
                .WithMessage("Please provide a valid release version format: --version=1.0.5");
        }

        [TestCase("version")]
        [TestCase("releaseNumber")]
        public async Task ShouldSupportBothReleaseNumberAndVersionArgForReleaseVersionNumberProperty(string argName)
        {
            CommandLineArgs.Add($"--project={projectResource.Name}");
            CommandLineArgs.Add($"--{argName}={releaseResource.Version}");

            await allowReleaseProgressionCommand.Execute(CommandLineArgs.ToArray());

            allowReleaseProgressionCommand.ReleaseVersionNumber.Should().Be(releaseResource.Version);
            await Repository.Projects.Received(1).GetReleaseByVersion(projectResource, allowReleaseProgressionCommand.ReleaseVersionNumber);
        }

        [Test]
        public void ShouldThrowCorrectException_WhenReleaseNotFound()
        {
            Repository.Projects.GetReleaseByVersion(projectResource, releaseResource.Version).Returns((ReleaseResource)null);

            CommandLineArgs.Add($"--project={projectResource.Name}");
            CommandLineArgs.Add($"--version={releaseResource.Version}");

            Func<Task> exec = () => allowReleaseProgressionCommand.Execute(CommandLineArgs.ToArray());

            exec.ShouldThrow<OctopusResourceNotFoundException>();
        }

        [Test]
        public async Task ShouldAllowReleaseProgressionCorrectly_WhenReleaseProgressionIsNotYetAllowed()
        {
            var defects = new[] { new DefectResource("Test Defect", DefectStatus.Unresolved), new DefectResource("Test Defect", DefectStatus.Resolved) };
            Repository.Defects.GetDefects(releaseResource).Returns(new ResourceCollection<DefectResource>(defects, new LinkCollection()));

            CommandLineArgs.Add($"--project={projectResource.Name}");
            CommandLineArgs.Add($"--version={releaseResource.Version}");

            await allowReleaseProgressionCommand.Execute(CommandLineArgs.ToArray());

            allowReleaseProgressionCommand.ProjectNameOrId.Should().Be(projectResource.Name);
            allowReleaseProgressionCommand.ReleaseVersionNumber.Should().Be(releaseResource.Version);

            await Repository.Projects.Received(1).FindByName(projectResource.Name);
            await Repository.Projects.Received(1).GetReleaseByVersion(projectResource, releaseResource.Version);
            await Repository.Defects.Received(1).GetDefects(releaseResource);
            await Repository.Defects.Received(1).ResolveDefect(releaseResource);
        }

        [Test]
        public async Task ShouldAllowReleaseProgressionCorrectly_WhenReleaseProgressionIsAlreadyAllowed()
        {
            CommandLineArgs.Add($"--project={projectResource.Name}");
            CommandLineArgs.Add($"--version={releaseResource.Version}");

            await allowReleaseProgressionCommand.Execute(CommandLineArgs.ToArray());

            allowReleaseProgressionCommand.ProjectNameOrId.Should().Be(projectResource.Name);
            allowReleaseProgressionCommand.ReleaseVersionNumber.Should().Be(releaseResource.Version);

            await Repository.Projects.Received(1).FindByName(projectResource.Name);
            await Repository.Projects.Received(1).GetReleaseByVersion(projectResource, releaseResource.Version);
            await Repository.Defects.Received(1).GetDefects(releaseResource);
            await Repository.Defects.DidNotReceive().ResolveDefect(releaseResource);
        }

        [Test]
        public void ShouldPrintDefaultOutputCorrectly()
        {
            allowReleaseProgressionCommand.PrintDefaultOutput();

            LogOutput.ToString().Trim().Should().Be("Allowed successfully.");
        }

        [Test]
        public async Task ShouldPrintJsonOutputCorrectly()
        {
            CommandLineArgs.Add($"--project={projectResource.Name}");
            CommandLineArgs.Add($"--version={releaseResource.Version}");
            CommandLineArgs.Add("--outputFormat=json");

            await allowReleaseProgressionCommand.Execute(CommandLineArgs.ToArray());

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