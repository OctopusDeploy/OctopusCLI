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

namespace Octopus.Cli.Commands.Tenant
{
    [Command("list-tenants", Description = "Lists tenants.")]
    public class ListTenantsCommand : ApiCommand, ISupportFormattedOutput
    {
        List<TenantResource> tenants;

        public ListTenantsCommand(IOctopusAsyncRepositoryFactory repositoryFactory, IOctopusFileSystem fileSystem, IOctopusClientFactory clientFactory, IOctopusCliCommandOutputProvider commandOutputProvider)
            : base(clientFactory, repositoryFactory, fileSystem, commandOutputProvider)
        {
        }

        public async Task Request()
        {
            var multiTenancyStatus = await Repository.Tenants.Status().ConfigureAwait(false);
            if (multiTenancyStatus.Enabled)
                tenants = await Repository.Tenants.FindAll().ConfigureAwait(false);
            else
                throw new CommandException("Multi-Tenancy is not enabled");
        }

        public void PrintDefaultOutput()
        {
            commandOutputProvider.Information("Tenants: {Count}", tenants.Count);

            foreach (var tenant in tenants.OrderBy(m => m.Name))
                commandOutputProvider.Information(" - {Tenant:l} (ID: {Count})", tenant.Name, tenant.Id);
        }

        public void PrintJsonOutput()
        {
            commandOutputProvider.Json(tenants.OrderBy(x => x.Name)
                .Select(t => new
                {
                    t.Id,
                    t.Name
                }));
        }
    }
}
