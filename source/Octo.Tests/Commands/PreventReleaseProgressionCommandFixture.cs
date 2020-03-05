﻿using System;
using System.Threading.Tasks;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Cli.Commands.Releases;
using Octopus.Cli.Infrastructure;
using Octopus.Cli.Util;
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
            Repository.Projects.FindByNameOrIdOrFail(projectResource.Name).Returns(projectResource);

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
        public void ShouldValidateProjectIdOrNameParameter()
        {
            CommandLineArgs.Add($"--version={releaseResource.Version}");
            CommandLineArgs.Add($"--reason={ReasonToPrevent}");

            Func<Task> exec = () => preventReleaseProgressionCommand.Execute(CommandLineArgs.ToArray());
            exec.ShouldThrow<CommandException>()
                .WithMessage("Please specify a project name or ID using the parameter: --project=XYZ");
        }

        [Test]
        public void ShouldValidateReasonToPreventParameter()
        {
            CommandLineArgs.Add($"--project={projectResource.Name}");
            CommandLineArgs.Add($"--version={releaseResource.Version}");

            Func<Task> exec = () => preventReleaseProgressionCommand.Execute(CommandLineArgs.ToArray());
            exec.ShouldThrow<CommandException>()
                .WithMessage("Please specify a reason why you would like to prevent this release from progressing to next phase");
        }

        [Test]
        public void ShouldValidateReleaseVersionNumberParameter()
        {
            CommandLineArgs.Add($"--project={projectResource.Name}");
            CommandLineArgs.Add($"--reason={ReasonToPrevent}");

            Func<Task> exec = () => preventReleaseProgressionCommand.Execute(CommandLineArgs.ToArray());
            exec.ShouldThrow<CommandException>()
                .WithMessage("Please specify a release version");
        }

        [Test]
        public async Task ShouldPreventReleaseProgressionCorrectly()
        {
            CommandLineArgs.Add($"--project={projectResource.Name}");
            CommandLineArgs.Add($"--version={releaseResource.Version}");
            CommandLineArgs.Add($"--reason={ReasonToPrevent}");

            await preventReleaseProgressionCommand.Execute(CommandLineArgs.ToArray());
            
            await Repository.Defects.Received(1).RaiseDefect(releaseResource, ReasonToPrevent);
        }

        [Test]
        public void ShouldPrintDefaultOutputCorrectly()
        {
            preventReleaseProgressionCommand.PrintDefaultOutput();
            
            LogOutput.ToString().Trim().Should().Be("Prevented Successfully.");
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
                Release = new { releaseResource.Id, releaseResource.Version },
                ReasonToPrevent
            });

            logOutput.Should().Be(expectedLogOutput);
        }
    }
}
