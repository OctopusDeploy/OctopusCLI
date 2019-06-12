﻿using System.Threading.Tasks;
using System.Linq;
using Octopus.Cli.Infrastructure;
using Octopus.Cli.Repositories;
using Octopus.Cli.Util;
using Octopus.Client;
using Octopus.Client.Model;
using Serilog;
using Octopus.Cli.Commands.Deployment;

namespace Octopus.Cli.Commands.Releases
{
    [Command("promote-release", Description = "Promotes a release.")]
    public class PromoteReleaseCommand : DeploymentCommandBase, ISupportFormattedOutput
    {
        ProjectResource project;
        EnvironmentResource environment;
        ReleaseResource release;

        public PromoteReleaseCommand(IOctopusAsyncRepositoryFactory repositoryFactory, IOctopusFileSystem fileSystem, IOctopusClientFactory clientFactory, ICommandOutputProvider commandOutputProvider)
            : base(repositoryFactory, fileSystem, clientFactory, commandOutputProvider)
        {
            var options = Options.For("Release Promotion");
            options.Add("project=", "Name or ID of the project", v => ProjectName = v);
            options.Add("from=", "Name or ID of the environment to get the current deployment from, e.g., Staging", v => FromEnvironmentName = v);
            options.Add("to=|deployto=", "Name or ID of the environment to deploy to, e.g., Production", v => DeployToEnvironmentNames.Add(v));
            options.Add("updateVariables", "Overwrite the variable snapshot for the release by re-importing the variables from the project", v => UpdateVariableSnapshot = true);
        }

        public string FromEnvironmentName { get; set; }
        public bool UpdateVariableSnapshot { get; set; }

        protected override async Task ValidateParameters()
        {
            if (DeployToEnvironmentNames.Count == 0) throw new CommandException("Please specify an environment name or ID using the parameter: --deployto=XYZ");
            if (string.IsNullOrWhiteSpace(FromEnvironmentName)) throw new CommandException("Please specify a source environment name or ID using the parameter: --from=XYZ");

            await base.ValidateParameters().ConfigureAwait(false);
        }

        public async Task Request()
        {
            project = await Repository.Projects.FindByNameOrIdOrFail(ProjectName).ConfigureAwait(false);

            environment = await Repository.Environments.FindByNameOrIdOrFail(FromEnvironmentName).ConfigureAwait(false);

            var dashboard = await Repository.Dashboards.GetDynamicDashboard(new[] {project.Id}, new[] {environment.Id}).ConfigureAwait(false);
            var dashboardItem = dashboard.Items.Where(e => e.EnvironmentId == environment.Id && e.ProjectId == project.Id)
                .OrderByDescending(i => SemanticVersion.Parse(i.ReleaseVersion))
                .FirstOrDefault();

            if (dashboardItem == null)
            {
                throw new CouldNotFindException("latest deployment of the project for this environment. Please check that a deployment for this project/environment exists on the dashboard.");
            }

            commandOutputProvider.Debug("Finding release details for release {Version:l}", dashboardItem.ReleaseVersion);
            
            release = await Repository.Projects.GetReleaseByVersion(project, dashboardItem.ReleaseVersion).ConfigureAwait(false);

            if (UpdateVariableSnapshot)
            {
                commandOutputProvider.Debug("Updating the release variable snapshot with variables from the project");
                await Repository.Releases.SnapshotVariables(release).ConfigureAwait(false);
            }

            await DeployRelease(project, release).ConfigureAwait(false);
        }
        
        public void PrintDefaultOutput()
        {
            
        }

        public void PrintJsonOutput()
        {
            commandOutputProvider.Json(new
            {
                ProjectName = new { project.Id, project.Name },
                FromEnvironment = new { environment.Id, environment.Name },
                release.Version,
                Deployments = deployments.Select(d => new
                {
                    DeploymentId = d.Id,
                    d.ReleaseId,
                    Environment = new
                    {
                        d.EnvironmentId,
                        promotionTargets.FirstOrDefault(x => x.Id == d.EnvironmentId)?.Name
                    },
                    d.SkipActions,
                    d.SpecificMachineIds,
                    d.ExcludedMachineIds,
                    d.Created,
                    d.Name,
                    d.QueueTime,
                    Tenant = string.IsNullOrEmpty(d.TenantId)
                        ? null
                        : new {d.TenantId, TenantName = deploymentTenants.FirstOrDefault(x => x.Id == d.TenantId)?.Name}
                })
            });
        }
    }
}