using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Octopus.Cli.Repositories;
using Octopus.Cli.Util;
using Octopus.Client;
using Octopus.Client.Model;
using Octopus.CommandLine;
using Octopus.CommandLine.Commands;

namespace Octopus.Cli.Commands.Runbooks {
    
    /// <summary>
    /// Base class for Runbook related commands with shared logic for all Runbook (and RunbookRun) commands
    /// </summary>
    public abstract class RunbookCommandBase : ApiCommand {
        protected readonly HashSet<string> projects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        protected IDictionary<string, ProjectResource> projectsById;
        protected string[] projectsFilter;

        public RunbookCommandBase(IOctopusAsyncRepositoryFactory repositoryFactory, IOctopusFileSystem fileSystem, IOctopusClientFactory clientFactory, ICommandOutputProvider commandOutputProvider)
            : base(clientFactory, repositoryFactory, fileSystem, commandOutputProvider) {
            var options = Options.For("Listing");
            options.Add<string>("project=", "[Optional] Name of a project to filter by. Can be specified many times.", v => projects.Add(v), allowsMultiple: true);
        }

        public virtual async Task Request() {
            projectsById = await LoadProjects().ConfigureAwait(false);
            projectsFilter = projectsById.Any() ? projectsById.Keys.ToArray() : new string[0];
        }

        protected IEnumerable<string> FormatRunbookPropertiesAsStrings(RunbookResource runbook) {
            return new string[]
            {
                "Id: " + runbook.Id,
                "Name: " + runbook.Name,
                "Description: " + runbook.Description,
            };
        }

        private async Task<IDictionary<string, ProjectResource>> LoadProjects() {
            commandOutputProvider.Information("Loading projects...");
            var projectQuery = projects.Any()
                ? Repository.Projects.FindByNames(projects.ToArray())
                : Repository.Projects.FindAll();

            var projectResources = await projectQuery.ConfigureAwait(false);

            var missingProjects = projects.Except(projectResources.Select(e => e.Name), StringComparer.OrdinalIgnoreCase).ToArray();

            if(missingProjects.Any())
                throw new CommandException("Could not find projects: " + string.Join(",", missingProjects));

            return projectResources.ToDictionary(p => p.Id, p => p);
        }
    }
}
