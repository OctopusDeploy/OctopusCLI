using System;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Octopus.Cli.Infrastructure;
using Octopus.Cli.Repositories;
using Octopus.Cli.Util;
using Octopus.Client;
using Octopus.Client.Model;
using Octopus.Client.Model.BuildInformation;
using Octopus.Client.Model.PackageMetadata;


namespace Octopus.Cli.Commands.Package
{
    [Command("build-information", Description = "Pushes build information to Octopus Server.")]
    public class BuildInformationCommand : ApiCommand, ISupportFormattedOutput
    {
        private OctopusPackageVersionBuildInformationMappedResource resultResource;

        public BuildInformationCommand(IOctopusAsyncRepositoryFactory repositoryFactory, IOctopusFileSystem fileSystem, IOctopusClientFactory clientFactory, ICommandOutputProvider commandOutputProvider)
            : base(clientFactory, repositoryFactory, fileSystem, commandOutputProvider)
        {
            var options = Options.For("Build information pushing");
            options.Add("package-id=", "The ID of the package, e.g., 'MyCompany.MyApp'.", v => PackageId = v);
            options.Add("version=", "The version of the package; defaults to a timestamp-based version", v => Version = v);
            options.Add("file=", "Octopus Build Information Json file.", file => File = file);
            options.Add("overwrite-mode=", "If the build information already exists in the repository, the default behavior is to reject the new build information being pushed (FailIfExists). You can use the overwrite mode to OverwriteExisting or IgnoreIfExists.", mode => OverwriteMode = (OverwriteMode)Enum.Parse(typeof(OverwriteMode), mode, true));
        }

        public string PackageId { get; set; }
        public string Version { get; set; }
        public string File { get; set; }
        public OverwriteMode OverwriteMode { get; set; }

        public async Task Request()
        {
            if (string.IsNullOrEmpty(File))
                throw new CommandException("Please specify the build information file.");
            if (string.IsNullOrEmpty(PackageId))
                throw new CommandException("Please specify the package id.");
            if (string.IsNullOrEmpty(Version))
                throw new CommandException("Please specify the package version.");

            if (!FileSystem.FileExists(File))
                throw new CommandException($"Build information file '{File}' does not exist");

            var fileContent = FileSystem.ReadAllText(File);

            var rootDocument = await Repository.LoadRootDocument();
            if (rootDocument.HasLink("BuildInformation"))
            {
                commandOutputProvider.Debug("Pushing build information: {PackageId}...", PackageId);

                var buildInformation = JsonConvert.DeserializeObject<OctopusBuildInformation>(fileContent);

                resultResource = await Repository.BuildInformationRepository.Push(PackageId, Version, buildInformation, OverwriteMode);
            }
            else
            {
                commandOutputProvider.Warning("Detected Octopus server version doesn't support the Build Information API, pushing build information as legacy package metadata: {PackageId}...", PackageId);

                var metadata = JsonConvert.DeserializeObject<OctopusPackageMetadata>(fileContent);
                // old server won't parse without the CommentParser being set, default it to Jira
                metadata.CommentParser = "Jira";

                var result = await Repository.PackageMetadataRepository.Push(PackageId, Version, metadata, OverwriteMode);
            }
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
