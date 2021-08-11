using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Octopus.Cli.Infrastructure;
using Octopus.Client;
using Octopus.Client.Exceptions;
using Octopus.Client.Extensibility;
using Octopus.Client.Model;
using Octopus.Client.Repositories.Async;
using Octopus.CommandLine.Commands;
using Serilog;

namespace Octopus.Cli.Util
{
    public static class RepositoryExtensions
    {
        static async Task<TResource> FindByNameOrIdOrFail<T, TResource>(this T repository,
            Func<string, Task<TResource>> findByNameFunc,
            string resourceTypeIdPrefix,
            string resourceTypeDisplayName,
            string nameOrId,
            string enclosingContextDescription = "",
            bool skipLog = false)
            where T : IGet<TResource>
            where TResource : Resource, INamedResource
        {
            TResource resourceById;
            if (!Regex.IsMatch(nameOrId,
                $@"^{Regex.Escape(resourceTypeIdPrefix)}-\d+$",
                RegexOptions.Compiled | RegexOptions.IgnoreCase))
                resourceById = null;
            else
                try
                {
                    resourceById = await repository.Get(nameOrId)
                        .ConfigureAwait(false);
                }
                catch (OctopusResourceNotFoundException)
                {
                    resourceById = null;
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
                throw new CouldNotFindException(resourceTypeDisplayName, nameOrId, enclosingContextDescription);

            if (resourceById != null
                && resourceByName != null
                && !string.Equals(resourceById.Id, resourceByName.Id, StringComparison.OrdinalIgnoreCase))
                throw new CommandException(
                    $"Ambiguous {resourceTypeDisplayName} reference '{nameOrId}' matches both '{resourceById.Name}' ({resourceById.Id}) and '{resourceByName.Name}' ({resourceByName.Id}).");

            var found = resourceById ?? resourceByName;

            if (!skipLog)
                Log.Logger.Debug($"Found {resourceTypeDisplayName}: {found.Name} ({found.Id})");

            return found;
        }

        
        static async Task<TResource[]> FindByNamesOrIdsOrFail<T, TResource>(this T repository,
            Func<string, Task<TResource>> findByNameFunc,
            string resourceTypeIdPrefix,
            string resourceTypeDisplayName,
            IEnumerable<string> namesOrIds,
            string enclosingContextDescription = "",
            bool skipLog = false)
            where T : IGet<TResource>
            where TResource : Resource, INamedResource
        {
            var findTasks = namesOrIds.Select(async nameOrId =>
            {
                try
                {
                    return new FindResourceResult<TResource>
                    {
                        Resource = await repository.FindByNameOrIdOrFail(findByNameFunc,
                                resourceTypeIdPrefix,
                                resourceTypeDisplayName,
                                nameOrId,
                                skipLog: true,
                                enclosingContextDescription: enclosingContextDescription)
                            .ConfigureAwait(false)
                    };
                }
                catch (CouldNotFindException)
                {
                    return new FindResourceResult<TResource> { MissingNameOrId = nameOrId };
                }
            });
            var results = await Task.WhenAll(findTasks)
                .ConfigureAwait(false);

            var missingNamesOrIds = results.Select(r => r.MissingNameOrId)
                .Where(m => m != null)
                .ToArray();
            if (missingNamesOrIds.Any())
                throw new CouldNotFindException(resourceTypeDisplayName,
                    missingNamesOrIds,
                    enclosingContextDescription);

            if (!skipLog)
                Log.Logger.Debug($"Found {resourceTypeDisplayName}{(results.Length == 1 ? "" : "s")}: "
                    + $"{string.Join(", ", results.Select(r => $"{r.Resource.Name} ({r.Resource.Id})"))}");

            return results.Select(r => r.Resource).ToArray();
        }

        public static Task<SpaceResource> FindByNameOrIdOrFail(this ISpaceRepository repo, string nameOrId)
        {
            return repo.FindByNameOrIdOrFail(n => repo.FindByName(n), "Spaces", "space", nameOrId);
        }

        public static Task<ProjectResource> FindByNameOrIdOrFail(this IProjectRepository repo, string nameOrId)
        {
            return repo.FindByNameOrIdOrFail(n => repo.FindByName(n), "Projects", "project", nameOrId);
        }

        public static Task<ProjectResource[]> FindByNamesOrIdsOrFail(this IProjectRepository repo,
            IEnumerable<string> namesOrIds)
        {
            return repo.FindByNamesOrIdsOrFail(n => repo.FindByName(n), "Projects", "project", namesOrIds);
        }

        public static Task<RunbookResource> FindByNameOrIdOrFail(this IRunbookRepository repo, string nameOrId, ProjectResource project)
        {
            return repo.FindByNameOrIdOrFail(n => repo.FindByName(project, n),
                "Runbooks",
                "runbook",
                nameOrId,
                $" in {project.Name}");
        }

        public static Task<ChannelResource> FindByNameOrIdOrFail(this IChannelRepository repo,
            ProjectResource project,
            string nameOrId)
        {
            return repo.FindByNameOrIdOrFail(n => repo.FindByName(project, n),
                "Channels",
                "channel",
                nameOrId,
                $" in {project.Name}");
        }
        
        internal static async Task<ChannelResource> FindGitRefChannelByNameOrIdOrFail(this IOctopusAsyncRepository repo,
            ProjectResource project,
            string nameOrId,
            string gitRef)
        {

            var resourceTypeIdPrefix = "Channels";
            var resourceTypeDisplayName = "channel";
            var enclosingContextDescription = $" in {project.Name}";
            var skipLog = false;

            ChannelResource resourceById;
            if (!Regex.IsMatch(nameOrId,
                $@"^{Regex.Escape(resourceTypeIdPrefix)}-\d+$",
                RegexOptions.Compiled | RegexOptions.IgnoreCase))
                resourceById = null;
            else
                try
                {
                    
                    resourceById = await repo.Channels.Beta().Get(project, nameOrId, gitRef);;
                }
                catch (OctopusResourceNotFoundException)
                {
                    resourceById = null;
                }

            ChannelResource resourceByName;
            try
            {
                // Duplicates missing FindByName in Client for Beta Repository
                resourceByName = await repo.Projects.Beta()
                    .GetAllChannels(project, gitRef)
                    .ContinueWith(d => 
                        d.Result.SingleOrDefault(named =>
                            string.Equals((named.Name ?? string.Empty).Trim(), nameOrId, StringComparison.OrdinalIgnoreCase))
                        ,TaskContinuationOptions.NotOnFaulted)
                    .ConfigureAwait(false);

            }
            catch (OctopusResourceNotFoundException)
            {
                resourceByName = null;
            }

            if (resourceById == null && resourceByName == null)
                throw new CouldNotFindException(resourceTypeDisplayName, nameOrId, enclosingContextDescription);

            if (resourceById != null
                && resourceByName != null
                && !string.Equals(resourceById.Id, resourceByName.Id, StringComparison.OrdinalIgnoreCase))
                throw new CommandException(
                    $"Ambiguous {resourceTypeDisplayName} reference '{nameOrId}' matches both '{resourceById.Name}' ({resourceById.Id}) and '{resourceByName.Name}' ({resourceByName.Id}).");

            var found = resourceById ?? resourceByName;

            if (!skipLog)
                Log.Logger.Debug($"Found {resourceTypeDisplayName}: {found.Name} ({found.Id})");

            return found;
        }

        public static Task<ChannelResource[]> FindByNamesOrIdsOrFail(this IChannelRepository repo,
            ProjectResource project,
            IEnumerable<string> namesOrIds)
        {
            return repo.FindByNamesOrIdsOrFail(n => repo.FindByName(project, n),
                "Channels",
                "channel",
                namesOrIds,
                $" in {project.Name}");
        }

        public static Task<EnvironmentResource> FindByNameOrIdOrFail(this IEnvironmentRepository repo, string nameOrId)
        {
            return repo.FindByNameOrIdOrFail(n => repo.FindByName(n), "Environments", "environment", nameOrId);
        }

        public static Task<EnvironmentResource[]> FindByNamesOrIdsOrFail(this IEnvironmentRepository repo,
            IEnumerable<string> namesOrIds)
        {
            return repo.FindByNamesOrIdsOrFail(n => repo.FindByName(n), "Environments", "environment", namesOrIds);
        }

        public static Task<TenantResource> FindByNameOrIdOrFail(this ITenantRepository repo, string nameOrId)
        {
            return repo.FindByNameOrIdOrFail(n => repo.FindByName(n), "Tenants", "tenant", nameOrId);
        }

        public static Task<TenantResource[]> FindByNamesOrIdsOrFail(this ITenantRepository repo,
            IEnumerable<string> namesOrIds)
        {
            return repo.FindByNamesOrIdsOrFail(n => repo.FindByName(n), "Tenants", "tenant", namesOrIds);
        }

        public static Task<LifecycleResource> FindByNameOrIdOrFail(this ILifecyclesRepository repo, string nameOrId)
        {
            return repo.FindByNameOrIdOrFail(n => repo.FindByName(n), "Lifecycles", "lifecycle", nameOrId);
        }

        class FindResourceResult<T>
        {
            public T Resource { get; set; }
            public string MissingNameOrId { get; set; }
        }
    }
}
