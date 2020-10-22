using System;
using System.Collections.Generic;
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
        private static OverwriteMode DefaultOverwriteMode = OverwriteMode.FailIfExists;
        private OctopusPackageVersionBuildInformationMappedResource resultResource;
        private readonly List<OctopusPackageVersionBuildInformationMappedResource> pushedBuildInformation;

        public BuildInformationCommand(IOctopusAsyncRepositoryFactory repositoryFactory, IOctopusFileSystem fileSystem, IOctopusClientFactory clientFactory, ICommandOutputProvider commandOutputProvider)
            : base(clientFactory, repositoryFactory, fileSystem, commandOutputProvider)
        {
            var options = Options.For("Build information pushing");
            options.Add<string>("package-id=", "The ID of the package. Specify multiple packages by specifying this argument multiple times: \n--package-id 'MyCompany.MyApp' --package-id 'MyCompany.MyApp2'.", packageId => PackageIds.Add(packageId), allowsMultiple: true);
            options.Add<string>("version=", "The version of the package; defaults to a timestamp-based version.", v => Version = v);
            options.Add<string>("file=", "Octopus Build Information Json file.", file => File = file);
            options.Add<OverwriteMode>("overwrite-mode=", $"Determines behavior if the package already exists in the repository. Valid values are {Enum.GetNames(typeof(OverwriteMode)).ReadableJoin()}. Default is {DefaultOverwriteMode}.", mode => OverwriteMode = mode);

            pushedBuildInformation = new List<OctopusPackageVersionBuildInformationMappedResource>();
        }

        public HashSet<string> PackageIds { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public string Version { get; set; }
        public string File { get; set; }

        public OverwriteMode OverwriteMode { get; set; } = DefaultOverwriteMode;

        public async Task Request()
        {
            if (string.IsNullOrEmpty(File))
                throw new CommandException("Please specify the build information file.");
            if (PackageIds.None())
                throw new CommandException("Please specify at least one package id.");
            if (string.IsNullOrEmpty(Version))
                throw new CommandException("Please specify the package version.");

            if (!FileSystem.FileExists(File))
                throw new CommandException($"Build information file '{File}' does not exist");

            var fileContent = FileSystem.ReadAllText(File);

            var rootDocument = await Repository.LoadRootDocument();
            if (rootDocument.HasLink("BuildInformation"))
            {

                var buildInformation = JsonConvert.DeserializeObject<OctopusBuildInformation>(fileContent);

                foreach (var packageId in PackageIds)
                {
                    commandOutputProvider.Debug("Pushing build information for package {PackageId} version {Version}...", packageId, Version);
                    resultResource = await Repository.BuildInformationRepository.Push(packageId, Version, buildInformation, OverwriteMode);
                    pushedBuildInformation.Add(resultResource);
                }
            }
            else
            {
                commandOutputProvider.Warning("Detected Octopus server version doesn't support the Build Information API.");

                var metadata = JsonConvert.DeserializeObject<OctopusPackageMetadata>(fileContent);
                // old server won't parse without the CommentParser being set, default it to Jira
                metadata.CommentParser = "Jira";

                foreach (var packageId in PackageIds)
                {
                    commandOutputProvider.Debug("Pushing build information as legacy package metadata for package {PackageId} version {Version}...", packageId, Version);
                    var result = await Repository.PackageMetadataRepository.Push(packageId, Version, metadata, OverwriteMode);
                }
            }
        }

        public void PrintDefaultOutput()
        {
            commandOutputProvider.Debug("Push successful");
        }

        public void PrintJsonOutput()
        {
            commandOutputProvider.Json(pushedBuildInformation);
        }
    }
}
