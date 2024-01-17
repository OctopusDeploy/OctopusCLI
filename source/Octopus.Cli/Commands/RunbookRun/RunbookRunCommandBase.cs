using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Octopus.Cli.Commands.Runbooks;
using Octopus.Cli.Repositories;
using Octopus.Cli.Util;
using Octopus.Client;
using Octopus.Client.Model;
using Octopus.CommandLine;
using Octopus.CommandLine.Commands;

namespace Octopus.Cli.Commands.RunbooksRun {
    
    /// <summary>
    /// Base class for Runbook related commands with shared logic for all Runbook (and RunbookRun) commands
    /// </summary>
    public abstract class RunbookRunCommandBase : RunbookCommandBase {
        protected readonly HashSet<string> environments = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        protected readonly HashSet<string> runbooks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        protected readonly HashSet<string> tenants = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        protected IDictionary<string, RunbookResource> runbooksById;
        protected IDictionary<string, EnvironmentResource> environmentsById;
        protected IDictionary<string, TenantResource> tenantsById;

        protected string[] runbooksFilter;
        protected string[] environmentsFilter;
        protected string[] tenantsFilter;

        public RunbookRunCommandBase(IOctopusAsyncRepositoryFactory repositoryFactory, IOctopusFileSystem fileSystem, IOctopusClientFactory clientFactory, ICommandOutputProvider commandOutputProvider)
            : base(repositoryFactory, fileSystem, clientFactory, commandOutputProvider) {
            var options = Options.For("Listing");
            options.Add<string>("runbook=", "[Optional] Name of a runbook to filter by. Can be specified many times.", v => runbooks.Add(v), allowsMultiple: true);
            options.Add<string>("environment=", "[Optional] Name of an environment to filter by. Can be specified many times.", v => environments.Add(v), allowsMultiple: true);
            options.Add<string>("tenant=", "[Optional] Name of a tenant to filter by. Can be specified many times.", v => tenants.Add(v), allowsMultiple: true);
        }

        public override async Task Request() {
            // Need to run the base implementation first to resolve the projects, which is required by Runbook query.
            await base.Request();

            environmentsById = await LoadEnvironments().ConfigureAwait(false);
            environmentsFilter = environmentsById.Any() ? environmentsById.Keys.ToArray() : new string[0];
            runbooksById = await LoadRunbooks().ConfigureAwait(false);
            runbooksFilter = runbooksById.Any() ? runbooksById.Keys.ToArray() : new string[0];
            tenantsById = await LoadTenants().ConfigureAwait(false);
            tenantsFilter = tenants.Any() ? tenantsById.Keys.ToArray() : new string[0];
        }

        private async Task<IDictionary<string, EnvironmentResource>> LoadEnvironments() {
            commandOutputProvider.Information("Loading environments...");
            var environmentQuery = environments.Any()
                ? Repository.Environments.FindByNames(environments.ToArray())
                : Repository.Environments.FindAll();

            var environmentResources = await environmentQuery.ConfigureAwait(false);

            var missingEnvironments =
                environments.Except(environmentResources.Select(e => e.Name), StringComparer.OrdinalIgnoreCase)
                    .ToArray();

            if(missingEnvironments.Any())
                throw new CommandException("Could not find environments: " + string.Join(",", missingEnvironments));

            return environmentResources.ToDictionary(e => e.Id, e => e);
        }

        private async Task<IDictionary<string, RunbookResource>> LoadRunbooks() {
            commandOutputProvider.Information("Loading runbooks...");

            Task<List<RunbookResource>> runbookQuery;

            if(projectsFilter.Any()) {
                runbookQuery = runbooks.Any()
                    ? Repository.Runbooks.FindMany(rb => projectsFilter.Contains(rb.ProjectId) && runbooks.ToArray().Contains(rb.Name))
                    : Repository.Runbooks.FindMany(rb => projectsFilter.Contains(rb.ProjectId));
            } else {
                runbookQuery = runbooks.Any()
                    ? Repository.Runbooks.FindByNames(runbooks.ToArray())
                    : Repository.Runbooks.FindAll();
            }

            var runbookResources = await runbookQuery.ConfigureAwait(false);

            var missingRunbooks =
                runbooks.Except(runbookResources.Select(e => e.Name), StringComparer.OrdinalIgnoreCase)
                    .ToArray();

            if(missingRunbooks.Any())
                throw new CommandException("Could not find runbooks: " + string.Join(",", missingRunbooks));

            return runbookResources.ToDictionary(rb => rb.Id, rb => rb);
        }

        private async Task<IDictionary<string, TenantResource>> LoadTenants() {
            commandOutputProvider.Information("Loading tenants...");

            var multiTenancyStatus = await Repository.Tenants.Status().ConfigureAwait(false);

            if(multiTenancyStatus.Enabled) {
                var tenantsQuery = tenants.Any()
                ? Repository.Tenants.FindByNames(tenants.ToArray())
                : Repository.Tenants.FindAll();

                var tenantsResources = await tenantsQuery.ConfigureAwait(false);

                var missingTenants = tenants.Except(tenantsResources.Select(e => e.Name), StringComparer.OrdinalIgnoreCase).ToArray();

                if(missingTenants.Any())
                    throw new CommandException("Could not find tenants: " + string.Join(",", missingTenants));

                return tenantsResources.ToDictionary(t => t.Id, t => t);
            } else {
                return new Dictionary<string, TenantResource>();
            }
        }
    }
}
