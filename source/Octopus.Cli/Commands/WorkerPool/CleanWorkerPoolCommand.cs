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

namespace Octopus.Cli.Commands.WorkerPool
{
    [Command("clean-workerpool", Description = "Cleans all Offline Workers from a WorkerPool.")]
    public class CleanWorkerPoolCommand : ApiCommand, ISupportFormattedOutput
    {
        public enum MachineAction
        {
            RemovedFromPool,
            Deleted
        }

        readonly HashSet<MachineModelHealthStatus> healthStatuses = new HashSet<MachineModelHealthStatus>();
        readonly List<MachineResult> commandResults = new List<MachineResult>();
        string poolName;
        bool? isDisabled;
        bool? isCalamariOutdated;
        bool? isTentacleOutdated;
        WorkerPoolResource workerPoolResource;
        IEnumerable<WorkerResource> machines;

        public CleanWorkerPoolCommand(IOctopusAsyncRepositoryFactory repositoryFactory, IOctopusFileSystem fileSystem, IOctopusClientFactory clientFactory, ICommandOutputProvider commandOutputProvider)
            : base(clientFactory, repositoryFactory, fileSystem, commandOutputProvider)
        {
            var options = Options.For("WorkerPool Cleanup");
            options.Add<string>("workerPool=", "Name of a worker pool to clean up.", v => poolName = v);
            options.Add<MachineModelHealthStatus>("health-status=", $"Health status of Workers to clean up. Valid values are {HealthStatusProvider.HealthStatusNames.ReadableJoin()}. Can be specified many times.", v => healthStatuses.Add(v), allowsMultiple: true);
            options.Add<bool>("disabled=", "[Optional] Disabled status filter of Worker to clean up.", v => isDisabled = v);
            options.Add<bool>("calamari-outdated=", "[Optional] State of Calamari to clean up. By default ignores Calamari state.", v => isCalamariOutdated = v);
            options.Add<bool>("tentacle-outdated=", "[Optional] State of Tentacle version to clean up. By default ignores Tentacle state.", v => isTentacleOutdated = v);
        }

        public async Task Request()
        {
            if (string.IsNullOrWhiteSpace(poolName))
                throw new CommandException("Please specify a worker pool name using the parameter: --workerpool=XYZ");
            if (!healthStatuses.Any())
                throw new CommandException("Please specify a status using the parameter: --health-status");

            workerPoolResource = await GetWorkerPool().ConfigureAwait(false);

            machines = await FilterByWorkerPool(workerPoolResource).ConfigureAwait(false);
            machines = await FilterByState(machines).ConfigureAwait(false);

            await CleanUpPool(machines.ToList(), workerPoolResource).ConfigureAwait(false);
        }

        async Task CleanUpPool(List<WorkerResource> filteredMachines, WorkerPoolResource poolResource)
        {
            commandOutputProvider.Information("Found {MachineCount} machines in {WorkerPool:l} with the status {Status:l}", filteredMachines.Count, poolResource.Name, GetStateFilterDescription());

            if (filteredMachines.Any(m => m.WorkerPoolIds.Count > 1))
                commandOutputProvider.Information("Note: Some of these machines belong to multiple pools. Instead of being deleted, these machines will be removed from the {WorkerPool:l} pool.", poolResource.Name);

            foreach (var machine in filteredMachines)
            {
                var result = new MachineResult
                {
                    Machine = machine
                };
                // If the machine belongs to more than one pool, we should remove the machine from the pool rather than delete it altogether.
                if (machine.WorkerPoolIds.Count > 1)
                {
                    commandOutputProvider.Information("Removing {Machine:l} {Status} (ID: {Id:l}) from {WorkerPool:l}",
                        machine.Name,
                        machine.Status,
                        machine.Id,
                        poolResource.Name);
                    machine.WorkerPoolIds.Remove(poolResource.Id);
                    await Repository.Workers.Modify(machine).ConfigureAwait(false);
                    result.Action = MachineAction.RemovedFromPool;
                }
                else
                {
                    commandOutputProvider.Information("Deleting {Machine:l} {Status} (ID: {Id:l})", machine.Name, machine.Status, machine.Id);
                    await Repository.Workers.Delete(machine).ConfigureAwait(false);
                    result.Action = MachineAction.Deleted;
                }

                commandResults.Add(result);
            }
        }

        async Task<IEnumerable<WorkerResource>> FilterByState(IEnumerable<WorkerResource> workers)
        {
            var rootDocument = await Repository.LoadRootDocument().ConfigureAwait(false);
            var provider = new HealthStatusProvider(Repository,
                new HashSet<MachineModelStatus>(),
                healthStatuses,
                commandOutputProvider,
                rootDocument);
            workers = provider.Filter(workers);

            if (isDisabled.HasValue)
                workers = workers.Where(m => m.IsDisabled == isDisabled.Value);
            if (isCalamariOutdated.HasValue)
                workers = workers.Where(m => m.HasLatestCalamari == !isCalamariOutdated.Value);
            if (isTentacleOutdated.HasValue)
                workers = workers.Where(m => (m.Endpoint as ListeningTentacleEndpointResource)?.TentacleVersionDetails.UpgradeSuggested == isTentacleOutdated.Value);
            return workers;
        }

        string GetStateFilterDescription()
        {
            var description = string.Join(",", healthStatuses);

            if (isDisabled.HasValue)
                description += isDisabled.Value ? "and disabled" : "and not disabled";

            if (isCalamariOutdated.HasValue)
                description += $" and its Calamari version {(isCalamariOutdated.Value ? "" : "not")}out of date";

            if (isTentacleOutdated.HasValue)
                description += $" and its Tentacle version {(isTentacleOutdated.Value ? "" : "not")}out of date";

            return description;
        }

        Task<List<WorkerResource>> FilterByWorkerPool(WorkerPoolResource poolResource)
        {
            commandOutputProvider.Debug("Loading workers...");
            return Repository.Workers.FindMany(x => x.WorkerPoolIds.Any(poolId => poolId == poolResource.Id));
        }

        async Task<WorkerPoolResource> GetWorkerPool()
        {
            commandOutputProvider.Debug("Loading worker pools...");
            var poolResource = await Repository.WorkerPools.FindByName(poolName).ConfigureAwait(false);
            if (poolResource == null)
                throw new CouldNotFindException("the specified worker pool");
            return poolResource;
        }

        public void PrintDefaultOutput()
        {
        }

        public void PrintJsonOutput()
        {
            commandOutputProvider.Json(commandResults.Select(x => new
            {
                Machine = new { x.Machine.Id, x.Machine.Name, x.Machine.Status },
                Environment = x.Action == MachineAction.RemovedFromPool ? new { workerPoolResource.Id, workerPoolResource.Name } : null,
                Action = x.Action.ToString()
            }));
        }

        class MachineResult
        {
            public MachineBasedResource Machine { get; set; }
            public MachineAction Action { get; set; }
        }
    }
}
