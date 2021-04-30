using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Packaging;
using NuGet.Versioning;
using Octopus.Cli.Diagnostics;
using Octopus.Cli.Infrastructure;
using Octopus.Cli.Model;
using Octopus.Cli.Util;
using Octopus.CommandLine;
using Octopus.CommandLine.Commands;

namespace Octopus.Cli.Commands.Package
{
    [Command("pack", Description = "Creates a package (.nupkg or .zip) from files on disk, without needing a .nuspec or .csproj.")]
    public class PackCommand : CommandBase, ISupportFormattedOutput
    {
        const PackageCompressionLevel DefaultPackageCompressionLevel = PackageCompressionLevel.Optimal;
        const PackageFormat DefaultPackageFormat = PackageFormat.NuPkg;
        const PackageFormat RecommendedPackageFormat = PackageFormat.Zip;
#pragma warning disable 618 //ignore obsolete member
        readonly string supportedPackageFormats = Enum.GetNames(typeof(PackageFormat)).Except(new[] { PackageFormat.Nuget.ToString() }).ReadableJoin();
#pragma warning restore 618

        readonly IList<string> authors = new List<string>();
        readonly IOctopusFileSystem fileSystem;
        readonly IList<string> includes = new List<string>();
        string basePath;
        string description;
        string id;
        string outFolder;
        bool overwrite;
        bool verbose;
        string releaseNotes, releaseNotesFile;
        string title;
        Client.Model.SemanticVersion version;
        IPackageBuilder packageBuilder;
        string allReleaseNotes;
        PackageCompressionLevel packageCompressionLevel = DefaultPackageCompressionLevel;

        public PackCommand(IOctopusFileSystem fileSystem, ICommandOutputProvider commandOutputProvider) : base(commandOutputProvider)
        {
            this.fileSystem = fileSystem;

            var common = Options.For("Advanced options");
            common.Add<string>("include=", "[Optional, Multiple] Add a file pattern to include, relative to the base path e.g. /bin/*.dll - if none are specified, defaults to **.", v => includes.Add(v), allowsMultiple: true);
            common.Add<bool>("overwrite", "[Optional] Allow an existing package file of the same ID/version to be overwritten.", v => overwrite = true);

            var zip = Options.For("Zip packages");
            zip.Add<PackageCompressionLevel>("compressionLevel=", $"[Optional] Sets the compression level of the package. Valid values are {Enum.GetNames(typeof(PackageCompressionLevel)).ReadableJoin()}. Default is {DefaultPackageCompressionLevel}.", c => packageCompressionLevel = c);

            var nuget = Options.For("NuGet packages");
            nuget.Add<string>("author=", "[Optional, Multiple] Add an author to the package metadata; defaults to the current user.", v => authors.Add(v), allowsMultiple: true);
            nuget.Add<string>("title=", "[Optional] The title of the package.", v => title = v);
            nuget.Add<string>("description=", "[Optional] A description of the package; defaults to a generic description.", v => description = v);
            nuget.Add<string>("releaseNotes=", "[Optional] Release notes for this version of the package.", v => releaseNotes = v);
            nuget.Add<string>("releaseNotesFile=", "[Optional] A file containing release notes for this version of the package.", v => releaseNotesFile = v);

            var basic = Options.For("Basic options");
            basic.Add<string>("id=", "The ID of the package; e.g. MyCompany.MyApp.", v => id = v);
            basic.Add<PackageFormat>("format=", $"Package format. Valid values are {supportedPackageFormats}. Default is {DefaultPackageFormat}, though we recommend {RecommendedPackageFormat} going forward.", fmt => packageBuilder = SelectFormat(fmt));
            basic.Add<string>("version=", "[Optional] The version of the package; must be a valid SemVer; defaults to a timestamp-based version.", v => version = string.IsNullOrWhiteSpace(v) ? null : new Client.Model.SemanticVersion(v));
            basic.Add<string>("outFolder=",
                "[Optional] The folder into which the generated NuPkg file will be written; defaults to '.'.",
                v =>
                {
                    v.CheckForIllegalPathCharacters(nameof(outFolder));
                    outFolder = v;
                });
            basic.Add<string>("basePath=",
                "[Optional] The root folder containing files and folders to pack; defaults to '.'.",
                v =>
                {
                    v.CheckForIllegalPathCharacters(nameof(basePath));
                    basePath = v;
                });
            basic.Add<bool>("verbose", "[Optional] verbose output.", v => verbose = true);
            basic.AddLogLevelOptions();

            packageBuilder = SelectFormat(DefaultPackageFormat);
        }

