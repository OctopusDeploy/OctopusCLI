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
    public class PreventReleaseProgressionCommandFixture : ApiCommandFixtureBase
    {
        PreventReleaseProgressionCommand preventReleaseProgressionCommand;
        ProjectResource projectResource;
        ReleaseResource releaseResource;
        const string ReasonToPrevent = "Test Prevention";

        [SetUp]
        public void SetUp()
        {
            preventReleaseProgressionCommand = new PreventReleaseProgressionCommand(ClientFactory, RepositoryFactory, FileSystem, CommandOutputProvider);

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
            typeof(PreventReleaseProgressionCommand).IsSubclassOf(typeof(ApiCommand)).Should().BeTrue();
        }

        [Test]
        public void ShouldImplementCorrectInterface()
        {
            typeof(PreventReleaseProgressionCommand).IsAssignableTo<ISupportFormattedOutput>().Should().BeTrue();
        }

        [Test]
        public void ShouldBeAttachedWithCorrectAttribute()
        {
            var attribute = typeof(PreventReleaseProgressionCommand).GetCustomAttribute<CommandAttribute>();

            attribute.Should().NotBeNull();

            attribute.Name.Should().Be("prevent-releaseprogression");
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("    ")]
        public void ShouldValidateProjectIdOrNameParameterForMissing(string projectIdOrName)
        {
            CommandLineArgs.Add($"--project={projectIdOrName}");
            CommandLineArgs.Add($"--version={releaseResource.Version}");
            CommandLineArgs.Add($"--reason={ReasonToPrevent}");

            Func<Task> exec = () => preventReleaseProgressionCommand.Execute(CommandLineArgs.ToArray());
            exec.ShouldThrow<CommandException>()
                .WithMessage("Please specify a project name or ID using the parameter: --project=XYZ");
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("    ")]
        public void ShouldValidateReasonToPreventParameterForMissing(string reasonToPrevent)
        {
            CommandLineArgs.Add($"--project={projectResource.Name}");
            CommandLineArgs.Add($"--version={releaseResource.Version}");
            CommandLineArgs.Add($"--reason={reasonToPrevent}");

            Func<Task> exec = () => preventReleaseProgressionCommand.Execute(CommandLineArgs.ToArray());
            exec.ShouldThrow<CommandException>()
                .WithMessage("Please specify a reason why you would like to prevent this release from progressing to next phase using the reason parameter: --reason=Contract Tests Failed");
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("    ")]
        public void ShouldValidateReleaseVersionNumberParameterForMissing(string releaseVersionNumber)
        {
            CommandLineArgs.Add($"--project={projectResource.Name}");
            CommandLineArgs.Add($"--reason={ReasonToPrevent}");
            CommandLineArgs.Add($"--version={releaseVersionNumber}");

            Func<Task> exec = () => preventReleaseProgressionCommand.Execute(CommandLineArgs.ToArray());
            exec.ShouldThrow<CommandException>()
                .WithMessage("Please specify a release version number using the version parameter: --version=1.0.5");
        }

        [TestCase("999999999999999999999999999999999999999999999999999999"), Description("Number larger than an int")]
        public void ShouldValidateReleaseVersionNumberParameterForFormat(string releaseVersionNumber)
        {
            CommandLineArgs.Add($"--project={projectResource.Name}");
            CommandLineArgs.Add($"--reason={ReasonToPrevent}");
            CommandLineArgs.Add($"--version={releaseVersionNumber}");

            Func<Task> exec = () => preventReleaseProgressionCommand.Execute(CommandLineArgs.ToArray());
            exec.ShouldThrow<CommandException>()
                .WithMessage("Please provide a valid release version format: --version=1.0.5");
        }

        [TestCase("version")]
        [TestCase("releaseNumber")]
        public async Task ShouldSupportBothReleaseNumberAndVersionArgForReleaseVersionNumberProperty(string argName)
        {
            CommandLineArgs.Add($"--project={projectResource.Name}");
            CommandLineArgs.Add($"--{argName}={releaseResource.Version}");
            CommandLineArgs.Add($"--reason={ReasonToPrevent}");

            await preventReleaseProgressionCommand.Execute(CommandLineArgs.ToArray());

            preventReleaseProgressionCommand.ReleaseVersionNumber.Should().Be(releaseResource.Version);
            await Repository.Projects.Received(1).GetReleaseByVersion(projectResource, preventReleaseProgressionCommand.ReleaseVersionNumber);
        }

        [Test]
        public void ShouldThrowCorrectException_WhenReleaseNotFound()
        {
            Repository.Projects.GetReleaseByVersion(projectResource, releaseResource.Version).Returns((ReleaseResource)null);

            CommandLineArgs.Add($"--project={projectResource.Name}");
            CommandLineArgs.Add($"--version={releaseResource.Version}");
            CommandLineArgs.Add($"--reason={ReasonToPrevent}");

            Func<Task> exec = () => preventReleaseProgressionCommand.Execute(CommandLineArgs.ToArray());

            exec.ShouldThrow<OctopusResourceNotFoundException>();
        }

        [Test] 
        public async Task ShouldPreventReleaseProgressionCorrectly_WhenReleaseProgressionIsNotYetPrevented()
        {
            CommandLineArgs.Add($"--project={projectResource.Name}");
            CommandLineArgs.Add($"--version={releaseResource.Version}");
            CommandLineArgs.Add($"--reason={ReasonToPrevent}");

            await preventReleaseProgressionCommand.Execute(CommandLineArgs.ToArray());

            preventReleaseProgressionCommand.ProjectNameOrId.Should().Be(projectResource.Name);
            preventReleaseProgressionCommand.ReleaseVersionNumber.Should().Be(releaseResource.Version);
            preventReleaseProgressionCommand.ReasonToPrevent.Should().Be(ReasonToPrevent);

            await Repository.Projects.Received(1).FindByName(projectResource.Name);
            await Repository.Projects.Received(1).GetReleaseByVersion(projectResource, releaseResource.Version);
            await Repository.Defects.Received(1).GetDefects(releaseResource);
            await Repository.Defects.Received(1).RaiseDefect(releaseResource, ReasonToPrevent);
        }

        [Test]
        public async Task ShouldPreventReleaseProgressionCorrectly_WhenReleaseProgressionIsAlreadyPrevented()
        {
            var defects = new[] { new DefectResource("Test Defect", DefectStatus.Unresolved), new DefectResource("Test Defect", DefectStatus.Resolved) };
            Repository.Defects.GetDefects(releaseResource).Returns(new ResourceCollection<DefectResource>(defects, new LinkCollection()));

            CommandLineArgs.Add($"--project={projectResource.Name}");
            CommandLineArgs.Add($"--version={releaseResource.Version}");
            CommandLineArgs.Add($"--reason={ReasonToPrevent}");

            await preventReleaseProgressionCommand.Execute(CommandLineArgs.ToArray());

            preventReleaseProgressionCommand.ProjectNameOrId.Should().Be(projectResource.Name);
            preventReleaseProgressionCommand.ReleaseVersionNumber.Should().Be(releaseResource.Version);
            preventReleaseProgressionCommand.ReasonToPrevent.Should().Be(ReasonToPrevent);

            await Repository.Projects.Received(1).FindByName(projectResource.Name);
            await Repository.Projects.Received(1).GetReleaseByVersion(projectResource, releaseResource.Version);
            await Repository.Defects.Received(1).GetDefects(releaseResource);
            await Repository.Defects.DidNotReceive().RaiseDefect(releaseResource, ReasonToPrevent);
        }

        [Test]
        public void ShouldPrintDefaultOutputCorrectly()
        {
            preventReleaseProgressionCommand.PrintDefaultOutput();

            LogOutput.ToString().Trim().Should().Be("Prevented successfully.");
        }

        [Test]
        public async Task ShouldPrintJsonOutputCorrectly()
        {
            CommandLineArgs.Add($"--project={projectResource.Name}");
            CommandLineArgs.Add($"--version={releaseResource.Version}");
            CommandLineArgs.Add($"--reason={ReasonToPrevent}");
            CommandLineArgs.Add("--outputFormat=json");

            await preventReleaseProgressionCommand.Execute(CommandLineArgs.ToArray());

            var logOutput = LogOutput.ToString().Trim();
            var expectedLogOutput = JsonSerialization.SerializeObject(new
            {
                projectResource.SpaceId,
                Project = new { projectResource.Id, projectResource.Name },
                Release = new { releaseResource.Id, releaseResource.Version, IsPreventedFromProgressing = true },
                ReasonToPrevent
            });

            logOutput.Should().Be(expectedLogOutput);
        }
    }
}
