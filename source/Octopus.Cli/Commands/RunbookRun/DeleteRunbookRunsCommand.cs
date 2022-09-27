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

namespace Octopus.Cli.Commands.RunbooksRun {
    [Command("delete-runbookruns", Description = "Deletes a range of runbook runs.")]
    public class DeleteRunbookRunsCommand : RunbookRunCommandBase, ISupportFormattedOutput
    {
        List<RunbookRunResource> toDelete = new List<RunbookRunResource>();
        List<RunbookRunResource> wouldDelete = new List<RunbookRunResource>();
        DateTime? minDate = null;
        DateTime? maxDate = null;

        bool whatIf = false;

        public DeleteRunbookRunsCommand(IOctopusAsyncRepositoryFactory repositoryFactory, IOctopusFileSystem fileSystem, IOctopusClientFactory clientFactory, ICommandOutputProvider commandOutputProvider)
            : base(repositoryFactory, fileSystem, clientFactory, commandOutputProvider)
        {
            var options = Options.For("Deletion");
            options.Add<DateTime>("minCreateDate=", "Earliest (inclusive) create date for the range of runbook runs to delete.", v => minDate = v);
            options.Add<DateTime>("maxCreateDate=", "Latest (inclusive) create date for the range of runbooks to delete.", v => maxDate = v);
            options.Add<bool>("whatIf", "[Optional, Flag] if specified, releases won't actually be deleted, but will be listed as if simulating the command.", v => whatIf = true);
        }

        protected override Task ValidateParameters() {
            if(!minDate.HasValue)
                throw new CommandException("Please specify the earliest (inclusive) create date for the range of runbook runs to delete using the parameter: --minCreateData=2022-01-01");
            if(!maxDate.HasValue)
                throw new CommandException("Please specify the latest (inclusive) create date for the range of runbooks to delete using the parameter: --maxCreateDate=2022-01-01");

            return base.ValidateParameters();
        }

        public override async Task Request()
        {
            await base.Request();
            commandOutputProvider.Debug($"Finding runbook runs created between {minDate:yyyy-mm-dd} and {maxDate:yyyy-mm-dd} ...");

            await Repository.RunbookRuns
                .Paginate(projectsFilter,
                    runbooksFilter,
                    environmentsFilter,
                    tenantsFilter,
                    page => {
                        foreach(var run in page.Items) {
                            if(run.Created >= minDate.Value && run.Created <= maxDate.Value) {
                                if(whatIf) {
                                    commandOutputProvider.Information("[WhatIf] Run {RunId:l} created on {CreatedOn:2} would have been deleted", run.Id, run.Created.ToString());
                                    wouldDelete.Add(run);
                                } else {
                                    toDelete.Add(run);
                                    commandOutputProvider.Information("Deleting Run {RunId:l} created on {CreatedOn:2}", run.Id, run.Created.ToString());
                                }
                            }
                        }
                        return true; // We need to check all runs
                    })
                .ConfigureAwait(false);

            // Don't do anything else for WhatIf
            if (whatIf) return;

            foreach(var run in toDelete)
                await Repository.RunbookRuns.Delete(run);
        }

        public void PrintDefaultOutput()
        {
        }

        public void PrintJsonOutput()
        {
            var affectedRuns = whatIf ? wouldDelete : toDelete;
            commandOutputProvider.Json(
                affectedRuns.Select(run => new {
                    Id = run.Id,
                    Name = run.Name,
                    Created = run.Created,
                    Project = new { Id = run.ProjectId, Name = projectsById[run.ProjectId].Name },
                    Runbook = new { Id = run.RunbookId, Name = runbooksById[run.RunbookId].Name },
                    Environment = new { Id = run.EnvironmentId, Name = environmentsById[run.EnvironmentId].Name },
                    Tenant = new { Id = run.TenantId, Name = !string.IsNullOrEmpty(run.TenantId) ? tenantsById[run.TenantId].Name : null },
                    FailureEncountered = run.FailureEncountered,
                    Comments = run.Comments
                }));
        }
    }
}
