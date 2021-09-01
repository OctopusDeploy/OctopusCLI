using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Octopus.Cli.Model;
using Octopus.Cli.Repositories;
using Octopus.Cli.Util;
using Octopus.Client;
using Octopus.Client.Model;
using Octopus.CommandLine;
using Octopus.CommandLine.Commands;

namespace Octopus.Cli.Commands.Deployment
{
    [Command("list-latestdeployments", Description = "Lists the releases last-deployed in each environment.")]
    public class ListLatestDeploymentsCommand : ApiCommand, ISupportFormattedOutput
    {
        readonly HashSet<string> environments = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        readonly HashSet<string> projects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        IDictionary<string, ProjectResource> projectsById;
        string[] projectsFilter;
        IDictionary<string, string> environmentsById;
        string[] environmentsFilter;
        DashboardResource dashboard;
        Dictionary<string, string> tenantsById;
        Dictionary<DashboardItemResource, DeploymentRelatedResources> dashboardRelatedResourceses;

        public ListLatestDeploymentsCommand(IOctopusAsyncRepositoryFactory repositoryFactory, IOctopusFileSystem fileSystem, IOctopusClientFactory clientFactory, ICommandOutputProvider commandOutputProvider)
            : base(clientFactory, repositoryFactory, fileSystem, commandOutputProvider)
        {
            var options = Options.For("Listing");
            options.Add<string>("project=", "Name of a project to filter by. Can be specified many times.", v => projects.Add(v), allowsMultiple: true);
            options.Add<string>("environment=", "Name of an environment to filter by. Can be specified many times.", v => environments.Add(v), allowsMultiple: true);
        }

        async Task<IDictionary<string, ProjectResource>> LoadProjects()
        {
            commandOutputProvider.Debug("Loading projects...");
            var projectQuery = projects.Any()
                ? Repository.Projects.FindByNames(projects.ToArray())
                : Repository.Projects.FindAll();

            var projectResources = await projectQuery.ConfigureAwait(false);

            var missingProjects = projects.Except(projectResources.Select(e => e.Name), StringComparer.OrdinalIgnoreCase).ToArray();

            if (missingProjects.Any())
                throw new CommandException("Could not find projects: " + string.Join(",", missingProjects));

            return projectResources.ToDictionary(p => p.Id, p => p);
        }

        async Task<IDictionary<string, string>> LoadEnvironments()
        {
            commandOutputProvider.Debug("Loading environments...");
            var environmentQuery = environments.Any()
                ? Repository.Environments.FindByNames(environments.ToArray())
                : Repository.Environments.FindAll();

            var environmentResources = await environmentQuery.ConfigureAwait(false);

            var missingEnvironments = environments.Except(environmentResources.Select(e => e.Name), StringComparer.OrdinalIgnoreCase).ToArray();

            if (missingEnvironments.Any())
                throw new CommandException("Could not find environments: " + string.Join(",", missingEnvironments));

            return environmentResources.ToDictionary(p => p.Id, p => p.Name);
        }

        static void LogDeploymentInfo(ICommandOutputProvider commandOutputProvider,
            DashboardItemResource dashboardItem,
            ReleaseResource release,
            string channelName,
            IDictionary<string, string> environmentsById,
            IDictionary<string, ProjectResource> projectedById,
            IDictionary<string, string> tenantsById)
        {
            var nameOfDeploymentEnvironment = environmentsById[dashboardItem.EnvironmentId];
            var nameOfDeploymentProject = projectedById[dashboardItem.ProjectId].Name;

            commandOutputProvider.Information(" - Project: {Project:l}", nameOfDeploymentProject);
            commandOutputProvider.Information(" - Environment: {Environment:l}", nameOfDeploymentEnvironment);
            if (!string.IsNullOrEmpty(dashboardItem.TenantId))
            {
                var nameOfDeploymentTenant = GetNameOfDeploymentTenant(tenantsById, dashboardItem.TenantId);
                commandOutputProvider.Information(" - Tenant: {Tenant:l}", nameOfDeploymentTenant);
            }

            if (channelName != null)
                commandOutputProvider.Information(" - Channel: {Channel:l}", channelName);

            commandOutputProvider.Information("   Date: {$Date:l}", dashboardItem.QueueTime);
            commandOutputProvider.Information("   Duration: {Duration:l}", dashboardItem.Duration);

            if (dashboardItem.State == TaskState.Failed)
                commandOutputProvider.Error("   State: {$State:l}", dashboardItem.State);
            else
                commandOutputProvider.Information("   State: {$State:l}", dashboardItem.State);

            commandOutputProvider.Information("   Version: {Version:l}", release.Version);
            commandOutputProvider.Information("   Assembled: {$Assembled:l}", release.Assembled);
            commandOutputProvider.Information("   Package Versions: {PackageVersion:l}", GetPackageVersionsAsString(release.SelectedPackages));
            commandOutputProvider.Information("   Release Notes: {ReleaseNotes:l}", GetReleaseNotes(release));
            commandOutputProvider.Information(string.Empty);
        }

