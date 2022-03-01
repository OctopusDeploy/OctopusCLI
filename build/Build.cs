// ReSharper disable RedundantUsingDirective - prevent PrettyBot from getting confused about unused code.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using Nuke.Common;
using Nuke.Common.Execution;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tools.Docker;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Utilities.Collections;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.Tools.DotNet.DotNetTasks;
using JetBrains.Annotations;
using Nuke.Common.CI;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.NuGet;
using Nuke.Common.Tools.OctoVersion;
using Nuke.Common.Tools.SignTool;
using SharpCompress.Common;
using SharpCompress.Readers;
using SharpCompress.Readers.GZip;
using SharpCompress.Readers.Tar;
using SharpCompress.Writers;
using SharpCompress.Writers.Tar;

[CheckBuildProjectConfigurations]
[UnsetVisualStudioEnvironmentVariables]
class Build : NukeBuild
{
    const string CiBranchNameEnvVariable = "OCTOVERSION_CurrentBranch";
    
    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")] readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;
    [Parameter("Pfx certificate to use for signing the files")] readonly AbsolutePath SigningCertificatePath = RootDirectory / "certificates" / "OctopusDevelopment.pfx";
    [Parameter("Password for the signing certificate")] readonly string SigningCertificatePassword = "Password01!";
    [Parameter("Branch name for OctoVersion to use to calculate the version number. Can be set via the environment variable " + CiBranchNameEnvVariable + ".", Name = CiBranchNameEnvVariable)]
    string BranchName { get; set; }
    [Parameter] readonly string RunNumber = "";
    
    [Solution(GenerateProjects = true)] readonly Solution Solution;

    [PackageExecutable(
        packageId: "OctoVersion.Tool",
        packageExecutable: "OctoVersion.Tool.dll",
        Framework = "net6.0")]
    readonly Tool OctoVersion;
    
    [PackageExecutable(
        packageId: "azuresigntool",
        packageExecutable: "azuresigntool.dll")]
    readonly Tool AzureSignTool = null!;

    [Parameter] readonly string AzureKeyVaultUrl = "";
    [Parameter] readonly string AzureKeyVaultAppId = "";
    [Parameter] [Secret] readonly string AzureKeyVaultAppSecret = "";
    [Parameter] readonly string AzureKeyVaultCertificateName = "";
    [Parameter] readonly string AzureKeyVaultTenantId = "";

    AbsolutePath SourceDirectory => RootDirectory / "source";
    AbsolutePath ArtifactsDirectory => RootDirectory / "artifacts";
    AbsolutePath PublishDirectory => RootDirectory / "publish";
    AbsolutePath AssetDirectory => RootDirectory / "BuildAssets";
    AbsolutePath LinuxPackageFeedsDir => RootDirectory / "linux-package-feeds";
    AbsolutePath OctopusCliDirectory => RootDirectory / "source" / "Octopus.Cli";
    AbsolutePath DotNetOctoCliFolder => RootDirectory / "source" / "Octopus.DotNet.Cli";
    AbsolutePath OctoPublishDirectory => PublishDirectory / "octo";
    AbsolutePath LocalPackagesDirectory => RootDirectory / ".." / "LocalPackages";

    string[] SigningTimestampUrls => new[]
    {
        "http://timestamp.comodoca.com/rfc3161",
        "http://timestamp.globalsign.com/tsa/r6advanced1", //https://support.globalsign.com/code-signing/code-signing-windows-7-8-and-10,
        "http://timestamp.digicert.com", //https://knowledge.digicert.com/solution/SO912.html
        "http://timestamp.apple.com/ts01", //https://gist.github.com/Manouchehri/fd754e402d98430243455713efada710
        "http://tsa.starfieldtech.com",
        "http://www.startssl.com/timestamp",
        "http://timestamp.verisign.com/scripts/timstamp.dll",
        "http://timestamp.globalsign.com/scripts/timestamp.dll",
        "https://rfc3161timestamp.globalsign.com/advanced"
    };

    string fullSemVer;
    
    Target Clean => _ => _
        .Executes(() =>
        {
            SourceDirectory.GlobDirectories("**/bin", "**/obj", "**/TestResults").ForEach(DeleteDirectory);
            EnsureCleanDirectory(ArtifactsDirectory);
            EnsureCleanDirectory(PublishDirectory);
        });
    
