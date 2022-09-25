using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Octopus.Cli.Infrastructure;
using Octopus.Cli.Repositories;
using Octopus.Cli.Util;
using Octopus.Client;
using Octopus.Client.Model;
using Octopus.CommandLine;
using Octopus.CommandLine.Commands;
using Octopus.Versioning.Octopus;

namespace Octopus.Cli.Commands.RunbooksRun
{
    [Command("delete-runbookruns", Description = "Deletes a range of runbook runs.")]
    public class DeleteRunbookRunsCommand : RunbookRunCommandBase, ISupportFormattedOutput
    {
        List<RunbookRunResource> toDelete = new List<RunbookRunResource>();
        List<RunbookRunResource> wouldDelete = new List<RunbookRunResource>();
        DateTime minDate = new DateTime(1900, 1, 1);
        DateTime maxDate = DateTime.MaxValue;

        bool whatIf = false;

        public DeleteRunbookRunsCommand(IOctopusAsyncRepositoryFactory repositoryFactory, IOctopusFileSystem fileSystem, IOctopusClientFactory clientFactory, ICommandOutputProvider commandOutputProvider)
            : base(repositoryFactory, fileSystem, clientFactory, commandOutputProvider)
        {
            var options = Options.For("Deletion");
            options.Add<DateTime>("minCreateDate=", "[Optional] Earliest (inclusive) create date for the range of runbook runs to delete.", v => minDate = v);
            options.Add<DateTime>("maxCreateDate=", "[Optional] Latest (inclusive) create date for the range of runbooks to delete.", v => maxDate = v);
            options.Add<bool>("whatIf", "[Optional, Flag] if specified, releases won't actually be deleted, but will be listed as if simulating the command.", v => whatIf = true);
        }

        public override async Task Request()
        {
            await base.Request();
            commandOutputProvider.Debug("Finding runbook runs...");

            await Repository.RunbookRuns
                .Paginate(projectsFilter,
                    runbooksFilter,
                    environmentsFilter,
                    tenantsFilter,
                    page => {
                        foreach(var run in page.Items) {
                            if(run.Created >= minDate && run.Created <= maxDate) {
                                if(whatIf) {
                                    commandOutputProvider.Information("[WhatIf] Run {RunId:l} would have been deleted", run.Id);
                                    wouldDelete.Add(run);
                                } else {
                                    toDelete.Add(run);
                                    commandOutputProvider.Information("Deleting Run {RunId:l}", run.Id);
                                }
                            }
                        }
                        return true; // We need to check all runs
                    })
                .ConfigureAwait(false);

            // Don't do anything else for WhatIf
            if (whatIf) return;

            foreach (var run in toDelete)
                await Repository.Client.Delete(run.Link("Self")).ConfigureAwait(false);
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
