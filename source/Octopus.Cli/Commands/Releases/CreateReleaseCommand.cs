﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Octopus.Cli.Commands.Deployment;
using Octopus.Cli.Diagnostics;
using Octopus.Cli.Infrastructure;
using Octopus.Cli.Repositories;
using Octopus.Cli.Util;
using Octopus.Client;
using Octopus.Client.Exceptions;
using Octopus.Client.Model;
using Serilog;
using Serilog.Events;

namespace Octopus.Cli.Commands.Releases
{
    [Command("create-release", Description = "Creates (and, optionally, deploys) a release.")]
    public class CreateReleaseCommand : DeploymentCommandBase, ISupportFormattedOutput
    {
        private readonly IReleasePlanBuilder releasePlanBuilder;
        ReleaseResource release;
        ProjectResource project;
        ReleasePlan plan;
        string versionNumber;

        public CreateReleaseCommand(IOctopusAsyncRepositoryFactory repositoryFactory, IOctopusFileSystem fileSystem, IPackageVersionResolver versionResolver, IReleasePlanBuilder releasePlanBuilder, IOctopusClientFactory clientFactory, ICommandOutputProvider commandOutputProvider, ExecutionResourceWaiter.Factory executionResourceWaiterFactory)
            : base(repositoryFactory, fileSystem, clientFactory, commandOutputProvider, executionResourceWaiterFactory)
        {
            this.releasePlanBuilder = releasePlanBuilder;

            var options = Options.For("Release creation");
            options.Add<string>("project=", "Name or ID of the project", v => ProjectNameOrId = v);
            options.Add<string>("defaultPackageVersion=|packageVersion=", "Default version number of all packages to use for this release. Override per-package using --package.", versionResolver.Default);
            options.Add<string>("version=|releaseNumber=", "[Optional] Release number to use for the new release.", v => VersionNumber = v);
            options.Add<string>("channel=", "[Optional] Name or ID of the channel to use for the new release. Omit this argument to automatically select the best channel.", v => ChannelNameOrId = v);
            options.Add<string>("package=", "[Optional] Version number to use for a package in the release. Format: StepName:Version or PackageID:Version or StepName:PackageName:Version. StepName, PackageID, and PackageName can be replaced with an asterisk. An asterisk will be assumed for StepName, PackageID, or PackageName if they are omitted.", v => versionResolver.Add(v));
            options.Add<string>("packagesFolder=", "[Optional] A folder containing NuGet packages from which we should get versions.", v => {v.CheckForIllegalPathCharacters("packagesFolder"); versionResolver.AddFolder(v);});
            options.Add<string>("releaseNotes=", "[Optional] Release Notes for the new release. Styling with Markdown is supported.", v => ReleaseNotes = v);
            options.Add<string>("releaseNoteFile=|releaseNotesFile=", "[Optional] Path to a file that contains Release Notes for the new release. Supports Markdown files.", ReadReleaseNotesFromFile);
            options.Add<bool>("ignoreExisting", "[Optional, Flag] Don't create this release if there is already one with the same version number.", v => IgnoreIfAlreadyExists = true);
            options.Add<bool>("ignoreChannelRules", "[Optional, Flag] Create the release ignoring any version rules specified by the channel.", v => IgnoreChannelRules = true);
            options.Add<string>("packagePrerelease=", "[Optional] Pre-release for latest version of all packages to use for this release.", v => VersionPreReleaseTag = v);
            options.Add<bool>("whatIf", "[Optional, Flag] Perform a dry run but don't actually create/deploy release.", v => WhatIf = true);

            options = Options.For("Deployment");
            options.Add<string>("deployTo=", "[Optional] Name or ID of the environment to automatically deploy to, e.g., 'Production' or 'Environments-1'; specify this argument multiple times to deploy to multiple environments.", v => DeployToEnvironmentNamesOrIds.Add(v));
        }

        public string ChannelNameOrId { get; set; }
        public string VersionNumber { get; set; }
        public string ReleaseNotes { get; set; }
        public bool IgnoreIfAlreadyExists { get; set; }
        public bool IgnoreChannelRules { get; set; }
        public string VersionPreReleaseTag { get; set; }
        public bool WhatIf { get; set; }

        protected override async Task ValidateParameters()
        {
            if (!string.IsNullOrWhiteSpace(ChannelNameOrId) && !await Repository.SupportsChannels().ConfigureAwait(false))
                throw new CommandException("Your Octopus Server does not support channels, which was introduced in Octopus 3.2. Please upgrade your Octopus Server, or remove the --channel argument.");

            await base.ValidateParameters();
        }

