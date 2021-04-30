﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Octopus.Cli.Infrastructure;
using Octopus.Cli.Util;
using Octopus.Client;
using Octopus.Client.Model;
using Octopus.CommandLine;
using Octopus.CommandLine.Commands;
using Octopus.Versioning.Octopus;

namespace Octopus.Cli.Repositories
{
    public class OctopusRepositoryCommonQueries
    {
        static readonly OctopusVersionParser OctopusVersionParser = new OctopusVersionParser();
        readonly IOctopusAsyncRepository repository;
        readonly ICommandOutputProvider commandOutputProvider;

        public OctopusRepositoryCommonQueries(IOctopusAsyncRepository repository, ICommandOutputProvider commandOutputProvider)
        {
            this.repository = repository;
            this.commandOutputProvider = commandOutputProvider;
        }

        public async Task<ProjectResource> GetProjectByName(string projectName)
        {
            commandOutputProvider.Debug("Finding project: {Project:l}", projectName);
            var project = await repository.Projects.FindByName(projectName).ConfigureAwait(false);
            if (project == null)
                throw new CouldNotFindException("a project named", projectName);
            return project;
        }

        public async Task<EnvironmentResource> GetEnvironmentByName(string environmentName)
        {
            commandOutputProvider.Debug("Finding environment: {Environment:l}", environmentName);
            var environment = await repository.Environments.FindByName(environmentName).ConfigureAwait(false);
            if (environment == null)
                throw new CouldNotFindException("an environment named", environmentName);
            return environment;
        }

        public async Task<ReleaseResource> GetReleaseByVersion(string versionNumber, ProjectResource project, ChannelResource channel)
        {
            string message;
            ReleaseResource releaseToPromote = null;
            if (string.Equals("latest", versionNumber, StringComparison.CurrentCultureIgnoreCase))
            {
                message = channel == null
                    ? "latest release for project"
                    : $"latest release in channel '{channel.Name}'";

                commandOutputProvider.Debug("Finding {Message:l}", message);

                var releases = await repository
                    .Projects
                    .GetReleases(project)
                    .ConfigureAwait(false);

                if (channel == null)
                    releaseToPromote = releases
                        .Items // We only need the first page
                        .OrderByDescending(r => OctopusVersionParser.Parse(r.Version))
                        .FirstOrDefault();
                else
                    await releases.Paginate(repository,
                            page =>
                            {
                                releaseToPromote = page.Items.OrderByDescending(r => OctopusVersionParser.Parse(r.Version))
                                    .FirstOrDefault(r => r.ChannelId == channel.Id);

                                // If we haven't found one yet, keep paginating
                                return releaseToPromote == null;
                            })
                        .ConfigureAwait(false);
            }
            else
            {
                message = $"release {versionNumber}";
                commandOutputProvider.Debug("Finding {Message:l}", message);
                releaseToPromote = await repository.Projects.GetReleaseByVersion(project, versionNumber).ConfigureAwait(false);
            }

            if (releaseToPromote == null)
                throw new CouldNotFindException($"the {message}", project.Name);
            return releaseToPromote;
        }

        public async Task<IReadOnlyList<TenantResource>> FindTenants(IReadOnlyList<string> tenantNames, IReadOnlyList<string> tenantTags)
        {
            if (!tenantNames.Any() && !tenantTags.Any())
                return new List<TenantResource>(0);

            if (!await repository.SupportsTenants().ConfigureAwait(false))
                throw new CommandException(
                    "Your Octopus Server does not support tenants, which was introduced in Octopus 3.4. Please upgrade your Octopus Server, enable the multi-tenancy feature or remove the --tenant and --tenantTag arguments.");

            var tenantsByName = FindTenantsByName(tenantNames).ConfigureAwait(false);
            var tenantsByTags = FindTenantsByTags(tenantTags).ConfigureAwait(false);

            var distinctTenants = (await tenantsByTags)
                .Concat(await tenantsByName)
                .GroupBy(t => t.Id)
                .Select(g => g.First())
                .ToList();

            return distinctTenants;
        }

        async Task<IEnumerable<TenantResource>> FindTenantsByName(IReadOnlyList<string> tenantNames)
        {
            if (!tenantNames.Any())
                return Enumerable.Empty<TenantResource>();

            if (tenantNames.Contains("*"))
                return await repository.Tenants.FindAll().ConfigureAwait(false);

            var tenantsByName = await repository.Tenants.FindByNames(tenantNames).ConfigureAwait(false);
            var missing = tenantsByName == null || !tenantsByName.Any()
                ? tenantNames.ToArray()
                : tenantNames.Except(tenantsByName.Select(e => e.Name), StringComparer.OrdinalIgnoreCase).ToArray();

            var tenantsById = await repository.Tenants.Get(missing).ConfigureAwait(false);

            missing = tenantsById == null || !tenantsById.Any()
                ? missing
                : missing.Except(tenantsById.Select(e => e.Id), StringComparer.OrdinalIgnoreCase).ToArray();

            if (missing.Any())
                throw new ArgumentException($"Could not find the {"tenant" + (missing.Length == 1 ? "" : "s")} {string.Join(", ", missing)} on the Octopus Server.");

            var allTenants = Enumerable.Empty<TenantResource>();
            if (tenantsById != null)
                allTenants = allTenants.Concat(tenantsById);
            if (tenantsByName != null)
                allTenants = allTenants.Concat(tenantsByName);

            return allTenants;
        }

        async Task<IEnumerable<TenantResource>> FindTenantsByTags(IReadOnlyList<string> tenantTags)
        {
            if (!tenantTags.Any())
                return Enumerable.Empty<TenantResource>();

            var tenantsByTag = await repository.Tenants.FindAll(null, tenantTags.ToArray()).ConfigureAwait(false);

            if (!tenantsByTag.Any())
                throw new ArgumentException($"Could not find any tenants matching the tags {string.Join(", ", tenantTags)}");

            return tenantsByTag;
        }
    }
}
