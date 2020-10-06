using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Assent;
using FluentAssertions;
using Newtonsoft.Json;
using NSubstitute;
using NUnit.Framework;
using Octopus.Cli.Commands.Releases;
using Octopus.Cli.Tests.Helpers;
using Octopus.Client.Model;
using Octopus.Client.Model.VersionControl;

namespace Octo.Tests.Commands
{
    [TestFixture]
    public class ListReleasesCommandFixture : ApiCommandFixtureBase
    {
        ListReleasesCommand listReleasesCommand;

        [SetUp]
        public void SetUp()
        {
            listReleasesCommand = new ListReleasesCommand(RepositoryFactory, FileSystem, ClientFactory, CommandOutputProvider);
        }

        [Test]
        public async Task ShouldGetListOfReleases()
        {
            const string versionControlledProjectId = "Projects-3";
            Repository.Projects.FindByNames(Arg.Any<IEnumerable<string>>()).Returns(new List<ProjectResource>
            {
                new ProjectResource {Name = "ProjectA", Id = "projectaid"},
                new ProjectResource {Name = "ProjectB", Id = "projectbid"},
                new ProjectResource {Name = "Version controlled project", Id = versionControlledProjectId}
            });

            Repository.Releases.FindMany(Arg.Any<Func<ReleaseResource, bool>>()).Returns(new List<ReleaseResource>
            {
                new ReleaseResource
                {
                    ProjectId = "projectaid",
                    Version = "1.0",
                    Assembled = DateTimeOffset.MinValue,
                    SelectedPackages = new List<SelectedPackage>
                    {
                        new SelectedPackage("Deploy a package", "1.0")
                    },
                    ReleaseNotes = "Release Notes 1"
                },
                new ReleaseResource
                {
                    ProjectId = "projectaid",
                    Version = "2.0",
                    Assembled = DateTimeOffset.MaxValue,
                    ReleaseNotes = "Release Notes 2"
                },
                new ReleaseResource
                {
                    ProjectId = versionControlledProjectId,
                    Version = "1.2.3",
                    Assembled = DateTimeOffset.MaxValue,
                    ReleaseNotes = "Version controlled release notes",
                    VersionControlReference = new VersionControlReferenceResource
                    {
                        GitCommit = "87a072ad2b4a2e9bf2d7ff84d8636a032786394d",
                        GitRef = "main"
                    }
                }
            });

            CommandLineArgs.Add("--project=ProjectA");

            await listReleasesCommand.Execute(CommandLineArgs.ToArray()).ConfigureAwait(false);

            this.Assent(LogOutput.ToString().ScrubApprovalString());
        }

        [Test]
        public async Task JsonFormat_ShouldBeWellFormed()
        {
            Repository.Projects.FindByNames(Arg.Any<IEnumerable<string>>()).Returns(new List<ProjectResource>
            {
                new ProjectResource {Name = "ProjectA", Id = "projectaid"},
                new ProjectResource {Name = "ProjectB", Id = "projectbid"}
            });

            Repository.Releases.FindMany(Arg.Any<Func<ReleaseResource, bool>>()).Returns(new List<ReleaseResource>
            {
                new ReleaseResource
                {
                    ProjectId = "projectaid",
                    Version = "1.0",
                    Assembled = DateTimeOffset.MinValue,
                    ReleaseNotes = "Release Notes 1"
                },
                new ReleaseResource
                {
                    ProjectId = "projectaid",
                    Version = "2.0",
                    Assembled = DateTimeOffset.MaxValue,
                    ReleaseNotes = "Release Notes 2"
                }
            });

            CommandLineArgs.Add("--project=ProjectA");
            CommandLineArgs.Add("--outputFormat=json");

            await listReleasesCommand.Execute(CommandLineArgs.ToArray()).ConfigureAwait(false);

            var logoutput = LogOutput.ToString();
            Console.WriteLine(logoutput);
            JsonConvert.DeserializeObject(logoutput);
            logoutput.Should().Contain("ProjectA");
            logoutput.Should().Contain("1.0");
            logoutput.Should().Contain("2.0");

        }
    }
}