    Target CalculateVersion => _ => _
        .Executes(() =>
        {
            var octoVersionText = RootDirectory / "octoversion.txt";
            
            Serilog.Log.Information("Looking for existing octoversion.txt in {Path}", octoVersionText);
            if (octoVersionText.FileExists())
            {
                Serilog.Log.Information("Found existing octoversion.txt in {Path}", octoVersionText);
                fullSemVer = File.ReadAllText(octoVersionText);
                
                Serilog.Log.Information("octoversion.txt has {FullSemVer}", fullSemVer);

                return;
            }
            
            // We are calculating the version to use explicitly here so we can support nightly builds with an incrementing number as well as only have non pre-releases for tagged commits
            var arguments = $"--CurrentBranch \"{BranchName ?? "local"}\" --NonPreReleaseTagsRegex \"refs/tags/[^-]*$\" --OutputFormats Json";

            var jObject = OctoVersion(arguments, customLogger: LogStdErrAsWarning).StdToJson();
            fullSemVer = jObject.Value<string>("FullSemVer");
            
            if (!String.IsNullOrEmpty(jObject.Value<string>("PreReleaseTag")))
            {
                fullSemVer += RunNumber;
            }
                
            File.WriteAllText(octoVersionText, fullSemVer);

            Console.WriteLine($"::notice Release version number: {fullSemVer}");
        });

    Target Compile => _ => _
        .DependsOn(Clean)
        .DependsOn(CalculateVersion)
        .Executes(() =>
        {
            Serilog.Log.Information("Building OctopusCLI v{0}", fullSemVer);

            DotNetBuild(_ => _
                .SetProjectFile(Solution)
                .SetConfiguration(Configuration)
                .SetVersion(fullSemVer));
        });

    [PublicAPI]
    Target Test => _ => _
        .Executes(() =>
        {
            DotNetTest(_ => _
                .SetProjectFile(Solution)
                .SetConfiguration(Configuration)
                .SetResultsDirectory(ArtifactsDirectory / "TestResults")
                .AddLoggers(
                    "console;verbosity=detailed",
                    "trx"
                ));
        });

    Target DotnetPublish => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            var portablePublishDir = OctoPublishDirectory / "portable";
            DotNetPublish(_ => _
                .SetProject(Solution.Octo)
                .SetFramework("netcoreapp3.1") 
                .SetConfiguration(Configuration)
                .SetOutput(portablePublishDir)
                .SetVersion(fullSemVer));

            SignBinaries(portablePublishDir);

            CopyFileToDirectory(AssetDirectory / "octo", portablePublishDir, FileExistsPolicy.Overwrite);
            CopyFileToDirectory(AssetDirectory / "octo.cmd", portablePublishDir, FileExistsPolicy.Overwrite);

