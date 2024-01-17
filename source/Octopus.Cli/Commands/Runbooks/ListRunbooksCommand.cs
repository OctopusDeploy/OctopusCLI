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

namespace Octopus.Cli.Commands.Runbooks
{
    [Command("list-runbooks", Description = "Lists runbooks by project.")]
    public class ListRunbooksCommand : RunbookCommandBase, ISupportFormattedOutput
    {
        List<RunbookResource> runbooks;

        public ListRunbooksCommand(IOctopusAsyncRepositoryFactory repositoryFactory, IOctopusFileSystem fileSystem, IOctopusClientFactory clientFactory, ICommandOutputProvider commandOutputProvider)
            : base(repositoryFactory, fileSystem, clientFactory, commandOutputProvider)
        {
        }

        public override async Task Request()
        {
            await base.Request();

            commandOutputProvider.Debug("Loading runbooks...");

            runbooks = await Repository.Runbooks
                .FindMany(x => projectsFilter.Contains(x.ProjectId))
                .ConfigureAwait(false);
        }

        public void PrintDefaultOutput()
        {
            commandOutputProvider.Information("Runbooks: {Count}", runbooks.Count);
            foreach (var project in projectsById.Values)
            {
                commandOutputProvider.Information(" - Project: {Project:l}", project.Name);

                foreach (var runbook in runbooks.Where(x => x.ProjectId == project.Id))
                {
                    var propertiesToLog = new List<string>();
                    propertiesToLog.AddRange(FormatRunbookPropertiesAsStrings(runbook));
                    foreach (var property in propertiesToLog)
                        commandOutputProvider.Information("    {Property:l}", property);
                    commandOutputProvider.Information("");
                }
            }
        }

        public void PrintJsonOutput()
        {
            commandOutputProvider.Json(projectsById.Values.Select(pr => new
            {
                Project = new { pr.Id, pr.Name },
                Ruunbooks = runbooks.Where(r => r.ProjectId == pr.Id)
                    .Select(r => new
                    {
                        r.Id,
                        r.Name,
                        r.Description
                    })
            }));
        }
    }
}
