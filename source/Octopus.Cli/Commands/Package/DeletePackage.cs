using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Octopus.Cli.Infrastructure;
using Octopus.Cli.Repositories;
using Octopus.Cli.Util;
using Octopus.Client;
using Octopus.Client.Model;
using Octopus.CommandLine;
using Octopus.CommandLine.Commands;
using Octopus.Versioning.Octopus;
namespace Octopus.Cli.Commands.Releases
{
    [Command("delete-package", Description = "Deletes a package from the built-in NuGet repository in an Octopus Server.")]
    public class DeletePackageCommand : ApiCommand, ISupportFormattedOutput
    {
        public DeletePackageCommand(IOctopusAsyncRepositoryFactory repositoryFactory, IOctopusFileSystem fileSystem, IOctopusClientFactory clientFactory, ICommandOutputProvider commandOutputProvider)
            : base(clientFactory, repositoryFactory, fileSystem, commandOutputProvider)
        {
            var options = Options.For("Deletion");
            options.Add<string>("package=", "Id of the package.", v => PackageId = v);
        }
        public string PackageId { get; set; }
        public async Task Request()
        {
            if (string.IsNullOrWhiteSpace(PackageId)) throw new CommandException("Please specify a package name using the parameter: --package=XYZ");

            var package = await Repository.BuiltInPackageRepository.GetPackage(PackageId, null).ConfigureAwait(false);
            if(package != null)
            {
                await Repository.BuiltInPackageRepository.DeletePackage(package).ConfigureAwait(false);
                commandOutputProvider.Information("Deleting package");
            }
            else
            {
                commandOutputProvider.Error("There is no available package to be deleted");
            }
            
        }
        public void PrintDefaultOutput()
        {
        }
        public void PrintJsonOutput()
        {
        }
    }
}