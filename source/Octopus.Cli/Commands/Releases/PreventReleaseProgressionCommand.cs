﻿using System.Threading.Tasks;
using Octopus.Cli.Infrastructure;
using Octopus.Cli.Repositories;
using Octopus.Cli.Util;
using Octopus.Client;
using Octopus.Client.Model;

namespace Octopus.Cli.Commands.Releases
{
    [Command(CommandName, Description = "Prevent a release from progressing to next phase.")]
    public class PreventReleaseProgressionCommand : ApiCommand, ISupportFormattedOutput
    {
        public const string CommandName = "prevent-release-progression";

        ProjectResource project;
        ReleaseResource release;

        public PreventReleaseProgressionCommand(IOctopusClientFactory clientFactory, IOctopusAsyncRepositoryFactory repositoryFactory, IOctopusFileSystem fileSystem, ICommandOutputProvider commandOutputProvider)
            : base(clientFactory, repositoryFactory, fileSystem, commandOutputProvider)
        {
            var options = Options.For("Preventing release progression.");
            options.Add("project=", "Name or ID of the project", v => ProjectNameOrId = v);
            options.Add("version=|releaseNumber=", "Release number to use for auto deployments.",
                v => ReleaseVersionNumber = v);
            options.Add("reason=", "Reason to prevent this release from progressing to next phase.",
                v => ReasonToPrevent = v);
        }

        public string ProjectNameOrId { get; set; }

        public string ReleaseVersionNumber { get; set; }

        public string ReasonToPrevent { get; set; }

        protected override async Task ValidateParameters()
        {
            if (string.IsNullOrWhiteSpace(ProjectNameOrId)) throw new CommandException("Please specify a project name or ID using the parameter: --project=XYZ");
            if (string.IsNullOrWhiteSpace(ReleaseVersionNumber)) throw new CommandException("Please specify a release version");
            if (string.IsNullOrWhiteSpace(ReasonToPrevent)) throw new CommandException("Please specify a reason why you would like to prevent this release from progressing to next phase");

            await base.ValidateParameters().ConfigureAwait(false);
        }

        public async Task Request()
        {
            project = await Repository.Projects.FindByNameOrIdOrFail(ProjectNameOrId).ConfigureAwait(false);

            release = await Repository.Projects.GetReleaseByVersion(project, ReleaseVersionNumber).ConfigureAwait(false);

            await Repository.Defects.RaiseDefect(release, ReasonToPrevent).ConfigureAwait(false);
        }

        public void PrintDefaultOutput()
        {
            commandOutputProvider.Information("Prevented Successfully.");
        }

        public void PrintJsonOutput()
        {
            commandOutputProvider.Json(new
            {
                project.SpaceId,
                Project = new { project.Id, project.Name },
                Release = new { release.Id, release.Version },
                ReasonToPrevent
            });
        }
    }
}
