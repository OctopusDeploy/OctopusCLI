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
    [Command("push-metadata", Description = "Pushes package metadata to Octopus Server.  Deprecated. Please use the build-information command for Octopus Server 2019.10.0 and above.")]
    public class PushMetadataCommand : ApiCommand, ISupportFormattedOutput
    {
        private static OverwriteMode DefaultOverwriteMode = OverwriteMode.FailIfExists;

        private OctopusPackageMetadataMappedResource resultResource;

        public PushMetadataCommand(IOctopusAsyncRepositoryFactory repositoryFactory, IOctopusFileSystem fileSystem, IOctopusClientFactory clientFactory, ICommandOutputProvider commandOutputProvider)
            : base(clientFactory, repositoryFactory, fileSystem, commandOutputProvider)
        {
            var options = Options.For("Package metadata pushing");
            options.Add<string>("package-id=", "The ID of the package, e.g., 'MyCompany.MyApp'.", v => PackageId = v);
            options.Add<string>("version=", "The version of the package; defaults to a timestamp-based version", v => Version = v);
            options.Add<string>("metadata-file=", "Octopus Package metadata Json file.", file => MetadataFile = file);
            options.Add<OverwriteMode>("overwrite-mode=", $"Determines behavior if the package already exists in the repository. Valid values are {Enum.GetNames(typeof(OverwriteMode)).ReadableJoin()}. Default is {DefaultOverwriteMode}.", mode => OverwriteMode = mode);
            options.Add<string>("replace-existing", "If the package metadata already exists in the repository, the default behavior is to reject the new package metadata being pushed. You can pass this flag to overwrite the existing package metadata. This flag may be deprecated in a future version; passing it is the same as using the OverwriteExisting overwrite-mode.", replace => OverwriteMode = OverwriteMode.OverwriteExisting);
        }

        public string PackageId { get; set; }
        public string Version { get; set; }
        public string MetadataFile { get; set; }
        public OverwriteMode OverwriteMode { get; set; } = DefaultOverwriteMode;

        public async Task Request()
        {
            if (string.IsNullOrEmpty(MetadataFile))
                throw new CommandException("Please specify the metadata file.");
            if (string.IsNullOrEmpty(PackageId))
                throw new CommandException("Please specify the package id.");
            if (string.IsNullOrEmpty(Version))
                throw new CommandException("Please specify the package version.");

            if (!FileSystem.FileExists(MetadataFile))
                throw new CommandException($"Metadata file '{MetadataFile}' does not exist");

            var rootDocument = await Repository.LoadRootDocument();
            if (rootDocument.HasLink("BuildInformation"))
                commandOutputProvider.Warning("This Octopus server supports the BuildInformation API, we recommend using the `build-information` command as `package-metadata` has been deprecated.");

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
