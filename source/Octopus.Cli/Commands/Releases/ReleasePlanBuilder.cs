using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Octopus.Cli.Infrastructure;
using Octopus.Cli.Util;
using Octopus.Client;
using Octopus.Client.Model;
using Octopus.CommandLine;
using Octopus.CommandLine.Commands;
using Serilog;

namespace Octopus.Cli.Commands.Releases
{
    public class ReleasePlanBuilder : IReleasePlanBuilder
    {
        public const string GitReferenceMissingForVersionControlledProjectErrorMessage =
            "Attempting to create a release for a version controlled project, but no git reference has been provided. Use the gitRef parameter to supply a git reference.";

        readonly IPackageVersionResolver versionResolver;
        readonly IChannelVersionRuleTester versionRuleTester;
        readonly ICommandOutputProvider commandOutputProvider;

        public ReleasePlanBuilder(ILogger log, IPackageVersionResolver versionResolver, IChannelVersionRuleTester versionRuleTester, ICommandOutputProvider commandOutputProvider)
        {
            this.versionResolver = versionResolver;
            this.versionRuleTester = versionRuleTester;
            this.commandOutputProvider = commandOutputProvider;
        }

        public static string GitReferenceSuppliedForDatabaseProjectErrorMessage(string gitReference)
        {
            return $"Attempting to create a release from version control because the git reference {gitReference} was provided. The selected project is not a version controlled project.";
        }

        public async Task<ReleasePlan> Build(IOctopusAsyncRepository repository,
            ProjectResource project,
            ChannelResource channel,
            string versionPreReleaseTag,
            string gitReference)
        {
            return string.IsNullOrWhiteSpace(gitReference)
                ? await BuildReleaseFromDatabase(repository, project, channel, versionPreReleaseTag)
                : await BuildReleaseFromVersionControl(repository,
                    project,
                    channel,
                    versionPreReleaseTag,
                    gitReference);
        }

        async Task<ReleasePlan> BuildReleaseFromVersionControl(IOctopusAsyncRepository repository,
            ProjectResource project,
            ChannelResource channel,
            string versionPreReleaseTag,
            string gitReference)
        {
            if (repository == null) throw new ArgumentNullException(nameof(repository));
            if (project == null) throw new ArgumentNullException(nameof(project));
            if (!project.IsVersionControlled)
                throw new CommandException(GitReferenceSuppliedForDatabaseProjectErrorMessage(gitReference));
            commandOutputProvider.Debug($"Finding deployment process at git reference {gitReference}...");
            var deploymentProcess = await repository.DeploymentProcesses.Beta().Get(project, gitReference);
            if (deploymentProcess == null)
                throw new CouldNotFindException(
                    $"a deployment process for project {project.Name} and git reference {gitReference}");

            commandOutputProvider.Debug($"Finding release template at git reference {gitReference}...");
            var releaseTemplate = await repository.DeploymentProcesses.GetTemplate(deploymentProcess, channel).ConfigureAwait(false);
            if (releaseTemplate == null)
                throw new CouldNotFindException(
                    $"a release template for project {project.Name}, channel {channel.Name} and git reference {gitReference}");

            return await Build(repository,
                project,
                channel,
                versionPreReleaseTag,
                releaseTemplate,
                deploymentProcess);
        }

        async Task<ReleasePlan> BuildReleaseFromDatabase(IOctopusAsyncRepository repository, ProjectResource project, ChannelResource channel, string versionPreReleaseTag)
        {
            if (repository == null) throw new ArgumentNullException(nameof(repository));
            if (project == null) throw new ArgumentNullException(nameof(project));
            if (project.IsVersionControlled)
                throw new CommandException(GitReferenceMissingForVersionControlledProjectErrorMessage);

            commandOutputProvider.Debug("Finding deployment process...");
            var deploymentProcess = await repository.DeploymentProcesses.Get(project.DeploymentProcessId).ConfigureAwait(false);
            if (deploymentProcess == null)
                throw new CouldNotFindException(
                    $"a deployment process for project {project.Name}");

            commandOutputProvider.Debug("Finding release template...");
            var releaseTemplate = await repository.DeploymentProcesses.GetTemplate(deploymentProcess, channel).ConfigureAwait(false);
            if (releaseTemplate == null)
                throw new CouldNotFindException(
                    $"a release template for project {project.Name} and channel {channel.Name}");

            return await Build(repository,
                project,
                channel,
                versionPreReleaseTag,
                releaseTemplate,
                deploymentProcess);
        }