        public async Task Request()
        {
            var serverSupportsChannels = await ServerSupportsChannels();
            commandOutputProvider.Debug(serverSupportsChannels ? "This Octopus Server supports channels" : "This Octopus Server does not support channels");

            project = await Repository.Projects.FindByNameOrIdOrFail(ProjectNameOrId).ConfigureAwait(false);

            plan = await BuildReleasePlan(project).ConfigureAwait(false);


            if (!string.IsNullOrWhiteSpace(VersionNumber))
            {
                versionNumber = VersionNumber;
                commandOutputProvider.Debug("Using version number provided on command-line: {Version:l}", versionNumber);
            }
            else if (!string.IsNullOrWhiteSpace(plan.ReleaseTemplate.NextVersionIncrement))
            {
                versionNumber = plan.ReleaseTemplate.NextVersionIncrement;
                commandOutputProvider.Debug("Using version number from release template: {Version:l}", versionNumber);
            }
            else if (!string.IsNullOrWhiteSpace(plan.ReleaseTemplate.VersioningPackageStepName))
            {
                versionNumber = plan.GetActionVersionNumber(plan.ReleaseTemplate.VersioningPackageStepName, plan.ReleaseTemplate.VersioningPackageReferenceName);
                commandOutputProvider.Debug("Using version number from package step: {Version:l}", versionNumber);
            }
            else
            {
                throw new CommandException(
                    "A version number was not specified and could not be automatically selected.");
            }

            commandOutputProvider.Write(
                plan.IsViableReleasePlan() ? LogEventLevel.Information : LogEventLevel.Warning,
                "Release plan for {Project:l} {Version:l}" + System.Environment.NewLine + "{Plan:l}",
                project.Name, versionNumber, plan.FormatAsTable()
            );
            if (plan.HasUnresolvedSteps())
            {
                throw new CommandException(
                    "Package versions could not be resolved for one or more of the package packageSteps in this release. See the errors above for details. Either ensure the latest version of the package can be automatically resolved, or set the version to use specifically by using the --package argument.");
            }
            if (plan.ChannelHasAnyEnabledSteps() == false)
            {
                if (serverSupportsChannels)
                {
                    throw new CommandException($"Channel {plan.Channel.Name} has no available steps");
                }
                else
                {
                    throw new CommandException($"Plan has no available steps");
                }
            }

            if (plan.HasStepsViolatingChannelVersionRules())
            {
                if (IgnoreChannelRules)
                {
                    commandOutputProvider.Warning("At least one step violates the package version rules for the Channel '{Channel:l}'. Forcing the release to be created ignoring these rules...", plan.Channel.Name);
                }
                else
                {
                    throw new CommandException(
                        $"At least one step violates the package version rules for the Channel '{plan.Channel.Name}'. Either correct the package versions for this release, let Octopus select the best channel by omitting the --channel argument, select a different channel using --channel=MyChannel argument, or ignore these version rules altogether by using the --ignoreChannelRules argument.");
                }
            }

            if (IgnoreIfAlreadyExists)
            {
                commandOutputProvider.Debug("Checking for existing release for {Project:l} {Version:l} because you specified --ignoreExisting...", project.Name, versionNumber);
                try
                {
                    var found = await Repository.Projects.GetReleaseByVersion(project, versionNumber)
                        .ConfigureAwait(false);
                    if (found != null)
                    {
                        commandOutputProvider.Information("A release of {Project:l} with the number {Version:l} already exists, and you specified --ignoreExisting, so we won't even attempt to create the release.", project.Name, versionNumber);
                        return;
                    }
                }
                catch (OctopusResourceNotFoundException)
                {
                    // Expected
                    commandOutputProvider.Debug("No release exists - the coast is clear!");
                }
            }

            if (WhatIf)
            {
                // We were just doing a dry run - bail out here
                if (DeployToEnvironmentNamesOrIds.Any())
                    commandOutputProvider.Information("[WhatIf] This release would have been created using the release plan and deployed to {Environments:l}", DeployToEnvironmentNamesOrIds.CommaSeperate());
                else
                    commandOutputProvider.Information("[WhatIf] This release would have been created using the release plan");
            }
            else
            {
                // Actually create the release!
                commandOutputProvider.Debug("Creating release...");

                // if no release notes were provided on the command line, but the project has a template, then use the template
                if (string.IsNullOrWhiteSpace(ReleaseNotes) && !string.IsNullOrWhiteSpace(project.ReleaseNotesTemplate))
                {
                    ReleaseNotes = project.ReleaseNotesTemplate;
                }

                release = await Repository.Releases.Create(new ReleaseResource(versionNumber, project.Id, plan.Channel?.Id)
                    {
                        ReleaseNotes = ReleaseNotes,
                        SelectedPackages = plan.GetSelections()
                    }, ignoreChannelRules: IgnoreChannelRules)
                    .ConfigureAwait(false);

                commandOutputProvider.Information("Release {Version:l} created successfully!", release.Version);
                commandOutputProvider.ServiceMessage("setParameter", new { name = "octo.releaseNumber", value = release.Version });
                commandOutputProvider.TfsServiceMessage(ServerBaseUrl, project, release);

                await DeployRelease(project, release).ConfigureAwait(false);
            }
        }