            var doc = new XmlDocument();
            doc.Load(Solution.Octo.Path);
            var selectSingleNode = doc.SelectSingleNode("Project/PropertyGroup/RuntimeIdentifiers");
            if (selectSingleNode == null)
                throw new ApplicationException("Unable to find Project/PropertyGroup/RuntimeIdentifiers in Octo.csproj");
            var rids = selectSingleNode.InnerText;
            foreach (var rid in rids.Split(';'))
            {
                DotNetPublish(_ => _
                    .SetProject(Solution.Octo)
                    .SetConfiguration(Configuration)
                    .SetFramework("net6.0")
                    .SetRuntime(rid)
                    .EnableSelfContained()
                    .EnablePublishSingleFile()
                    .SetOutput(OctoPublishDirectory / rid)
                    .SetVersion(fullSemVer));

                if (!rid.StartsWith("linux-") && !rid.StartsWith("osx-"))
                    // Sign binaries, except linux which are verified at download, and osx which are signed on a mac
                    SignBinaries(OctoPublishDirectory / rid);
            }
        });


    Target Zip => _ => _
        .DependsOn(DotnetPublish)
        .Executes(() =>
        {
            foreach (var dir in Directory.EnumerateDirectories(OctoPublishDirectory))
            {
                var dirName = Path.GetFileName(dir);

                var outFile = ArtifactsDirectory / $"OctopusTools.{fullSemVer}.{dirName}";
                if (dirName == "portable" || dirName.Contains("win"))
                    CompressionTasks.CompressZip(dir, outFile + ".zip");

                if (!dirName.Contains("win"))
                    TarGzip(dir, outFile);
            }
        });

    Target PackOctopusToolsNuget => _ => _
        .DependsOn(DotnetPublish)
        .Executes(() =>
        {
            var nugetPackDir = PublishDirectory / "nuget";
            var nuspecFile = "OctopusTools.nuspec";

            CopyDirectoryRecursively(OctoPublishDirectory / "win-x64", nugetPackDir);
            CopyFileToDirectory(AssetDirectory / "icon.png", nugetPackDir);
            CopyFileToDirectory(AssetDirectory / "LICENSE.txt", nugetPackDir);
            CopyFileToDirectory(AssetDirectory / "VERIFICATION.txt", nugetPackDir);
            CopyFileToDirectory(AssetDirectory / "init.ps1", nugetPackDir);
            CopyFileToDirectory(AssetDirectory / nuspecFile, nugetPackDir);

            NuGetTasks.NuGetPack(_ => _
                .SetTargetPath(nugetPackDir / nuspecFile)
                .SetVersion(fullSemVer)
                .SetOutputDirectory(ArtifactsDirectory));
        });

    Target PackDotNetOctoNuget => _ => _
        .DependsOn(DotnetPublish)
        .Executes(() =>
        {
            SignBinaries(OctopusCliDirectory / "bin" / Configuration);

            DotNetPack(_ => _
                .SetProject(OctopusCliDirectory)
                .SetConfiguration(Configuration)
                .SetOutputDirectory(ArtifactsDirectory)
                .SetVersion(fullSemVer)
                .EnableNoBuild()
                .DisableIncludeSymbols());

            SignBinaries(DotNetOctoCliFolder / "bin" / Configuration);

            DotNetPack(_ => _
                .SetProject(DotNetOctoCliFolder)
                .SetConfiguration(Configuration)
                .SetOutputDirectory(ArtifactsDirectory)
                .SetVersion(fullSemVer)
                .EnableNoBuild()
                .DisableIncludeSymbols());
        });

    Target AssertPortableArtifactsExists => _ => _
        .Executes(() =>
        {
            if (EnvironmentInfo.IsWin)
            {
                var file = ArtifactsDirectory / $"OctopusTools.{fullSemVer}.portable.zip";
                if (!file.FileExists())
                    throw new Exception($"This build requires the portable zip at {file}. This either means the tools package wasn't build successfully, or the build artifacts were not put into the expected location.");
            }
            else
            {
                var file = ArtifactsDirectory / $"OctopusTools.{fullSemVer}.portable.tar.gz";
                if (!file.FileExists())
                    throw new Exception($"This build requires the portable tar.gz file at {file}. This either means the tools package wasn't build successfully, or the build artifacts were not put into the expected location.");
            }
        });

    Target AssertLinuxSelfContainedArtifactsExists => _ => _
        .Executes(() =>
        {
            var file = ArtifactsDirectory / $"OctopusTools.{fullSemVer}.linux-x64.tar.gz";
            if (!file.FileExists())
                throw new Exception($"This build requires the linux self-contained tar.gz file at {file}. This either means the tools package wasn't build successfully, or the build artifacts were not put into the expected location.");
        });

    Target MuteLoudDockerCli => _ => _
        .Executes(() =>
        {
            //docker sends lots to stderr
            DockerTasks.DockerLogger = (_, s) => Serilog.Log.Information(s);
        });
    
    [PublicAPI]
    Target BuildDockerImage => _ => _
        .DependsOn(CalculateVersion)
        .DependsOn(MuteLoudDockerCli)
        .DependsOn(AssertPortableArtifactsExists)
        .Executes(() =>
        {
            const string platform = "alpine";
            var tag = $"octopusdeploy/octo-prerelease:{fullSemVer}-{platform}";
            var latest = $"octopusdeploy/octo-prerelease:latest-{platform}";

            DockerTasks.DockerBuild(_ => _
                .SetFile(RootDirectory / "Dockerfiles" / platform / "Dockerfile")
                .SetTag(tag, latest)
                .SetBuildArg($"OCTO_TOOLS_VERSION={fullSemVer}")
                .SetPath(ArtifactsDirectory)
            );

            //test that we can run
            var stdOut = DockerTasks.DockerRun(_ => _
                .SetImage(tag)
                .SetCommand("version")
                .EnableRm());

            var text = stdOut.FirstOrDefault().Text;
            if (text == fullSemVer)
                Serilog.Log.Information($"Image successfully created - running 'docker run {tag} version --rm' returned '{string.Join('\n', stdOut.Select(x => x.Text))}'");
            else
                throw new Exception($"Built image did not return expected version {fullSemVer} - it returned {text}");

            var tarFile = $"Octo.Docker.Image.{fullSemVer}.tar";
            var gzipFile = $"{tarFile}.gz";

            DockerTasks.DockerImageSave(_ => _.SetImages("octopusdeploy/octo-prerelease").SetOutput(ArtifactsDirectory / tarFile));

            using Stream stream = File.Open(ArtifactsDirectory / gzipFile, FileMode.Create);
            using var zip = WriterFactory.Open(stream, ArchiveType.GZip, CompressionType.GZip);
            zip.Write(tarFile, ArtifactsDirectory / tarFile);
            zip.Dispose();
            
            DeleteFile(ArtifactsDirectory / tarFile);
        });

    [PublicAPI]
    Target TestLinuxPackages => _ => _
        .DependsOn(CalculateVersion)
        .DependsOn(MuteLoudDockerCli)
        .DependsOn(AssertLinuxSelfContainedArtifactsExists)
        .Executes(() =>
        {
            var packagesPath = ArtifactsDirectory / "OctopusTools.Packages";
            CompressionTasks.UncompressZip(
                ArtifactsDirectory / $"OctopusTools.Packages.linux-x64.{fullSemVer}.zip",
                packagesPath);

            var config = LinuxPackageFeedsDir / "test-env-docker-images.conf";
            foreach (var dockerImage in File.ReadLines(config))
            {
                DockerTasks.DockerRun(_ => _
                    .EnableRm()
                    .EnableTty()
                    .SetEnv(@"OCTOPUS_CLI_SERVER",
                        "OCTOPUS_CLI_API_KEY",
                        "REDHAT_SUBSCRIPTION_USERNAME",
                        "REDHAT_SUBSCRIPTION_PASSWORD",
                        "OCTOPUS_SPACE=Integrations",
                        "OCTOPUS_EXPECT_ENV=Components - Internal",
                        "PKG_PATH_PREFIX=octopuscli")
                    .SetVolume(packagesPath + ":/working",
                        LinuxPackageFeedsDir + ":/opt/linux-package-feeds",
                        AssetDirectory / "test-linux-package.sh" + ":/test-linux-package.sh")
                    .SetImage(dockerImage)
                    .SetCommand("bash")
                    .SetArgs("-c", "cd /working && bash /test-linux-package.sh"));
            }
        });

    [PublicAPI]
    Target CreateLinuxPackages => _ => _
        .DependsOn(CalculateVersion)
        .DependsOn(MuteLoudDockerCli)
        .DependsOn(AssertLinuxSelfContainedArtifactsExists)
        .Executes(() =>
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SIGN_PRIVATE_KEY"))
                || string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SIGN_PASSPHRASE")))
                throw new Exception("This build requires environment variables `SIGN_PRIVATE_KEY` (in a format gpg1 can import)"
                    + " and `SIGN_PASSPHRASE`, which are used to sign the .rpm.");

            if (!LinuxPackageFeedsDir.DirectoryExists())
                throw new Exception($"This build requires `{LinuxPackageFeedsDir}` to contain scripts from https://github.com/OctopusDeploy/linux-package-feeds.\n"
                    + "They are usually added as an Artifact Dependency in TeamCity from 'Infrastructure / Linux Package Feeds' with the rule:\n"
                    + "  LinuxPackageFeedsTools.*.zip!*=>linux-package-feeds\n"
                    + "See https://build.octopushq.com/admin/editDependencies.html?id=buildType:OctopusDeploy_OctopusCLI_BuildLinuxContainer");

            UnTarGZip(
                ArtifactsDirectory / $"OctopusTools.{fullSemVer}.linux-x64.tar.gz",
                ArtifactsDirectory / $"OctopusTools.{fullSemVer}.linux-x64.extracted");

            DockerTasks.DockerRun(_ => _
                .EnableRm()
                .EnableTty()
                .SetEnv($"VERSION={fullSemVer}",
                    $"BINARIES_PATH=/artifacts/OctopusTools.{fullSemVer}.linux-x64.extracted/",
                    "PACKAGES_PATH=/artifacts",
                    "SIGN_PRIVATE_KEY",
                    "SIGN_PASSPHRASE")
                .SetVolume(AssetDirectory + ":/BuildAssets",
                    LinuxPackageFeedsDir + ":/opt/linux-package-feeds",
                    ArtifactsDirectory + ":/artifacts")
                .SetImage("octopusdeploy/package-linux-docker:latest")
                .SetCommand("bash")
                .SetArgs("/BuildAssets/create-octopuscli-linux-packages.sh"));

            DeleteDirectory(ArtifactsDirectory / $"OctopusTools.{fullSemVer}.linux-x64.extracted");
        
            var linuxPackagesDir = ArtifactsDirectory / "linuxpackages";
            EnsureExistingDirectory(linuxPackagesDir);
            ArtifactsDirectory.GlobFiles("*.deb").ForEach(path => MoveFile(path, linuxPackagesDir / new FileInfo(path).Name));
            ArtifactsDirectory.GlobFiles("*.rpm").ForEach(path => MoveFile(path, linuxPackagesDir / new FileInfo(path).Name));
            CopyFileToDirectory($"{LinuxPackageFeedsDir}/publish-apt.sh", linuxPackagesDir);
            CopyFileToDirectory($"{LinuxPackageFeedsDir}/publish-rpm.sh", linuxPackagesDir);
            CopyFileToDirectory(AssetDirectory / "repos" / "test-linux-package-from-feed-in-dists.sh", linuxPackagesDir);
            CopyFileToDirectory(AssetDirectory / "repos" / "test-linux-package-from-feed.sh", linuxPackagesDir);
            CopyFileToDirectory($"{LinuxPackageFeedsDir}/test-env-docker-images.conf", linuxPackagesDir);
            CopyFileToDirectory($"{LinuxPackageFeedsDir}/install-linux-feed-package.sh", linuxPackagesDir);
            CompressionTasks.CompressZip(linuxPackagesDir, ArtifactsDirectory / $"OctopusTools.Packages.linux-x64.{fullSemVer}.zip");
            DeleteDirectory(linuxPackagesDir);
        });

    [PublicAPI]
    Target CreateDockerContainerAndLinuxPackages => _ => _
        .DependsOn(BuildDockerImage)
        .DependsOn(CreateLinuxPackages);

    [PublicAPI]
    Target CopyToLocalPackages => _ => _
        .OnlyWhenStatic(() => IsLocalBuild)
        .TriggeredBy(PackOctopusToolsNuget)
        .TriggeredBy(PackDotNetOctoNuget)
        .TriggeredBy(Zip)
        .Executes(() =>
        {
            EnsureExistingDirectory(LocalPackagesDirectory);
            CopyFileToDirectory(ArtifactsDirectory / $"Octopus.Cli.{fullSemVer}.nupkg", LocalPackagesDirectory, FileExistsPolicy.Overwrite);
            CopyFileToDirectory(ArtifactsDirectory / $"Octopus.DotNet.Cli.{fullSemVer}.nupkg", LocalPackagesDirectory, FileExistsPolicy.Overwrite);
        });

    Target Default => _ => _
        .DependsOn(PackOctopusToolsNuget)
        .DependsOn(PackDotNetOctoNuget)
        .DependsOn(Zip);

    void SignBinaries(string path)
    {
        Serilog.Log.Information($"Signing binaries in {path}");

        var files = Directory.EnumerateFiles(path, "Octopus.*.dll", SearchOption.AllDirectories).ToList();
        files.AddRange(Directory.EnumerateFiles(path, "octo.dll", SearchOption.AllDirectories));
        files.AddRange(Directory.EnumerateFiles(path, "octo.exe", SearchOption.AllDirectories));
        files.AddRange(Directory.EnumerateFiles(path, "dotnet-octo.dll", SearchOption.AllDirectories));
        files.AddRange(Directory.EnumerateFiles(path, "octo*.dll", SearchOption.AllDirectories));
        files.AddRange(Directory.EnumerateFiles(path, "Octo*.dll", SearchOption.AllDirectories));
        var distinctFiles = files.Distinct().ToArray();

        var useSignTool = string.IsNullOrEmpty(AzureKeyVaultUrl)
            && string.IsNullOrEmpty(AzureKeyVaultAppId)
            && string.IsNullOrEmpty(AzureKeyVaultAppSecret)
            && string.IsNullOrEmpty(AzureKeyVaultCertificateName);

        var lastException = default(Exception);
        foreach (var url in SigningTimestampUrls)
        {
            Serilog.Log.Information("Signing and timestamping with server {Url}", url);
            try
            {
                if (useSignTool)
                    SignWithSignTool(distinctFiles, url);
                else
                    SignWithAzureSignTool(distinctFiles, url);
                lastException = null;
            }
            catch (Exception ex)
            {
                lastException = ex;
            }

            if (lastException == null)
                break;
        }

        if (lastException != null)
            throw lastException;
        Serilog.Log.Information($"Finished signing {distinctFiles.Length} files.");
    }

    void SignWithAzureSignTool(IEnumerable<string> files, string timestampUrl)
    {
        Serilog.Log.Information("Signing files using azuresigntool and the production code signing certificate.");

        var arguments = "sign " +
            $"--azure-key-vault-url \"{AzureKeyVaultUrl}\" " +
            $"--azure-key-vault-client-id \"{AzureKeyVaultAppId}\" " +
            $"--azure-key-vault-tenant-id \"{AzureKeyVaultTenantId}\" " +
            $"--azure-key-vault-client-secret \"{AzureKeyVaultAppSecret}\" " +
            $"--azure-key-vault-certificate \"{AzureKeyVaultCertificateName}\" " +
            "--file-digest sha256 " +
            "--description \"Octopus CLI\" " +
            "--description-url \"https://octopus.com\" " +
            $"--timestamp-rfc3161 {timestampUrl} " +
            "--timestamp-digest sha256 ";

        foreach (var file in files)
            arguments += $"\"{file}\" ";

        AzureSignTool(arguments, customLogger: LogStdErrAsWarning);
    }

    void SignWithSignTool(IEnumerable<string> files, string url)
    {
        Serilog.Log.Information("Signing files using signtool.");
        SignToolTasks.SignToolLogger = LogStdErrAsWarning;

        SignToolTasks.SignTool(_ => _
            .SetFile(SigningCertificatePath)
            .SetPassword(SigningCertificatePassword)
            .SetFiles(files)
            .SetProcessToolPath(RootDirectory / "certificates" / "signtool.exe")
            .SetTimestampServerDigestAlgorithm("sha256")
            .SetDescription("Octopus CLI")
            .SetUrl("https://octopus.com")
            .SetRfc3161TimestampServerUrl(url));
    }

    static void LogStdErrAsWarning(OutputType type, string message)
    {
        if (type == OutputType.Err)
            Serilog.Log.Warning(message);
        else
            Serilog.Log.Debug(message);
    }

    void TarGzip(string path, string outputFile)
    {
        var outFile = $"{outputFile}.tar.gz";
        Serilog.Log.Information("Creating TGZ file {0} from {1}", outFile, path);
        using (var tarMemStream = new MemoryStream())
        {
            using (var tar = WriterFactory.Open(tarMemStream, ArchiveType.Tar, new TarWriterOptions(CompressionType.None, true)))
            {
                // Add the remaining files
                tar.WriteAll(path, "*", SearchOption.AllDirectories);
            }

            tarMemStream.Seek(0, SeekOrigin.Begin);

            using (Stream stream = File.Open(outFile, FileMode.Create))
            using (var zip = WriterFactory.Open(stream, ArchiveType.GZip, CompressionType.GZip))
            {
                zip.Write($"{outputFile}.tar", tarMemStream);
            }
        }

        Serilog.Log.Information("Successfully created TGZ file: {0}", outFile);
    }

    void UnTarGZip(string path, string destination)
    {
        using (var packageStream = new FileStream(path, FileMode.Open, FileAccess.Read))
        {
            using (var gzipReader = GZipReader.Open(packageStream))
            {
                gzipReader.MoveToNextEntry();
                using (var compressionStream = gzipReader.OpenEntryStream())
                {
                    using (var reader = TarReader.Open(compressionStream))
                    {
                        while (reader.MoveToNextEntry())
                        {
                            var entryDestination = Path.Combine(destination, reader.Entry.Key);
                            if (EnvironmentInfo.IsWin && File.Exists(entryDestination))
                                // In Windows, remove existing files before overwrite, to prevent existing filename case sticking
                                File.Delete(entryDestination);

                            reader.WriteEntryToDirectory(destination, new ExtractionOptions { ExtractFullPath = true, Overwrite = true });
                        }
                    }
                }
            }
        }
    }

    /// Support plugins are available for:
    /// - JetBrains ReSharper        https://nuke.build/resharper
    /// - JetBrains Rider            https://nuke.build/rider
    /// - Microsoft VisualStudio     https://nuke.build/visualstudio
    /// - Microsoft VSCode           https://nuke.build/vscode
    public static int Main() => Execute<Build>(x => x.Default);
}
