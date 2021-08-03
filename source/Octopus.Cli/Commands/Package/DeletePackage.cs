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
        static readonly OctopusVersionParser OctopusVersionParser = new OctopusVersionParser();
        public DeletePackageCommand(IOctopusAsyncRepositoryFactory repositoryFactory, IOctopusFileSystem fileSystem, IOctopusClientFactory clientFactory, ICommandOutputProvider commandOutputProvider)
            : base(clientFactory, repositoryFactory, fileSystem, commandOutputProvider)
        {
            var options = Options.For("Deletion");
            options.Add<string>("package=", "Name of the package.", v => PackageName = v);
        }
        public string PackageName { get; set; }
        public async Task Request()
        {
            if (string.IsNullOrWhiteSpace(PackageName)) throw new CommandException("Please specify a package name using the parameter: --package=XYZ");

            var package = await Repository.BuiltInPackageRepository.GetPackage(PackageName, null).ConfigureAwait(false);
            await Repository.BuiltInPackageRepository.DeletePackage(package).ConfigureAwait(false);
            
            commandOutputProvider.Information("Deleting package");
        }
        public void PrintDefaultOutput()
        {
        }
        public void PrintJsonOutput()
        {
        }
    }
}