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

namespace Octopus.Cli.Commands.RunbooksRun 
{
    [Command("list-runbookruns", Description = "Lists a runbook runs by project and/or runbook")]
    public class ListRunbookRunsCommand : RunbookRunCommandBase, ISupportFormattedOutput
    {
        const int DefaultReturnAmount = 30;
        int? numberOfResults;

        List<RunbookRunResource> runbookRuns = new List<RunbookRunResource>();

        public ListRunbookRunsCommand(IOctopusAsyncRepositoryFactory repositoryFactory, IOctopusFileSystem fileSystem, IOctopusClientFactory clientFactory, ICommandOutputProvider commandOutputProvider)
            : base(repositoryFactory, fileSystem, clientFactory, commandOutputProvider)
        {
            var options = Options.For("Listing");
            options.Add<int>("number=", $"[Optional] number of results to return, default is {DefaultReturnAmount}", v => numberOfResults = v);
        }

        public override async Task Request()
        {
            await base.Request();

            commandOutputProvider.Debug("Loading runbook runs...");

            var maxResults = numberOfResults ?? DefaultReturnAmount;
            await Repository.RunbookRuns
                .Paginate(projectsFilter,
                    runbooksFilter,
                    environmentsFilter,
                    tenantsFilter,
                    delegate(ResourceCollection<RunbookRunResource> page)
                    {
                        if (runbookRuns.Count < maxResults)
                            foreach (var dr in page.Items.Take(maxResults - runbookRuns.Count))
                                runbookRuns.Add(dr);

                        return true;
                    })
                .ConfigureAwait(false);
        }

        public void PrintDefaultOutput()
        {
            if (!runbookRuns.Any())
                commandOutputProvider.Information("Did not find any runbook runs matching the search criteria.");

            commandOutputProvider.Debug($"Showing {runbookRuns.Count} results...");

            foreach(var item in runbookRuns) {
                LogrunbookRunInfo(commandOutputProvider, item);
            }

            if (numberOfResults.HasValue && numberOfResults != runbookRuns.Count)
                commandOutputProvider.Debug($"Please note you asked for {numberOfResults} results, but there were only {runbookRuns.Count} that matched your criteria");
        }

        public void PrintJsonOutput()
        {
            commandOutputProvider.Json(
                runbookRuns.Select(run => new
                {
                    Id = run.Id,
                    Name = run.Name,
                    Created = run.Created,
                    Project = new { Id = run.ProjectId, Name = projectsById[run.ProjectId].Name},
                    Runbook = new { Id = run.RunbookId, Name = runbooksById[run.RunbookId].Name},
                    Environment = new { Id = run.EnvironmentId, Name = environmentsById[run.EnvironmentId].Name},
                    Tenant = new { Id = run.TenantId, Name = !string.IsNullOrEmpty(run.TenantId) ? tenantsById[run.TenantId].Name : null },
                    FailureEncountered = run.FailureEncountered,
                    Comments = run.Comments
                }));
        }

        private void LogrunbookRunInfo(ICommandOutputProvider outputProvider,
            RunbookRunResource runbookRunItem)
        {
            var nameOfrunbookRunEnvironment = environmentsById[runbookRunItem.EnvironmentId].Name;
            var nameOfrunbookRunProject = projectsById[runbookRunItem.ProjectId].Name;
            var nameOfrunbook = runbooksById[runbookRunItem.RunbookId].Name;

            outputProvider.Information(" - Id: {Name:1}", runbookRunItem.Id);
            outputProvider.Information(" - Name: {Name:1}", runbookRunItem.Name);
            outputProvider.Information(" - Project: {Project:l}", nameOfrunbookRunProject);
            outputProvider.Information(" - Runbook: {Runbook:l}", nameOfrunbook);
            outputProvider.Information(" - Environment: {Environment:l}", nameOfrunbookRunEnvironment);

            if (!string.IsNullOrEmpty(runbookRunItem.TenantId))
            {
                var nameOfrunbookRunTenant = tenantsById[runbookRunItem.TenantId].Name;
                outputProvider.Information(" - Tenant: {Tenant:l}", nameOfrunbookRunTenant);
            }

            outputProvider.Information("\tCreated: {$Date:l}", runbookRunItem.Created);
            if (!string.IsNullOrWhiteSpace(runbookRunItem.Comments)) outputProvider.Information("\tComments: {$Comments:l}", runbookRunItem.Comments);
            outputProvider.Information("\tFaulure Encountered: {FailureEncountered:l}", runbookRunItem.FailureEncountered ? "Yes" : "No");

            outputProvider.Information(string.Empty);
        }
    }
}
