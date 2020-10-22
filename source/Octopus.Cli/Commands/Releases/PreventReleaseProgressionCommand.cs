using System.Linq;
using System.Threading.Tasks;
using Octopus.Cli.Infrastructure;
using Octopus.Cli.Repositories;
using Octopus.Cli.Util;
using Octopus.Client;
using Octopus.Client.Exceptions;
using Octopus.Client.Model;

namespace Octopus.Cli.Commands.Releases
{
    [Command("prevent-releaseprogression", Description = "Prevents a release from progressing to the next phase.")]
    public class PreventReleaseProgressionCommand : ApiCommand, ISupportFormattedOutput
    {
        ProjectResource project;
        ReleaseResource release;

        public PreventReleaseProgressionCommand(IOctopusClientFactory clientFactory, IOctopusAsyncRepositoryFactory repositoryFactory, IOctopusFileSystem fileSystem, ICommandOutputProvider commandOutputProvider)
            : base(clientFactory, repositoryFactory, fileSystem, commandOutputProvider)
        {
            var options = Options.For("Preventing release progression.");
            options.Add<string>("project=", "Name or ID of the project.", v => ProjectNameOrId = v);
            options.Add<string>("version=|releaseNumber=", "Release version/number.", v => ReleaseVersionNumber = v);
            options.Add<string>("reason=", "Reason to prevent this release from progressing to next phase.", v => ReasonToPrevent = v);
        }

        public string ProjectNameOrId { get; set; }

        public string ReleaseVersionNumber { get; set; }

        public string ReasonToPrevent { get; set; }

        protected override async Task ValidateParameters()
        {
            if (string.IsNullOrWhiteSpace(ProjectNameOrId)) throw new CommandException("Please specify a project name or ID using the parameter: --project=XYZ");
            if (string.IsNullOrWhiteSpace(ReleaseVersionNumber)) throw new CommandException("Please specify a release version number using the version parameter: --version=1.0.5");
            if (!SemanticVersion.TryParse(ReleaseVersionNumber, out _)) throw new CommandException("Please provide a valid release version format, you can refer to https://semver.org/ for a valid format: --version=1.0.5");
            if (string.IsNullOrWhiteSpace(ReasonToPrevent)) throw new CommandException("Please specify a reason why you would like to prevent this release from progressing to next phase using the reason parameter: --reason=Contract Tests Failed");

            await base.ValidateParameters().ConfigureAwait(false);
        }

        public async Task Request()
        {
            project = await Repository.Projects.FindByNameOrIdOrFail(ProjectNameOrId).ConfigureAwait(false);

            release = await Repository.Projects.GetReleaseByVersion(project, ReleaseVersionNumber).ConfigureAwait(false);
            if (release == null) throw new OctopusResourceNotFoundException($"Unable to locate a release with version/release number '{ReleaseVersionNumber}'.");

            var isReleasePreventedFromProgressionAlready = (await Repository.Defects.GetDefects(release).ConfigureAwait(false)).Items.Any(i => i.Status == DefectStatus.Unresolved);
            if (isReleasePreventedFromProgressionAlready)
            {
                commandOutputProvider.Debug($"Release with version/release number '{ReleaseVersionNumber}' is already prevented from progressing to next phase.");

                return;
            }

            await Repository.Defects.RaiseDefect(release, ReasonToPrevent).ConfigureAwait(false);
        }

        public void PrintDefaultOutput()
        {
            commandOutputProvider.Information("Prevented successfully.");
        }

        public void PrintJsonOutput()
        {
            commandOutputProvider.Json(new
            {
                project.SpaceId,
                Project = new { project.Id, project.Name },
                Release = new { release.Id, release.Version, IsPreventedFromProgressing = true },
                ReasonToPrevent
            });
        }
    }
}
