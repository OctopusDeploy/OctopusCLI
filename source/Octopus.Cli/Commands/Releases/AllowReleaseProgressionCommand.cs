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
    [Command("allow-releaseprogression", Description = "Allow a release from progressing to next phase.")]
    public class AllowReleaseProgressionCommand : ApiCommand, ISupportFormattedOutput
    {
        ProjectResource project;
        ReleaseResource release;

        public AllowReleaseProgressionCommand(IOctopusClientFactory clientFactory, IOctopusAsyncRepositoryFactory repositoryFactory, IOctopusFileSystem fileSystem, ICommandOutputProvider commandOutputProvider)
            : base(clientFactory, repositoryFactory, fileSystem, commandOutputProvider)
        {
            var options = Options.For("Allowing release progression.");
            options.Add("project=", "Name or ID of the project", v => ProjectNameOrId = v);
            options.Add("version=|releaseNumber=", "Release version/number",
                v => ReleaseVersionNumber = v);
        }

        public string ProjectNameOrId { get; set; }

        public string ReleaseVersionNumber { get; set; }

        protected override async Task ValidateParameters()
        {
            if (string.IsNullOrWhiteSpace(ProjectNameOrId)) throw new CommandException("Please specify a project name or ID using the parameter: --project=XYZ");
            if (string.IsNullOrWhiteSpace(ReleaseVersionNumber)) throw new CommandException("Please specify a release version number using the version parameter: --version=1.0.5");
            if (!SemanticVersion.TryParse(ReleaseVersionNumber, out _)) throw new CommandException("Please provide a valid release version format, you can refer to https://semver.org/ for a valid format: --version=1.0.5");

            await base.ValidateParameters().ConfigureAwait(false);
        }

        public async Task Request()
        {
            project = await Repository.Projects.FindByNameOrIdOrFail(ProjectNameOrId).ConfigureAwait(false);

            release = await Repository.Projects.GetReleaseByVersion(project, ReleaseVersionNumber).ConfigureAwait(false);
            if (release == null) throw new OctopusResourceNotFoundException($"Unable to locate a release with version/release number '{ReleaseVersionNumber}'.");

            var isReleaseAllowedFromProgressionAlready = (await Repository.Defects.GetDefects(release).ConfigureAwait(false)).Items.All(i => i.Status == DefectStatus.Resolved);
            if (isReleaseAllowedFromProgressionAlready)
            {
                commandOutputProvider.Information($"Release with version/release number '{ReleaseVersionNumber}' is already allowed to progress to next phase.");

                return;
            }

            await Repository.Defects.ResolveDefect(release).ConfigureAwait(false);
        }

        public void PrintDefaultOutput()
        {
            commandOutputProvider.Information("Allowed successfully.");
        }

        public void PrintJsonOutput()
        {
            commandOutputProvider.Json(new
            {
                project.SpaceId,
                Project = new { project.Id, project.Name },
                Release = new { release.Id, release.Version, IsPreventedFromProgressing = false }
            });
        }
    }
}
