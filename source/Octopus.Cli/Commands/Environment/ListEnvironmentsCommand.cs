﻿using System;
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

namespace Octopus.Cli.Commands.Environment
{
    [Command("list-environments", Description = "Lists environments.")]
    public class ListEnvironmentsCommand : ApiCommand, ISupportFormattedOutput
    {
        List<EnvironmentResource> environments;

        public ListEnvironmentsCommand(IOctopusAsyncRepositoryFactory repositoryFactory, IOctopusFileSystem fileSystem, IOctopusClientFactory clientFactory, IOctopusCliCommandOutputProvider commandOutputProvider)
            : base(clientFactory, repositoryFactory, fileSystem, commandOutputProvider)
        {
        }

        public async Task Request()
        {
            environments = await Repository.Environments.FindAll().ConfigureAwait(false);
        }

        public void PrintDefaultOutput()
        {
            commandOutputProvider.Information("Environments: {Count}", environments.Count);

            foreach (var environment in environments)
                commandOutputProvider.Information(" - {Environment:l} (ID: {Id:l})", environment.Name, environment.Id);
        }

        public void PrintJsonOutput()
        {
            commandOutputProvider.Json(
                environments.Select(environment => new
                {
                    environment.Id,
                    environment.Name
                }));
        }
    }
}