        private async Task<ReleasePlan> BuildReleasePlan(ProjectResource project)
        {
            if (!string.IsNullOrWhiteSpace(ChannelNameOrId))
            {
                commandOutputProvider.Information("Building release plan for channel '{Channel:l}'...", ChannelNameOrId);
                var matchingChannel = await Repository.Channels.FindByNameOrIdOrFail(project, ChannelNameOrId).ConfigureAwait(false);

                return await releasePlanBuilder.Build(Repository, project, matchingChannel, VersionPreReleaseTag).ConfigureAwait(false);
            }

            // All Octopus 3.2+ servers should have the Channels hypermedia link, we should use the channel information
            // to select the most appropriate channel, or provide enough information to proceed from here
            if (await ServerSupportsChannels().ConfigureAwait(false))
            {
                commandOutputProvider.Debug("Automatically selecting the best channel for this release...");
                return await AutoSelectBestReleasePlanOrThrow(project).ConfigureAwait(false);
            }

            // Compatibility: this has to cater for Octopus before Channels existed
            commandOutputProvider.Information("Building release plan without a channel for Octopus Server without channels support...");
            return await releasePlanBuilder.Build(Repository, project, null, VersionPreReleaseTag).ConfigureAwait(false);
        }

        private Task<bool> ServerSupportsChannels()
        {
            return Repository.HasLink("Channels");
        }

        async Task<ReleasePlan> AutoSelectBestReleasePlanOrThrow(ProjectResource project)
        {
            // Build a release plan for each channel to determine which channel is the best match for the provided options
            var channels = await Repository.Projects.GetChannels(project).ConfigureAwait(false);
            var candidateChannels = await channels.GetAllPages(Repository).ConfigureAwait(false);
            var releasePlans = new List<ReleasePlan>();
            foreach (var channel in candidateChannels)
            {
                commandOutputProvider.Information("Building a release plan for Channel '{Channel:l}'...", channel.Name);

                var plan = await releasePlanBuilder.Build(Repository, project, channel, VersionPreReleaseTag).ConfigureAwait(false);
                releasePlans.Add(plan);
                if (plan.ChannelHasAnyEnabledSteps() == false)
                {
                    commandOutputProvider.Warning($"Channel {channel.Name} does not contain any packageSteps");
                }
            }

            var viablePlans = releasePlans.Where(p => p.IsViableReleasePlan()).ToArray();
            if (viablePlans.Length <= 0)
            {
                throw new CommandException(
                    "There are no viable release plans in any channels using the provided arguments. The following release plans were considered:" +
                    System.Environment.NewLine +
                    $"{releasePlans.Select(p => p.FormatAsTable()).NewlineSeperate()}");
            }

            if (viablePlans.Length == 1)
            {
                var selectedPlan = viablePlans.Single();
                commandOutputProvider.Information("Selected the release plan for Channel '{Channel:l}' - it is a perfect match", selectedPlan.Channel.Name);
                return selectedPlan;
            }

            if (viablePlans.Length > 1 && viablePlans.Any(p => p.Channel.IsDefault))
            {
                var selectedPlan = viablePlans.First(p => p.Channel.IsDefault);
                commandOutputProvider.Information("Selected the release plan for Channel '{Channel:l}' - there were multiple matching Channels ({AllChannels:l}) so we selected the default channel.", selectedPlan.Channel.Name, viablePlans.Select(p => p.Channel.Name).CommaSeperate());
                return selectedPlan;
            }

            throw new CommandException(
                $"There are {viablePlans.Length} viable release plans using the provided arguments so we cannot auto-select one. The viable release plans are:" +
                System.Environment.NewLine +
                $"{viablePlans.Select(p => p.FormatAsTable()).NewlineSeperate()}" +
                System.Environment.NewLine +
                "The unviable release plans are:" +
                System.Environment.NewLine +
                $"{releasePlans.Except(viablePlans).Select(p => p.FormatAsTable()).NewlineSeperate()}");
        }

        void ReadReleaseNotesFromFile(string value)
        {
            try
            {
                ReleaseNotes = File.ReadAllText(value);
            }
            catch (IOException ex)
            {
                throw new CommandException(ex.Message);
            }
        }

        public void PrintDefaultOutput()
        {

        }

        public void PrintJsonOutput()
        {
            commandOutputProvider.Json(new
            {
                ReleaseId = release.Id,
                release.Assembled,
                release.Version,
                Project = new { project.Id, project.Name },
                Channel = plan.Channel == null ? null : new { plan.Channel.Id, plan.Channel.Name },
                Steps = plan.PackageSteps.Select((x, i) => new
                {
                    Id = i,
                    x.ActionName,
                    x.Version,
                    x.VersionSource,
                    VersionRule = x.ChannelVersionRuleTestResult?.ToSummaryString()
                })
            });
        }
    }
}