        public override Task Execute(string[] commandLineArguments)
        {
            return Task.Run(() =>
            {
                Options.Parse(commandLineArguments);

                if (printHelp)
                {
                    GetHelp(Console.Out, commandLineArguments);
                    return;
                }

                commandOutputProvider.PrintMessages = OutputFormat == OutputFormat.Default || verbose;

                if (string.IsNullOrWhiteSpace(id))
                    throw new CommandException("An ID is required");

                if (includes.All(string.IsNullOrWhiteSpace))
                    includes.Add("**");

                if (string.IsNullOrWhiteSpace(basePath))
                    basePath = Path.GetFullPath(Directory.GetCurrentDirectory());

                if (string.IsNullOrWhiteSpace(outFolder))
                    outFolder = Path.GetFullPath(Directory.GetCurrentDirectory());

                if (version == null)
                {
                    var now = DateTime.Now;
                    version = Client.Model.SemanticVersion.Parse($"{now.Year}.{now.Month}.{now.Day}.{now.Hour * 10000 + now.Minute * 100 + now.Second}");
                }

                if (authors.All(string.IsNullOrWhiteSpace))
                    authors.Add(System.Environment.GetEnvironmentVariable("USERNAME") + "@" + System.Environment.GetEnvironmentVariable("USERDOMAIN"));

                if (string.IsNullOrWhiteSpace(description))
                    description = "A deployment package created from files on disk.";

                allReleaseNotes = null;
                if (!string.IsNullOrWhiteSpace(releaseNotesFile))
                {
                    if (!File.Exists(releaseNotesFile))
                        commandOutputProvider.Warning("The release notes file '{Path:l}' could not be found", releaseNotesFile);
                    else
                        allReleaseNotes = fileSystem.ReadFile(releaseNotesFile);
                }

                if (!string.IsNullOrWhiteSpace(releaseNotes))
                {
                    if (allReleaseNotes != null)
                        allReleaseNotes += System.Environment.NewLine + releaseNotes;
                    else
                        allReleaseNotes = releaseNotes;
                }

                if (string.IsNullOrWhiteSpace(version.OriginalString))
                    throw new Exception("Somehow we created a SemanticVersion without the OriginalString value being preserved. We want to use the OriginalString so we can preserve the version as intended by the caller.");

                var metadata = new ManifestMetadata
                {
                    Id = id,
                    Authors = authors,
                    Description = description,
                    Version = NuGetVersion.Parse(version.OriginalString)
                };

                if (!string.IsNullOrWhiteSpace(allReleaseNotes))
                    metadata.ReleaseNotes = allReleaseNotes;

                if (!string.IsNullOrWhiteSpace(title))
                    metadata.Title = title;

                packageBuilder.SetCompression(packageCompressionLevel);
                if (verbose)
                    commandOutputProvider.Information("Verbose logging");
                commandOutputProvider.Information("Packing {id:l} version {Version}...", id, version);

                packageBuilder.BuildPackage(basePath,
                    includes,
                    metadata,
                    outFolder,
                    overwrite,
                    verbose);

                if (OutputFormat == OutputFormat.Json)
                    PrintJsonOutput();
                else
                    PrintDefaultOutput();
            });
        }

        IPackageBuilder SelectFormat(PackageFormat fmt)
        {
            switch (fmt)
            {
                case PackageFormat.Zip:
                    return new ZipPackageBuilder(fileSystem, commandOutputProvider);
                case PackageFormat.NuPkg:
#pragma warning disable 618 //ignore obsolete member
                case PackageFormat.Nuget:
#pragma warning restore 618
                    return new NuGetPackageBuilder(fileSystem, commandOutputProvider);
                default:
                    throw new CommandException("Unknown package format: " + fmt);
            }
        }

        public Task Request()
        {
            return Task.WhenAny();
        }

        public void PrintDefaultOutput()
        {
            commandOutputProvider.Information("Done.");
        }

        public void PrintJsonOutput()
        {
            commandOutputProvider.Json(new
            {
                PackageId = id,
                Version = version.ToString(),
                ReleaseNotes = allReleaseNotes ?? string.Empty,
                Description = description,
                packageBuilder.PackageFormat,
                OutputFolder = outFolder,
                Files = packageBuilder.Files.Any() ? packageBuilder.Files : includes
            });
        }
    }
}
