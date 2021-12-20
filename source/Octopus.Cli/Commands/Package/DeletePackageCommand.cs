using System;
using System.Threading.Tasks;
using Octopus.Cli.Repositories;
using Octopus.Cli.Util;
using Octopus.Client;
using Octopus.CommandLine;
using Octopus.CommandLine.Commands;

namespace Octopus.Cli.Commands.Package
{
    [Command("delete-package", Description = "Deletes a package from the built-in NuGet repository in an Octopus Server.")]
    public class DeletePackageCommand : ApiCommand, ISupportFormattedOutput
    {
        object result;

        public DeletePackageCommand(IOctopusAsyncRepositoryFactory repositoryFactory,IOctopusFileSystem fileSystem, IOctopusClientFactory clientFactory, ICommandOutputProvider commandOutputProvider)
            : base(clientFactory, repositoryFactory, fileSystem, commandOutputProvider)
        {
            var options = Options.For("Deletion");
            options.Add<string>("packageId=", "Id of the package.", v => PackageId = v);
            options.Add<string>("version=", "Version number of the package.", v => PackageVersion = v);
        }

        public string PackageId { get; set; }
        public string PackageVersion { get; set; }

        public async Task Request()
        {
            if (string.IsNullOrWhiteSpace(PackageId)) throw new CommandException("Please specify a package id using the parameter: --packageId=XYZ");
            if (string.IsNullOrWhiteSpace(PackageVersion)) throw new CommandException("Please specify a package version using the parameter: --version=1.0.0");

            commandOutputProvider.Debug("Finding package: {PackageId:l} with version {Version:l}", PackageId, PackageVersion);
            
            // If package == null client throws 404 caught in CliProgram
            var package = await Repository.BuiltInPackageRepository.GetPackage(PackageId, PackageVersion).ConfigureAwait(false);

            commandOutputProvider.Debug("Found package with id: {PackageId:l}... Deleting package", package.PackageId);
            await Repository.BuiltInPackageRepository.DeletePackage(package).ConfigureAwait(false);
            result = new
            {
                Status = "Success",
                Package = new {PackageId, Version = PackageVersion}
            };
        }

        public void PrintDefaultOutput()
        {
            commandOutputProvider.Information("Package deleted");
        }

        public void PrintJsonOutput()
        {
            commandOutputProvider.Json(result);
        }
    }
}
