﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Octopus.Cli.Infrastructure;
using Octopus.Cli.Repositories;
using Octopus.Cli.Util;
using Octopus.Client;
using Octopus.Client.Model;
using Octopus.Client.Model.Endpoints;
using Octopus.CommandLine;
using Octopus.CommandLine.Commands;
using Octopus.CommandLine.OptionParsing;

namespace Octopus.Cli.Commands.Machine
{
    [Command("list-workers", Description = "Lists all workers.")]
    public class ListWorkersCommand : ApiCommand, ISupportFormattedOutput
    {
        readonly HashSet<string> pools = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        readonly HashSet<MachineModelStatus> statuses = new HashSet<MachineModelStatus>();
        readonly HashSet<MachineModelHealthStatus> healthStatuses = new HashSet<MachineModelHealthStatus>();
        HealthStatusProvider provider;
        List<WorkerPoolResource> workerpoolResources;
        IEnumerable<WorkerResource> workerpoolWorkers;
        bool? isDisabled;
        bool? isCalamariOutdated;
        bool? isTentacleOutdated;

        public ListWorkersCommand(IOctopusAsyncRepositoryFactory repositoryFactory, IOctopusFileSystem fileSystem, IOctopusClientFactory clientFactory, IOctopusCliCommandOutputProvider commandOutputProvider)
            : base(clientFactory, repositoryFactory, fileSystem, commandOutputProvider)
        {
            var options = Options.For("Listing Workers");
            options.Add<string>("workerPool=", "Name of a worker pool to filter by. Can be specified many times.", v => pools.Add(v), allowsMultiple: true);
            options.Add<MachineModelStatus>("status=", $"[Optional] Status of Machines filter by ({string.Join(", ", HealthStatusProvider.StatusNames)}). Can be specified many times.", v => statuses.Add(v), allowsMultiple: true);
            options.Add<MachineModelHealthStatus>("health-status=|healthStatus=", $"[Optional] Health status of Machines filter by ({string.Join(", ", HealthStatusProvider.HealthStatusNames)}). Can be specified many times.", v => healthStatuses.Add(v), allowsMultiple: true);
            options.Add<bool>("disabled=", "[Optional] Disabled status filter of Machine.", v => isDisabled = v);
            options.Add<bool>("calamari-outdated=", "[Optional] State of Calamari to filter. By default ignores Calamari state.", v => isCalamariOutdated = v);
            options.Add<bool>("tentacle-outdated=", "[Optional] State of Tentacle version to filter. By default ignores Tentacle state.", v => isTentacleOutdated = v);
        }

        public async Task Request()
        {
            var rootDocument = await Repository.LoadRootDocument().ConfigureAwait(false);
            provider = new HealthStatusProvider(Repository,
                statuses,
                healthStatuses,
                commandOutputProvider,
                rootDocument);

            workerpoolResources = await GetPools().ConfigureAwait(false);

            workerpoolWorkers = await FilterByWorkerPools(workerpoolResources).ConfigureAwait(false);
            workerpoolWorkers = FilterByState(workerpoolWorkers, provider);
        }

        public void PrintDefaultOutput()
        {
            LogFilteredMachines(workerpoolWorkers, provider, workerpoolResources);
        }

        public void PrintJsonOutput()
        {
            commandOutputProvider.Json(workerpoolWorkers.Select(machine => new
            {
                machine.Id,
                machine.Name,
                Status = provider.GetStatus(machine),
                WorkerPools = machine.WorkerPoolIds.Select(id => workerpoolResources.First(e => e.Id == id).Name)
                    .ToArray()
            }));
        }

        void LogFilteredMachines(IEnumerable<WorkerResource> poolMachines, HealthStatusProvider provider, List<WorkerPoolResource> poolResources)
        {
            var orderedMachines = poolMachines.OrderBy(m => m.Name).ToList();
            commandOutputProvider.Information("Workers: {Count}", orderedMachines.Count);
            foreach (var machine in orderedMachines)
            {
                commandOutputProvider.Information(" - {Machine:l} {Status:l} (ID: {MachineId:l}) in {WorkerPool:l}",
                    machine.Name,
                    provider.GetStatus(machine),
                    machine.Id,
                    string.Join(" and ", machine.WorkerPoolIds.Select(id => poolResources.First(e => e.Id == id).Name)));
            }
        }

        Task<List<WorkerPoolResource>> GetPools()
        {
            commandOutputProvider.Debug("Loading pools...");
            return Repository.WorkerPools.FindAll();
        }

        IEnumerable<WorkerResource> FilterByState(IEnumerable<WorkerResource> poolMachines, HealthStatusProvider provider)
        {
            poolMachines = provider.Filter(poolMachines);

            if (isDisabled.HasValue)
                poolMachines = poolMachines.Where(m => m.IsDisabled == isDisabled.Value);
            if (isCalamariOutdated.HasValue)
                poolMachines = poolMachines.Where(m => m.HasLatestCalamari == !isCalamariOutdated.Value);
            if (isTentacleOutdated.HasValue)
                poolMachines =
                    poolMachines.Where(
                        m =>
                            (m.Endpoint as ListeningTentacleEndpointResource)?.TentacleVersionDetails.UpgradeSuggested ==
                            isTentacleOutdated.Value);
            return poolMachines;
        }

        Task<List<WorkerResource>> FilterByWorkerPools(List<WorkerPoolResource> poolResources)
        {
            var poolsToInclude = poolResources.Where(e => pools.Contains(e.Name, StringComparer.OrdinalIgnoreCase)).ToList();
            var missingPools = pools.Except(poolsToInclude.Select(e => e.Name), StringComparer.OrdinalIgnoreCase).ToList();
            if (missingPools.Any())
                throw new CouldNotFindException("pools(s) named", string.Join(", ", missingPools));

            var poolsFilter = poolsToInclude.Select(p => p.Id).ToList();

            commandOutputProvider.Debug("Loading workers...");
            if (poolsFilter.Count > 0)
            {
                commandOutputProvider.Debug("Loading machines from {WorkerPools:l}...", string.Join(", ", poolsToInclude.Select(e => e.Name)));
                return
                    Repository.Workers.FindMany(
                        x => { return x.WorkerPoolIds.Any(poolId => poolsFilter.Contains(poolId)); });
            }

            commandOutputProvider.Debug("Loading workers from all pools...");
            return Repository.Workers.FindAll();
        }
    }
}
