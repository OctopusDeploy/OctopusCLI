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
    [Command("delete-project", Description = "Deletes a project.")]
    public class DeleteProjectCommand : ApiCommand, ISupportFormattedOutput
    {
        ProjectResource project;
        bool ProjectDeleted = true;

        public DeleteProjectCommand(IOctopusAsyncRepositoryFactory repositoryFactory, IOctopusFileSystem fileSystem, IOctopusClientFactory clientFactory, ICommandOutputProvider commandOutputProvider)
            : base(clientFactory, repositoryFactory, fileSystem, commandOutputProvider)
        {
            var options = Options.For("Project deletion");
            options.Add<string>("name=", "The name of the project.", v => ProjectName = v);
        }

        public string ProjectName { get; set; }
        public string ProjectGroupName { get; set; }
        public bool ProjectExists { get; set; }

        public async Task Request()
        {
            if (string.IsNullOrWhiteSpace(ProjectName)) throw new CommandException("Please specify a project name using the parameter: --name=XYZ");

            commandOutputProvider.Information("Finding project: {Project:l}", ProjectName);

            project = await Repository.Projects.FindByName(ProjectName).ConfigureAwait(false);
            if (project == null)
            {
                commandOutputProvider.Information("The project {Project:l} does not exist", project.Name);
                return;

                throw new CommandException($"The project {project.Name} does not exist.");
            }

            commandOutputProvider.Information("Deleting project: {Project:l}", ProjectName);

            try
            {
                await Repository.Projects.Delete(project).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                ProjectDeleted = false;
                commandOutputProvider.Error("Error deleting project {Project:l}: {Exception:l}", project.Name, ex.Message);
            }

        }

        public void PrintDefaultOutput()
        {
            commandOutputProvider.Information("Project deleted. ID: {Id:l}", project.Id);
        }

        public void PrintJsonOutput()
        {
            commandOutputProvider.Json(new
            {
                Project = new
                {
                    project.Id,
                    project.Name,
                    ProjectDeleted
                }
            });
        }
    }
}