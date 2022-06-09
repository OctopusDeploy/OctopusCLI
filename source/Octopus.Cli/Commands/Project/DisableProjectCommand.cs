using System;
using System.Threading.Tasks;
using Octopus.Cli.Repositories;
using Octopus.Cli.Util;
using Octopus.Client;
using Octopus.Client.Model;
using Octopus.CommandLine;
using Octopus.CommandLine.Commands;

namespace Octopus.Cli.Commands.Project
{
    [Command("disable-project", Description = "Disables a project.")]
    public class DisableProjectCommand : ApiCommand, ISupportFormattedOutput
    {
        ProjectResource project;

        public DisableProjectCommand(IOctopusAsyncRepositoryFactory repositoryFactory, IOctopusFileSystem fileSystem, IOctopusClientFactory clientFactory, ICommandOutputProvider commandOutputProvider)
            : base(clientFactory, repositoryFactory, fileSystem, commandOutputProvider)
        {
            var options = Options.For("Project disablement");
            options.Add<string>("name=", "The name of the project.", v => ProjectName = v);
        }

        public string ProjectName { get; set; }

        public async Task Request()
        {
            if (string.IsNullOrWhiteSpace(ProjectName)) throw new CommandException("Please specify a project name using the parameter: --name=XYZ");

            commandOutputProvider.Information("Finding project: {Project:l}", ProjectName);

            // Get project
            project = await Repository.Projects.FindByName(ProjectName).ConfigureAwait(false);

            // Check that is project exists and isn't already disabled
            if (project == null)
            {
                throw new CouldNotFindException("project");
            }
            else if (project.IsDisabled == true)
            {
                commandOutputProvider.Information("The project {Project:l} is already disabled.", project.Name);
                return;

                throw new CommandException($"The project {project.Name} is already disabled.");
            }

            // Disable project
            commandOutputProvider.Information("Disabling project: {Project:l}", ProjectName);
            try
            {
                project.IsDisabled = true;
                await Repository.Projects.Modify(project).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                commandOutputProvider.Error("Error disabling project {Project:l}: {Exception:l}", project.Name, ex.Message);
            }

        }

        public void PrintDefaultOutput()
        {
            commandOutputProvider.Information("Project {Project:l} disabled. ID: {Id:l}", project.Name, project.Id);
        }

        public void PrintJsonOutput()
        {
            commandOutputProvider.Json(new
            {
                Project = new
                {
                    project.Id,
                    project.Name,
                    project.IsDisabled
                }
            });
        }
    }
}