        public async Task Request()
        {
            projectsById = await LoadProjects().ConfigureAwait(false);
            projectsFilter = projectsById.Keys.ToArray();

            environmentsById = await LoadEnvironments().ConfigureAwait(false);
            environmentsFilter = environmentsById.Keys.ToArray();

            commandOutputProvider.Debug("Loading dashboard...");

            dashboard = await Repository.Dashboards.GetDynamicDashboard(projectsFilter, environmentsFilter).ConfigureAwait(false);
            tenantsById = dashboard.Tenants.ToDictionary(t => t.Id, t => t.Name);

            dashboardRelatedResourceses = new Dictionary<DashboardItemResource, DeploymentRelatedResources>();
            foreach (var dashboardItem in dashboard.Items)
            {
                var release = await Repository.Releases.Get(dashboardItem.ReleaseId).ConfigureAwait(false);
                var channel = await Repository.Channels
                    .LoadChannelOrNull(projectsById[release.ProjectId], release.ChannelId, release.VersionControlReference?.GitCommit);
                dashboardRelatedResourceses[dashboardItem] = new DeploymentRelatedResources
                {
                    ReleaseResource = release,
                    ChannelName = channel?.Name
                };
            }
        }
        
        public void PrintDefaultOutput()
        {
            if (!dashboard.Items.Any())
                commandOutputProvider.Information("Did not find any releases matching the search criteria.");

            foreach (var dashboardItem in dashboardRelatedResourceses.Keys)
            {
                LogDeploymentInfo(
                    commandOutputProvider,
                    dashboardItem,
                    dashboardRelatedResourceses[dashboardItem].ReleaseResource,
                    dashboardRelatedResourceses[dashboardItem].ChannelName,
                    environmentsById,
                    projectsById,
                    tenantsById);
            }
        }

        public void PrintJsonOutput()
        {
            commandOutputProvider.Json(dashboardRelatedResourceses.Keys.Select(dashboardItem => new
                {
                    dashboardItem,
                    release = dashboardRelatedResourceses[dashboardItem].ReleaseResource,
                    dashboardRelatedResourceses[dashboardItem].ChannelName
                })
                .Select(x => new
                {
                    Project = new { Id = x.dashboardItem.ProjectId, Name = projectsById[x.dashboardItem.ProjectId] },
                    Environment = new { Id = x.dashboardItem.EnvironmentId, Name = environmentsById[x.dashboardItem.EnvironmentId] },
                    Tenant = string.IsNullOrWhiteSpace(x.dashboardItem.TenantId)
                        ? null
                        : new { Id = x.dashboardItem.TenantId, Name = GetNameOfDeploymentTenant(tenantsById, x.dashboardItem.TenantId) },
                    Channel = x.ChannelName == null ? null : new { x.release.ChannelId, Name = x.ChannelName },
                    Date = x.dashboardItem.QueueTime,
                    x.dashboardItem.Duration,
                    State = x.dashboardItem.State.ToString(),
                    x.release.Version,
                    x.release.Assembled,
                    PackageVersion = GetPackageVersionsAsString(x.release.SelectedPackages),
                    ReleaseNotes = GetReleaseNotes(x.release)
                }));
        }

        static string GetNameOfDeploymentTenant(IDictionary<string, string> tenantsById, string tenantId)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                return null;

            return tenantsById.ContainsKey(tenantId) ? tenantsById[tenantId] : "<Removed>";
        }
    }
}