        async Task<ReleasePlan> Build(IOctopusAsyncRepository repository,
            ProjectResource project,
            ChannelResource channel,
            string versionPreReleaseTag,
            ReleaseTemplateResource releaseTemplate,
            DeploymentProcessResource deploymentProcess)
        {
            var plan = new ReleasePlan(project,
                channel,
                releaseTemplate,
                deploymentProcess,
                versionResolver);

            if (plan.UnresolvedSteps.Any())
            {
                commandOutputProvider.Debug(
                    "The package version for some steps was not specified. Going to try and resolve those automatically...");

                var allRelevantFeeds = await LoadFeedsForSteps(repository, project, plan.UnresolvedSteps);

                foreach (var unresolved in plan.UnresolvedSteps)
                {
                    if (!unresolved.IsResolveable)
                    {
                        commandOutputProvider.Error(
                            "The version number for step '{Step:l}' cannot be automatically resolved because the feed or package ID is dynamic.",
                            unresolved.ActionName);
                        continue;
                    }

                    if (!string.IsNullOrEmpty(versionPreReleaseTag))
                        commandOutputProvider.Debug("Finding latest package with pre-release '{Tag:l}' for step: {StepName:l}",
                            versionPreReleaseTag,
                            unresolved.ActionName);
                    else
                        commandOutputProvider.Debug("Finding latest package for step: {StepName:l}", unresolved.ActionName);

                    if (!allRelevantFeeds.TryGetValue(unresolved.PackageFeedId, out var feed))
                        throw new CommandException(string.Format(
                            "Could not find a feed with ID {0}, which is used by step: " + unresolved.ActionName,
                            unresolved.PackageFeedId));

                    var filters = BuildChannelVersionFilters(unresolved.ActionName, unresolved.PackageReferenceName, channel);
                    filters["packageId"] = unresolved.PackageId;
                    if (!string.IsNullOrWhiteSpace(versionPreReleaseTag))
                        filters["preReleaseTag"] = versionPreReleaseTag;

                    var packages = await repository.Client.Get<List<PackageResource>>(feed.Link("SearchTemplate"), filters)
                        .ConfigureAwait(false);
                    var latestPackage = packages.FirstOrDefault();

                    if (latestPackage == null)
                    {
                        commandOutputProvider.Error(
                            "Could not find any packages with ID '{PackageId:l}' in the feed '{FeedUri:l}'",
                            unresolved.PackageId,
                            feed.Name);
                    }
                    else
                    {
                        commandOutputProvider.Debug("Selected '{PackageId:l}' version '{Version:l}' for '{StepName:l}'",
                            latestPackage.PackageId,
                            latestPackage.Version,
                            unresolved.ActionName);
                        unresolved.SetVersionFromLatest(latestPackage.Version);
                    }
                }
            }

            // Test each step in this plan satisfies the channel version rules
            if (channel != null)
                foreach (var step in plan.PackageSteps)
                {
                    // Note the rule can be null, meaning: anything goes
                    var rule = channel.Rules.SingleOrDefault(r => r.ActionPackages.Any(pkg =>
                        pkg.DeploymentActionNameMatches(step.ActionName) &&
                        pkg.PackageReferenceNameMatches(step.PackageReferenceName)));
                    var result = await versionRuleTester.Test(repository, rule, step.Version, step.PackageFeedId).ConfigureAwait(false);
                    step.SetChannelVersionRuleTestResult(result);
                }

            return plan;
        }

        static async Task<Dictionary<string, FeedResource>> LoadFeedsForSteps(IOctopusAsyncRepository repository, ProjectResource project, IEnumerable<ReleasePlanItem> steps)
        {
            // PackageFeedId can be an id or a name
            var allRelevantFeedIdOrName = steps.Select(step => step.PackageFeedId).ToArray();
            var allRelevantFeeds = project.IsVersionControlled
                ? (await repository.Feeds.FindByNames(allRelevantFeedIdOrName).ConfigureAwait(false)).ToDictionary(feed => feed.Name)
                : (await repository.Feeds.Get(allRelevantFeedIdOrName).ConfigureAwait(false)).ToDictionary(feed => feed.Id);

            return allRelevantFeeds;
        }

        IDictionary<string, object> BuildChannelVersionFilters(string stepName, string packageReferenceName, ChannelResource channel)
        {
            var filters = new Dictionary<string, object>();

            if (channel == null)
                return filters;

            var rule = channel.Rules.FirstOrDefault(r => r.ActionPackages.Any(pkg => pkg.DeploymentActionNameMatches(stepName) && pkg.PackageReferenceNameMatches(packageReferenceName)));

            if (rule == null)
                return filters;

            if (!string.IsNullOrWhiteSpace(rule.VersionRange))
                filters["versionRange"] = rule.VersionRange;

            if (!string.IsNullOrWhiteSpace(rule.Tag))
                filters["preReleaseTag"] = rule.Tag;

            return filters;
        }
    }
}
