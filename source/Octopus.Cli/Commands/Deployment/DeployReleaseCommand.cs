using System;
using System.Linq;
using System.Threading.Tasks;
using Octopus.Cli.Infrastructure;
using Octopus.Cli.Repositories;
using Octopus.Cli.Util;
using Octopus.Client;
using Octopus.Client.Model;
using Serilog;

namespace Octopus.Cli.Commands.Deployment
{
    [Command("deploy-release", Description = "Deploys a release.")]
    public class DeployReleaseCommand : DeploymentCommandBase, ISupportFormattedOutput
    {
        ProjectResource project;
        ChannelResource channel;
        ReleaseResource releaseToPromote;

        public DeployReleaseCommand(IOctopusAsyncRepositoryFactory repositoryFactory, IOctopusFileSystem fileSystem, IOctopusClientFactory clientFactory, ICommandOutputProvider commandOutputProvider)
            : base(repositoryFactory, fileSystem, clientFactory, commandOutputProvider)
        {
            var options = Options.For("Deployment");
            options.Add("project=", "Name or ID of the project", v => ProjectNameOrId = v);
            options.Add("deployto=", "Name or ID of the environment to deploy to, e.g., 'Production' or 'Environments-1'; specify this argument multiple times to deploy to multiple environments.", v => DeployToEnvironmentNamesOrIds.Add(v));
            options.Add("releaseNumber=|version=", "Version number of the release to deploy. Or specify --version=latest for the latest release.", v => VersionNumber = v);
            options.Add("channel=", "[Optional] Name or ID of the channel to use when getting the release to deploy", v => ChannelNameOrId = v);
            options.Add("updateVariables", "Overwrite the variable snapshot for the release by re-importing the variables from the project", v => UpdateVariableSnapshot = true);
        }

        public string VersionNumber { get; set; }
        public string ChannelNameOrId { get; set; }
        public bool UpdateVariableSnapshot { get; set; }


        protected override async Task ValidateParameters()
        {
            if (DeployToEnvironmentNamesOrIds.Count == 0) throw new CommandException("Please specify an environment name or ID using the parameter: --deployto=XYZ");
            if (string.IsNullOrWhiteSpace(VersionNumber)) throw new CommandException("Please specify a release version using the parameter: --version=1.0.0.0 or --version=latest for the latest release");
            if (!string.IsNullOrWhiteSpace(ChannelNameOrId) && !await Repository.SupportsChannels().ConfigureAwait(false)) throw new CommandException("Your Octopus Server does not support channels, which was introduced in Octopus 3.2. Please upgrade your Octopus Server, or remove the --channel argument.");

            await base.ValidateParameters().ConfigureAwait(false);
        }

        public async Task Request()
        {
            project = await Repository.Projects.FindByNameOrIdOrFail(ProjectNameOrId).ConfigureAwait(false);
            channel = !string.IsNullOrWhiteSpace(ChannelNameOrId)
                ? await Repository.Channels.FindByNameOrIdOrFail(project, ChannelNameOrId).ConfigureAwait(false)
                : null;
            releaseToPromote = await RepositoryCommonQueries.GetReleaseByVersion(VersionNumber, project, channel).ConfigureAwait(false);

            if (UpdateVariableSnapshot)
            {
                commandOutputProvider.Debug("Updating the release variable snapshot with variables from the project");
                await Repository.Releases.SnapshotVariables(releaseToPromote).ConfigureAwait(false);
            }

            await DeployRelease(project, releaseToPromote).ConfigureAwait(false);
        }

        public void PrintDefaultOutput()
        {
            
        }

        public void PrintJsonOutput()
        {
            commandOutputProvider.Json(new
            {
                ProjectName = project.Name,
                releaseToPromote.Version,
                Deployments = deployments.Select(d => new
                {
                    DeploymentId = d.Id,
                    d.ReleaseId,
                    Environment = new
                    {
                        d.EnvironmentId,
                        EnvironmentName = promotionTargets.FirstOrDefault(x => x.Id == d.EnvironmentId)?.Name
                    },
                    d.SkipActions,
                    d.SpecificMachineIds,
                    d.ExcludedMachineIds,
                    d.Created,
                    d.Name,
                    d.QueueTime,
                    Tenant = string.IsNullOrEmpty(d.TenantId)
                        ? null
                        : new { d.TenantId, TenantName = deploymentTenants.FirstOrDefault(x => x.Id == d.TenantId)?.Name },
                    d.UseGuidedFailure
                })
            });
        }
    }
}