using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Octopus.Cli.Infrastructure;
using Octopus.Client.Exceptions;
using Octopus.Client.Logging;
using Octopus.Client.Model;
using Octopus.Client.Repositories.Async;

namespace Octopus.Cli.Util
{
    public static class RepositoryExtensions
    {
        private static readonly ILog Logger = LogProvider.GetLogger(typeof(RepositoryExtensions));

        private static async Task<TResource> FindByNameOrIdOrFail<T, TResource>(this T repository,
            Func<string, Task<TResource>> findByNameFunc, string fixedIdPrefix, string typeDescription, string nameOrId,
            string inDescription = "", bool skipLog = false)
            where T : IGet<TResource> where TResource : Resource
        {
            if (!skipLog)
            {
                Logger.Debug($"Finding {typeDescription}: {nameOrId}");
            }

            TResource resourceById;
            if (!Regex.IsMatch(nameOrId, $@"^{Regex.Escape(fixedIdPrefix)}-\d+$",
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

            var resourceByName = await findByNameFunc(nameOrId)
                .ConfigureAwait(false);

            if (resourceById == null && resourceByName == null)
            {
                throw new CouldNotFindException(typeDescription, nameOrId, inDescription);
            }

            if (resourceById != null
                && resourceByName != null
                && !string.Equals(resourceById.Id, resourceByName.Id, StringComparison.OrdinalIgnoreCase))
            {
                throw new CommandException(
                    $"Ambiguous {typeDescription} reference '{nameOrId}' matches one {typeDescription} by name and another by id.");
            }

            return resourceById ?? resourceByName;
        }

        private static async Task<TResource[]> FindByNamesOrIdsOrFail<T, TResource>(this T repository,
            Func<string, Task<TResource>> findByNameFunc, string fixedIdPrefix, string typeDescription,
            IEnumerable<string> namesOrIds, string inDescription = "")
            where T : IGet<TResource> where TResource : Resource
        {
            var results = await Task.WhenAll(namesOrIds.Select<string, Task<(string missing, TResource resource)>>(
                async nameOrId =>
                {
                    try
                    {
                        return (null, await repository.FindByNameOrIdOrFail(findByNameFunc, fixedIdPrefix,
                                typeDescription, nameOrId, skipLog: true, inDescription: inDescription)
                            .ConfigureAwait(false));
                    }
                    catch (CouldNotFindException)
                    {
                        return (nameOrId, null);
                    }
                })).ConfigureAwait(false);
            var missing = results.Select(r => r.missing)
                .Where(m => m != null)
                .ToArray();
            if (missing.Any())
            {
                var missingStr = string.Join(", ", missing.Select(m => '"' + m + '"'));
                throw new CommandException(
                    $"The {typeDescription}{(missing.Length == 1 ? "" : "s")} {missingStr} "
                    + $"do{(missing.Length == 1 ? "es" : "")} not exist{inDescription} or the account does not have access.");
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
                nameOrId, inDescription: $" in {project.Name}");

        public static Task<ChannelResource[]> FindByNamesOrIdsOrFail(this IChannelRepository repo,
            ProjectResource project, IEnumerable<string> namesOrIds)
            => repo.FindByNamesOrIdsOrFail(n => repo.FindByName(project, n), "Channels", "channel",
                namesOrIds, inDescription: $" in {project.Name}");

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