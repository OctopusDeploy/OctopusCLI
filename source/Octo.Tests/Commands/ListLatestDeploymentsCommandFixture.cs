using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Newtonsoft.Json;
using NSubstitute;
using NUnit.Framework;
using Octopus.Cli.Commands.Deployment;
using Octopus.Client.Model;

namespace Octo.Tests.Commands
{
    [TestFixture]
    public class ListLatestDeploymentsCommandFixture : ApiCommandFixtureBase
    {
        ListLatestDeploymentsCommand listLatestDeploymentsCommands;

        [SetUp]
        public void SetUp()
        {
            listLatestDeploymentsCommands = new ListLatestDeploymentsCommand(RepositoryFactory, FileSystem, ClientFactory, CommandOutputProvider);

            var dashboardResources = new DashboardResource
            {
                Items = new List<DashboardItemResource>
                {
                    new DashboardItemResource
                    {
                        EnvironmentId = "environmentid1",
                        ProjectId = "projectaid",
                        TenantId = "tenantid1",
                        ReleaseId = "Release1",
                        State = TaskState.Success
                    },
                    new DashboardItemResource
                    {
                        EnvironmentId = "environmentid1",
                        ProjectId = "projectaid",
                        TenantId = "tenantid2",
                        ReleaseId = "Release2",
                        State = TaskState.Failed
                    },
                    new DashboardItemResource
                    {
                        EnvironmentId = "environmentid1",
                        ProjectId = "projectaid",
                        TenantId = "tenantid2",
                        ReleaseId = "Release2",
                        State = TaskState.Success
                    }
                },
                Tenants = new List<DashboardTenantResource>
                {
                    new DashboardTenantResource
                    {
                        Id = "tenantid1",
                        Name = "tenant1"
                    }
                }
            };

            Repository.Projects.FindByNames(Arg.Any<IEnumerable<string>>())
                .Returns(Task.FromResult(
                    new List<ProjectResource>
                    {
                        new ProjectResource { Name = "ProjectA", Id = "projectaid" }
                    }));

            Repository.Environments.FindAll()
                .Returns(Task.FromResult(
                    new List<EnvironmentResource>
                    {
                        new EnvironmentResource { Name = "EnvA", Id = "environmentid1" }
                    }));

            Repository.Releases.Get(Arg.Is("Release1")).Returns(new ReleaseResource { Version = "0.0.1" });
            Repository.Releases.Get(Arg.Is("Release2")).Returns(new ReleaseResource { Version = "V1.0.0" });

            Repository.Dashboards.GetDynamicDashboard(Arg.Any<string[]>(), Arg.Any<string[]>()).ReturnsForAnyArgs(dashboardResources);
        }

        [Test]
        public async Task ShouldNotFailWhenTenantIsRemoved()
        {
            CommandLineArgs.Add("--project=ProjectA");

            await listLatestDeploymentsCommands.Execute(CommandLineArgs.ToArray()).ConfigureAwait(false);

            LogLines.Should().Contain(" - Tenant: tenant1");
            LogLines.Should().Contain(" - Tenant: <Removed>");
            LogLines.Should().Contain("   Version: V1.0.0");
        }

        [Test]
        public async Task JsonOutput_ShouldNotFailOnRemovedTenant()
        {
            CommandLineArgs.Add("--project=ProjectA");
            CommandLineArgs.Add("--outputFormat=json");

            await listLatestDeploymentsCommands.Execute(CommandLineArgs.ToArray()).ConfigureAwait(false);

            var logoutput = LogOutput.ToString();
            JsonConvert.DeserializeObject(logoutput);
            logoutput.Should().Contain("tenant1");
            logoutput.Should().Contain("<Removed>");
            logoutput.Should().Contain("V1.0.0");
        }

        [Test]
        public async Task ShouldIncludeFailedDeploymentsWhenGetLastSuccessfulDeploymentFlagNotSet()
        {
            CommandLineArgs.Add("--project=ProjectA");

            await listLatestDeploymentsCommands.Execute(CommandLineArgs.ToArray()).ConfigureAwait(false);
            
            LogLines.Should().Contain("   State: Success");
            LogLines.Should().Contain("   State: Failed");
        }

        [Test]
        public async Task ShouldNotIncludeFailedDeploymentsWhenGetLastSuccessfulDeploymentFlagSet()
        {
            CommandLineArgs.Add("--project=ProjectA");
            CommandLineArgs.Add("--getLastSuccessful");

            await listLatestDeploymentsCommands.Execute(CommandLineArgs.ToArray()).ConfigureAwait(false);
            
            LogLines.Should().Contain("   State: Success");
            LogLines.Should().NotContain("   State: Failed");
        }

        [Test]
        public async Task JsonOutput_ShouldIncludeFailedDeploymentsWhenGetLastSuccessfulDeploymentFlagNotSet()
        {
            CommandLineArgs.Add("--project=ProjectA");
            CommandLineArgs.Add("--outputFormat=json");

            await listLatestDeploymentsCommands.Execute(CommandLineArgs.ToArray()).ConfigureAwait(false);

            var logoutput = LogOutput.ToString();
            JsonConvert.DeserializeObject(logoutput);
            logoutput.Should().Contain("Success");
            logoutput.Should().Contain("Failed");
        }

        [Test]
        public async Task JsonOutput_ShouldNotIncludeFailedDeploymentsWhenGetLastSuccessfulDeploymentFlagSet()
        {
            CommandLineArgs.Add("--project=ProjectA");
            CommandLineArgs.Add("--outputFormat=json");
            CommandLineArgs.Add("--getLastSuccessful");

            await listLatestDeploymentsCommands.Execute(CommandLineArgs.ToArray()).ConfigureAwait(false);

            var logoutput = LogOutput.ToString();
            JsonConvert.DeserializeObject(logoutput);
            logoutput.Should().Contain("Success");
            logoutput.Should().NotContain("Failed");
        }
    }
}
