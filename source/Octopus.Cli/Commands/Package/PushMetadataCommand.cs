using System;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Octopus.Cli.Infrastructure;
using Octopus.Cli.Repositories;
using Octopus.Cli.Util;
using Octopus.Client;
using Octopus.Client.Model;
using Octopus.Client.Model.PackageMetadata;

namespace Octopus.Cli.Commands.Package
{
    [Command("push-metadata", Description = "Pushes package metadata to Octopus Server.")]
    public class PushMetadataCommand : ApiCommand, ISupportFormattedOutput
    {
        private OctopusPackageMetadataMappedResource resultResource;

        public PushMetadataCommand(IOctopusAsyncRepositoryFactory repositoryFactory, IOctopusFileSystem fileSystem, IOctopusClientFactory clientFactory, ICommandOutputProvider commandOutputProvider)
            : base(clientFactory, repositoryFactory, fileSystem, commandOutputProvider)
        {
            var options = Options.For("Package metadata pushing");
            options.Add("package-id=", "The ID of the package; e.g. MyCompany.MyApp", v => PackageId = v);
            options.Add("version=", "The version of the package; defaults to a timestamp-based version", v => Version = v);
            options.Add("metadata-file=", "Octopus Package metadata Json file.", file => MetadataFile = file);
            options.Add("overwrite-mode=", "If the package metadata already exists in the repository, the default behavior is to reject the new package metadata being pushed (FailIfExists). You can use the overwrite mode to OverwriteExisting or IgnoreIfExists.", mode => OverwriteMode = (OverwriteMode)Enum.Parse(typeof(OverwriteMode), mode, true));
            options.Add("replace-existing", "If the package metadata already exists in the repository, the default behavior is to reject the new package metadata being pushed. You can pass this flag to overwrite the existing package metadata. This flag may be deprecated in a future version; passing it is the same as using the OverwriteExisting overwrite-mode.", replace => OverwriteMode = OverwriteMode.OverwriteExisting);
        }

        public string PackageId { get; set; }
        public string Version { get; set; }
        public string MetadataFile { get; set; }
        public OverwriteMode OverwriteMode { get; set; }

        public async Task Request()
        {
            if (!FileSystem.FileExists(MetadataFile))
                throw new CommandException("Metadata file does not exist");

            commandOutputProvider.Debug("Pushing package metadata: {PackageId}...", PackageId);

            var fileContent = FileSystem.ReadAllText(MetadataFile);
            var octopusPackageMetadata = JsonConvert.DeserializeObject<OctopusPackageMetadata>(fileContent);

            resultResource = await Repository.PackageMetadataRepository.Push(PackageId, Version, octopusPackageMetadata, OverwriteMode);
        }

        public void PrintDefaultOutput()
        {
            commandOutputProvider.Debug("Push successful");
        }

        public void PrintJsonOutput()
        {
            commandOutputProvider.Json(resultResource);
        }
    }
}
