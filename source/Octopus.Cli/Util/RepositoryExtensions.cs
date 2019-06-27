using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Octopus.Cli.Infrastructure;
using Octopus.Client.Exceptions;
using Octopus.Client.Extensibility;
using Octopus.Client.Logging;
using Octopus.Client.Model;
using Octopus.Client.Repositories.Async;

namespace Octopus.Cli.Util
{
    public static class RepositoryExtensions
    {
        private static readonly ILog Logger = LogProvider.GetLogger(typeof(RepositoryExtensions));

        private static async Task<TResource> FindByNameOrIdOrFail<T, TResource>(this T repository,
            Func<string, Task<TResource>> findByNameFunc, string resourceTypeIdPrefix, string resourceTypeDisplayName,
            string nameOrId, string enclosingContextDescription = "", bool skipLog = false)
            where T : IGet<TResource>
            where TResource : Resource, INamedResource
        {
            TResource resourceById;
            if (!Regex.IsMatch(nameOrId, $@"^{Regex.Escape(resourceTypeIdPrefix)}-\d+$",
                RegexOptions.Compiled | RegexOptions.IgnoreCase))
            {
                resourceById = null;
            }
            else
            {
                try
                {
                    resourceById = await repository.Get(nameOrId)
                        .ConfigureAwait(false);
                }
                catch (OctopusResourceNotFoundException)
                {
                    resourceById = null;
                }
            }

            TResource resourceByName;
            try
            {
                resourceByName = await findByNameFunc(nameOrId)
                    .ConfigureAwait(false);
            }
            catch (OctopusResourceNotFoundException)
            {
                resourceByName = null;
            }

            if (resourceById == null && resourceByName == null)
            {
                throw new CouldNotFindException(resourceTypeDisplayName, nameOrId, enclosingContextDescription);
            }

            if (resourceById != null
                && resourceByName != null
                && !string.Equals(resourceById.Id, resourceByName.Id, StringComparison.OrdinalIgnoreCase))
            {
                throw new CommandException(
                    $"Ambiguous {resourceTypeDisplayName} reference '{nameOrId}' matches both '{resourceById.Name}' ({resourceById.Id}) and '{resourceByName.Name}' ({resourceByName.Id}).");
            }

            var found = resourceById ?? resourceByName;

            if (!skipLog)
            {
                Logger.Debug($"Found {resourceTypeDisplayName}: {found.Name} ({found.Id})");
            }

            return found;
        }

        private static async Task<TResource[]> FindByNamesOrIdsOrFail<T, TResource>(this T repository,
            Func<string, Task<TResource>> findByNameFunc, string resourceTypeIdPrefix, string resourceTypeDisplayName,
            IEnumerable<string> namesOrIds, string enclosingContextDescription = "", bool skipLog = false)
            where T : IGet<TResource>
            where TResource : Resource, INamedResource
        {
            var results = await Task.WhenAll(namesOrIds.Select<string, Task<(string missing, TResource resource)>>(
                async nameOrId =>
                {
                    try
                    {
                        return (null,
                            await repository.FindByNameOrIdOrFail(findByNameFunc, resourceTypeIdPrefix,
                                    resourceTypeDisplayName, nameOrId, skipLog: true,
                                    enclosingContextDescription: enclosingContextDescription)
                                .ConfigureAwait(false));
                    }
                    catch (CouldNotFindException)
                    {
                        return (nameOrId, null);
                    }
                })).ConfigureAwait(false);
            var missingNamesOrIds = results.Select(r => r.missing)
                .Where(m => m != null)
                .ToArray();
            if (missingNamesOrIds.Any())
            {
                var missingStr = string.Join(", ", missingNamesOrIds.Select(m => '"' + m + '"'));
                throw new CommandException(
                    $"The {resourceTypeDisplayName}{(missing.Length == 1 ? "" : "s")} {missingStr} "
                    + $"do{(missing.Length == 1 ? "es" : "")} not exist{enclosingContextDescription} or the account does not have access.");
            }

            if (!skipLog)
            {
                Logger.Debug($"Found {resourceTypeDisplayName}{(results.Length == 1 ? "" : "s")}: "
                             + $"{string.Join(", ", results.Select(r => $"{r.resource.Name} ({r.resource.Id})"))}");
            }

            return results.Select(r => r.resource).ToArray();
        }

        public static Task<SpaceResource> FindByNameOrIdOrFail(this ISpaceRepository repo, string nameOrId)
            => repo.FindByNameOrIdOrFail(n => repo.FindByName(n), "Spaces", "space", nameOrId);

        public static Task<ProjectResource> FindByNameOrIdOrFail(this IProjectRepository repo, string nameOrId)
            => repo.FindByNameOrIdOrFail(n => repo.FindByName(n), "Projects", "project", nameOrId);

        public static Task<ProjectResource[]> FindByNamesOrIdsOrFail(this IProjectRepository repo,
            IEnumerable<string> namesOrIds)
            => repo.FindByNamesOrIdsOrFail(n => repo.FindByName(n), "Projects", "project", namesOrIds);

        public static Task<ChannelResource> FindByNameOrIdOrFail(this IChannelRepository repo, ProjectResource project,
            string nameOrId)
            => repo.FindByNameOrIdOrFail(n => repo.FindByName(project, n), "Channels", "channel",
                nameOrId, enclosingContextDescription: $" in {project.Name}");

        public static Task<ChannelResource[]> FindByNamesOrIdsOrFail(this IChannelRepository repo,
            ProjectResource project, IEnumerable<string> namesOrIds)
            => repo.FindByNamesOrIdsOrFail(n => repo.FindByName(project, n), "Channels", "channel",
                namesOrIds, enclosingContextDescription: $" in {project.Name}");

        public static Task<EnvironmentResource> FindByNameOrIdOrFail(this IEnvironmentRepository repo, string nameOrId)
            => repo.FindByNameOrIdOrFail(n => repo.FindByName(n), "Environments", "environment", nameOrId);

        public static Task<EnvironmentResource[]> FindByNamesOrIdsOrFail(this IEnvironmentRepository repo,
            IEnumerable<string> namesOrIds)
            => repo.FindByNamesOrIdsOrFail(n => repo.FindByName(n), "Environments", "environment", namesOrIds);

        public static Task<TenantResource> FindByNameOrIdOrFail(this ITenantRepository repo, string nameOrId)
            => repo.FindByNameOrIdOrFail(n => repo.FindByName(n), "Tenants", "tenant", nameOrId);

        public static Task<TenantResource[]> FindByNamesOrIdsOrFail(this ITenantRepository repo,
            IEnumerable<string> namesOrIds)
            => repo.FindByNamesOrIdsOrFail(n => repo.FindByName(n), "Tenants", "tenant", namesOrIds);

        public static Task<LifecycleResource> FindByNameOrIdOrFail(this ILifecyclesRepository repo, string nameOrId)
            => repo.FindByNameOrIdOrFail(n => repo.FindByName(n), "Lifecycles", "lifecycle", nameOrId);
    }
}