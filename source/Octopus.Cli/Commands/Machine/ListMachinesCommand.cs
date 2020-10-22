using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Octopus.Cli.Infrastructure;
using Octopus.Cli.Repositories;
using Octopus.Cli.Util;
using Octopus.Client;
using Octopus.Client.Model;
using Octopus.Client.Model.Endpoints;
using Serilog;

namespace Octopus.Cli.Commands.Machine
{
    [Command("list-machines", Description = "Lists all machines.")]
    public class ListMachinesCommand : ApiCommand, ISupportFormattedOutput
    {
        readonly HashSet<string> environments = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        readonly HashSet<MachineModelStatus> statuses = new HashSet<MachineModelStatus>();
        readonly HashSet<MachineModelHealthStatus> healthStatuses = new HashSet<MachineModelHealthStatus>();
        private HealthStatusProvider provider;
        List<EnvironmentResource> environmentResources;
        IEnumerable<MachineResource> environmentMachines;
        private bool? isDisabled;
        private bool? isCalamariOutdated;
        private bool? isTentacleOutdated;

        public ListMachinesCommand(IOctopusAsyncRepositoryFactory repositoryFactory, IOctopusFileSystem fileSystem, IOctopusClientFactory clientFactory, ICommandOutputProvider commandOutputProvider)
            : base(clientFactory, repositoryFactory, fileSystem, commandOutputProvider)
        {
            var options = Options.For("Listing");
            options.Add<string>("environment=", "Name of an environment to filter by. Can be specified many times.", v => environments.Add(v), allowsMultiple: true);
            options.Add<MachineModelStatus>("status=", $"[Optional] Status of Machines filter by ({string.Join(", ", HealthStatusProvider.StatusNames)}). Can be specified many times.", v => statuses.Add(v), allowsMultiple: true);
            options.Add<MachineModelHealthStatus>("health-status=|healthStatus=", $"[Optional] Health status of Machines filter by ({string.Join(", ", HealthStatusProvider.HealthStatusNames)}). Can be specified many times.", v => healthStatuses.Add(v), allowsMultiple: true);
            options.Add<bool>("disabled=", "[Optional] Disabled status filter of Machine.", v => isDisabled = v);
            options.Add<bool>("calamari-outdated=", "[Optional] State of Calamari to filter. By default ignores Calamari state.", v => isCalamariOutdated = v);
            options.Add<bool>("tentacle-outdated=", "[Optional] State of Tentacle version to filter. By default ignores Tentacle state", v => isTentacleOutdated = v);
        }

        public async Task Request()
        {
            var rootDocument = await Repository.LoadRootDocument().ConfigureAwait(false);
            provider = new HealthStatusProvider(Repository, statuses, healthStatuses, commandOutputProvider, rootDocument);

            environmentResources = await GetEnvironments().ConfigureAwait(false);

            environmentMachines = await FilterByEnvironments(environmentResources).ConfigureAwait(false);
            environmentMachines = FilterByState(environmentMachines, provider);
        }

        public void PrintDefaultOutput()
        {
            LogFilteredMachines(environmentMachines, provider, environmentResources);
        }

        public void PrintJsonOutput()
        {
            commandOutputProvider.Json(environmentMachines.Select(machine => new
            {
                machine.Id,
                machine.Name,
                Status = provider.GetStatus(machine),
                Environments = machine.EnvironmentIds.Select(id => environmentResources.First(e => e.Id == id).Name)
                    .ToArray()
            }));
        }

        private void LogFilteredMachines(IEnumerable<MachineResource> environmentMachines, HealthStatusProvider provider, List<EnvironmentResource> environmentResources)
        {
            var orderedMachines = environmentMachines.OrderBy(m => m.Name).ToList();
            commandOutputProvider.Information("Machines: {Count}", orderedMachines.Count);
            foreach (var machine in orderedMachines)
            {
                commandOutputProvider.Information(" - {Machine:l} {Status:l} (ID: {MachineId:l}) in {Environments:l}", machine.Name, provider.GetStatus(machine), machine.Id,
                    string.Join(" and ", machine.EnvironmentIds.Select(id => environmentResources.First(e => e.Id == id).Name)));
            }
        }

        private Task<List<EnvironmentResource>> GetEnvironments()
        {
            commandOutputProvider.Debug("Loading environments...");
            return Repository.Environments.FindAll();
        }

        private IEnumerable<MachineResource> FilterByState(IEnumerable<MachineResource> environmentMachines, HealthStatusProvider provider)
        {
            environmentMachines = provider.Filter(environmentMachines);

            if (isDisabled.HasValue)
            {
                environmentMachines = environmentMachines.Where(m => m.IsDisabled == isDisabled.Value);
            }
            if (isCalamariOutdated.HasValue)
            {
                environmentMachines = environmentMachines.Where(m => m.HasLatestCalamari == !isCalamariOutdated.Value);
            }
            if (isTentacleOutdated.HasValue)
            {
                environmentMachines =
                    environmentMachines.Where(
                        m =>
                            (m.Endpoint as ListeningTentacleEndpointResource)?.TentacleVersionDetails.UpgradeSuggested ==
                            isTentacleOutdated.Value);
            }
            return environmentMachines;
        }

        private  Task<List<MachineResource>> FilterByEnvironments(List<EnvironmentResource> environmentResources)
        {
            var environmentsToInclude = environmentResources.Where(e => environments.Contains(e.Name, StringComparer.OrdinalIgnoreCase)).ToList();
            var missingEnvironments = environments.Except(environmentsToInclude.Select(e => e.Name), StringComparer.OrdinalIgnoreCase).ToList();
            if (missingEnvironments.Any())
                throw new CouldNotFindException("environment(s) named", string.Join(", ", missingEnvironments));


            var environmentFilter = environmentsToInclude.Select(p => p.Id).ToList();

            commandOutputProvider.Debug("Loading machines...");
            if (environmentFilter.Count > 0)
            {
                commandOutputProvider.Debug("Loading machines from {Environments:l}...", string.Join(", ", environmentsToInclude.Select(e => e.Name)));
                return
                     Repository.Machines.FindMany(
                        x => { return x.EnvironmentIds.Any(environmentId => environmentFilter.Contains(environmentId)); });
            }
            else
            {
                commandOutputProvider.Debug("Loading machines from all environments...");
                return  Repository.Machines.FindAll();
            }
        }
    }
}
