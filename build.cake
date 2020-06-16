//////////////////////////////////////////////////////////////////////
// TOOLS
//////////////////////////////////////////////////////////////////////
#tool "nuget:?package=GitVersion.CommandLine&version=4.0.0"
#tool "nuget:?package=ILRepack&version=2.0.13"
#addin "nuget:?package=SharpCompress&version=0.24.0"
#addin "nuget:?package=Cake.Incubator&version=5.1.0"
#addin "nuget:?package=Cake.Docker&version=0.10.0"

using SharpCompress;
using SharpCompress.Common;
using SharpCompress.Readers;
using SharpCompress.Readers.GZip;
using SharpCompress.Readers.Tar;
using SharpCompress.Writers;
using SharpCompress.Writers.Tar;
using System.Xml;
using Cake.Incubator;
using Cake.Incubator.LoggingExtensions;

//////////////////////////////////////////////////////////////////////
// ARGUMENTS
//////////////////////////////////////////////////////////////////////
var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");
var signingCertificatePath = Argument("signing_certificate_path", "");
var signingCertificatePassword = Argument("signing_certificate_password", "");

///////////////////////////////////////////////////////////////////////////////
// GLOBAL VARIABLES
///////////////////////////////////////////////////////////////////////////////
var publishDir = "./publish";
var artifactsDir = "./artifacts";
var assetDir = "./BuildAssets";
var localPackagesDir = "../LocalPackages";
var globalAssemblyFile = "./source/Octo/Properties/AssemblyInfo.cs";
var projectToPublish = "./source/Octo/Octo.csproj";
var octoPublishFolder = $"{publishDir}/Octo";
var octoMergedFolder =  $"{publishDir}/OctoMerged";
var octopusCliFolder = "./source/Octopus.Cli";
var dotNetOctoCliFolder = "./source/Octopus.DotNet.Cli";
var dotNetOctoPublishFolder = $"{publishDir}/dotnetocto";
var dotNetOctoMergedFolder =  $"{publishDir}/dotnetocto-Merged";

string nugetVersion;


///////////////////////////////////////////////////////////////////////////////
// SETUP / TEARDOWN
///////////////////////////////////////////////////////////////////////////////
Setup(context =>
{
    var fromEnv = context.EnvironmentVariable("GitVersion.NuGetVersion");
    
    if (string.IsNullOrEmpty(fromEnv))
    { 
        var gitVersionInfo = GitVersion(new GitVersionSettings {
            OutputType = GitVersionOutput.Json
        });
        nugetVersion = gitVersionInfo.NuGetVersion;
        Information("Building OctopusCli v{0}", nugetVersion);
        Information("Informational Version {0}", gitVersionInfo.InformationalVersion);
        Verbose("GitVersion:\n{0}", gitVersionInfo.Dump());
    }
    else
    {
        nugetVersion = fromEnv;
        Information("Building OctopusCli v{0}", nugetVersion);
    }

    if(BuildSystem.IsRunningOnTeamCity)
        BuildSystem.TeamCity.SetBuildNumber(nugetVersion);
});

Teardown(context =>
{
    Information("Finished running tasks for build v{0}", nugetVersion);
});

//////////////////////////////////////////////////////////////////////
//  PRIVATE TASKS
//////////////////////////////////////////////////////////////////////

Task("Clean")
    .Does(() =>
{
    CleanDirectory(artifactsDir);
    CleanDirectory(publishDir);
    CleanDirectories("./source/**/bin");
    CleanDirectories("./source/**/obj");
    CleanDirectories("./source/**/TestResults");
});

Task("Restore")
    .IsDependentOn("Clean")
    .Does(() => DotNetCoreRestore("source", new DotNetCoreRestoreSettings
        {
            ArgumentCustomization = args => args.Append($"/p:Version={nugetVersion}")
        }));

Task("Build")
    .IsDependentOn("Restore")
    .IsDependentOn("Clean")
    .Does(() =>
{
    DotNetCoreBuild("./source", new DotNetCoreBuildSettings
    {
        Configuration = configuration,
        ArgumentCustomization = args => args.Append($"/p:Version={nugetVersion}")
    });
});

Task("Test")
    .IsDependentOn("Build")
    .Does(() =>
    {
        //GetFiles("**/**/*Tests.csproj")
/*            .ToList()
            .ForEach(testProjectFile =>
            {
                DotNetCoreTest(testProjectFile.FullPath, new DotNetCoreTestSettings
                {
                    Configuration = configuration,
                    NoBuild = true
                });
            }); ZZDY NOSHIP */
    });

Task("DotnetPublish")
    .IsDependentOn("Test")
    .Does(() =>
{
    DotNetCorePublish(projectToPublish, new DotNetCorePublishSettings
    {
        Framework = "net452",
        Configuration = configuration,
        OutputDirectory = $"{octoPublishFolder}/netfx",
        ArgumentCustomization = args => args.Append($"/p:Version={nugetVersion}")
    });

    var portablePublishDir =  $"{octoPublishFolder}/portable";
    DotNetCorePublish(projectToPublish, new DotNetCorePublishSettings
    {
        Framework = "netcoreapp2.0" /* For compatibility until we gently phase it out. We encourage upgrading to self-contained executable. */,
        Configuration = configuration,
        OutputDirectory = portablePublishDir,
        ArgumentCustomization = args => args.Append($"/p:Version={nugetVersion}")
    });
    SignBinaries(portablePublishDir);

    CopyFileToDirectory($"{assetDir}/octo", portablePublishDir);
    CopyFileToDirectory($"{assetDir}/octo.cmd", portablePublishDir);

    var doc = new XmlDocument();
    doc.Load(@".\source\Octo\Octo.csproj");
    var rids = doc.SelectSingleNode("Project/PropertyGroup/RuntimeIdentifiers").InnerText;
    foreach (var rid in rids.Split(';'))
    {
        DotNetCorePublish(projectToPublish, new DotNetCorePublishSettings
        {
            Framework = "netcoreapp3.1",
            Configuration = configuration,
            Runtime = rid,
            OutputDirectory = $"{octoPublishFolder}/{rid}",
			ArgumentCustomization = args => args.Append($"/p:Version={nugetVersion}"),
            SelfContained = true,
            PublishSingleFile = true
        });
        if (!rid.StartsWith("linux-") && !rid.StartsWith("osx-")) {
            // Sign binaries, except linux which are verified at download, and osx which are signed on a mac
            SignBinaries($"{octoPublishFolder}/{rid}");
        }
    }
});

Task("MergeOctoExe")
    .IsDependentOn("DotnetPublish")
    .Does(() => {
        var inputFolder = $"{octoPublishFolder}/netfx";
        var outputFolder = $"{octoPublishFolder}/netfx-merged";
        CreateDirectory(outputFolder);
        ILRepack(
            $"{outputFolder}/octo.exe",
            $"{inputFolder}/octo.exe",
            System.IO.Directory.EnumerateFiles(inputFolder, "*.dll")
				.Union(System.IO.Directory.EnumerateFiles(inputFolder, "octodiff.exe"))
				.Select(f => (FilePath) f),
            new ILRepackSettings {
                Internalize = true,
                Parallel = true,
                Libs = new List<DirectoryPath>() { inputFolder }
            }
        );
        SignBinaries(outputFolder);
    });


Task("Zip")
    .IsDependentOn("MergeOctoExe")
    .IsDependentOn("DotnetPublish")
    .Does(() => {
        foreach(var dir in System.IO.Directory.EnumerateDirectories(octoPublishFolder))
        {
            var dirName = System.IO.Path.GetFileName(dir);

            if(dirName == "netfx")
                continue;

            if(dirName == "netfx-merged")
            {
                Zip(dir, $"{artifactsDir}/OctopusTools.{nugetVersion}.zip");
            }
            else
            {
                var outFile = $"{artifactsDir}/OctopusTools.{nugetVersion}.{dirName}";
                if(dirName == "portable" || dirName.Contains("win"))
                    Zip(dir, outFile + ".zip");

                if(!dirName.Contains("win"))
                    TarGzip(dir, outFile,
                        insertCapitalizedOctoWrapper: dirName.Contains("linux"),
                        insertCapitalizedDotNetWrapper: dirName == "portable");
            }
        }
    });


Task("PackOctopusToolsNuget")
    .IsDependentOn("DotnetPublish")
    .Does(() => {
        var nugetPackDir = $"{publishDir}/nuget";
        var nuspecFile = "OctopusTools.nuspec";

        CopyDirectory($"{octoPublishFolder}/win-x64", nugetPackDir);
        CopyFileToDirectory($"{assetDir}/icon.png", nugetPackDir);
        CopyFileToDirectory($"{assetDir}/LICENSE.txt", nugetPackDir);
        CopyFileToDirectory($"{assetDir}/VERIFICATION.txt", nugetPackDir);
        CopyFileToDirectory($"{assetDir}/init.ps1", nugetPackDir);
        CopyFileToDirectory($"{assetDir}/{nuspecFile}", nugetPackDir);

        NuGetPack($"{nugetPackDir}/{nuspecFile}", new NuGetPackSettings {
            Version = nugetVersion,
            OutputDirectory = artifactsDir
        });
    });

Task("PackDotNetOctoNuget")
	.IsDependentOn("DotnetPublish")
    .Does(() => {

		SignBinaries($"{octopusCliFolder}/bin/{configuration}");

		DotNetCorePack(octopusCliFolder, new DotNetCorePackSettings
		{
			Configuration = configuration,
			OutputDirectory = artifactsDir,
			ArgumentCustomization = args => args.Append($"/p:Version={nugetVersion}"),
            NoBuild = true,
            IncludeSymbols = false
		});

		SignBinaries($"{dotNetOctoCliFolder}/bin/{configuration}");

		DotNetCorePack(dotNetOctoCliFolder, new DotNetCorePackSettings
		{
			Configuration = configuration,
			OutputDirectory = artifactsDir,
			ArgumentCustomization = args => args.Append($"/p:Version={nugetVersion}"),
            NoBuild = true,
            IncludeSymbols = false
		});
    });

Task("CopyToLocalPackages")
    .WithCriteria(BuildSystem.IsLocalBuild)
    .IsDependentOn("PackOctopusToolsNuget")
    .IsDependentOn("PackDotNetOctoNuget")
    .IsDependentOn("Zip")
    .Does(() =>
{
    CreateDirectory(localPackagesDir);
    CopyFileToDirectory($"{artifactsDir}/Octopus.Cli.{nugetVersion}.nupkg", localPackagesDir);
    CopyFileToDirectory($"{artifactsDir}/Octopus.DotNet.Cli.{nugetVersion}.nupkg", localPackagesDir);
});

Task("AssertPortableArtifactsExists")
    .Does(() =>
{
    if (IsRunningOnWindows())
    {    
        var file = artifactsDir + $"/OctopusTools.{nugetVersion}.portable.zip";
        if (!FileExists(file))
            throw new Exception($"This build requires the portable zip at {file}. This either means the tools package wasn't build successfully, or the build artifacts were not put into the expected location.");
    } 
    else
    {
        var file = artifactsDir + $"/OctopusTools.{nugetVersion}.portable.tar.gz";
        if (!FileExists(file))
            throw new Exception($"This build requires the portable tar.gz file at {file}. This either means the tools package wasn't build successfully, or the build artifacts were not put into the expected location.");
    }
});

Task("AssertLinuxSelfContainedArtifactsExists")
    .Does(() =>
{
    var file = artifactsDir + $"/OctopusTools.{nugetVersion}.linux-x64.tar.gz";
    if (!FileExists(file))
        throw new Exception($"This build requires the linux self-contained tar.gz file at {file}. This either means the tools package wasn't build successfully, or the build artifacts were not put into the expected location.");
});

Task("BuildDockerImage")
    .IsDependentOn("AssertPortableArtifactsExists")
    .Does(() => 
{
    var platform = "nanoserver";
    if (IsRunningOnUnix())
    {
        platform = "alpine";
    }
    var tag = $"octopusdeploy/octo-prerelease:{nugetVersion}-{platform}";
    var latest = $"octopusdeploy/octo-prerelease:latest-{platform}";

    DockerBuild(new DockerImageBuildSettings { File = $"Dockerfiles/{platform}/Dockerfile", Tag = new [] { tag, latest }, BuildArg = new [] { $"OCTO_TOOLS_VERSION={nugetVersion}"} }, "artifacts");

    //test that we can run
    var stdOut = DockerRun(tag, "version", "--rm");
    
    if (stdOut == nugetVersion)
    {
        Information($"Image successfully created - running 'docker run {tag} version --rm' returned '{stdOut}'");
    }
    else 
    {
        throw new Exception($"Built image did not return expected version {nugetVersion} - it returned {stdOut}");
    }
    
    DockerPush(tag);
    DockerPush(latest);
});

Task("CreateLinuxPackages")
    .IsDependentOn("AssertLinuxSelfContainedArtifactsExists")
    .Does(() =>
{
    UnTarGZip(
        artifactsDir + $"/OctopusTools.{nugetVersion}.linux-x64.tar.gz",
        artifactsDir + $"/OctopusTools.{nugetVersion}.linux-x64.extracted");

    DockerRunWithoutResult(new DockerContainerRunSettings {
        Rm = true,
        Tty = true,
        Env = new string[] { 
            $"VERSION={nugetVersion}",
            "BINARIES_PATH=/app/",
            "COMMAND_FILE=octo",
            "INSTALL_PATH=/opt/octopus/octopuscli",
            "PACKAGE_NAME=octopuscli",
            "PACKAGE_DESC=Command line tool for Octopus Deploy",
            "FPM_OPTS=--exclude 'opt/octopus/octopuscli/Octo'",
            "FPM_DEB_OPTS=--depends 'liblttng-ust0' --depends 'libcurl3 | libcurl4' --depends 'libssl1.0.0 | libssl1.0.2 | libssl1.1' --depends 'libkrb5-3' --depends 'zlib1g' --depends 'libicu52 | libicu55 | libicu57 | libicu60 | libicu63 | libicu66'",
            // Note: Microsoft recommends dep 'lttng-ust' but it seems to be unavailable in CentOS 7, so we're omitting it for now.
            // As it's related to tracing, hopefully it will not be required for normal usage.
            "FPM_RPM_OPTS=--depends 'libcurl' --depends 'openssl-libs' --depends 'krb5-libs' --depends 'zlib' --depends 'libicu'",
            "PACKAGES_PATH=/out"
        },
        Volume = new string[] { 
            $"{Environment.CurrentDirectory}/BuildAssets:/build",
            $"{Environment.CurrentDirectory}/artifacts/OctopusTools.{nugetVersion}.linux-x64.extracted:/app",
            $"{Environment.CurrentDirectory}/artifacts:/out"
        }
    }, "octopusdeploy/package-linux-docker:latest", "bash /build/create-linux-packages.sh");
    
    DeleteDirectory(artifactsDir + $"/OctopusTools.{nugetVersion}.linux-x64.extracted", new DeleteDirectorySettings { Recursive = true, Force = true });
 
    CreateDirectory($"{artifactsDir}/linuxpackages");
    MoveFiles(GetFiles($"{artifactsDir}/*.deb"), $"{artifactsDir}/linuxpackages");
    MoveFiles(GetFiles($"{artifactsDir}/*.rpm"), $"{artifactsDir}/linuxpackages");
    CopyFileToDirectory($"{assetDir}/test-linux-packages.sh", $"{artifactsDir}/linuxpackages");
    CopyFileToDirectory($"{assetDir}/repos/publish-apt.sh", $"{artifactsDir}/linuxpackages");
    CopyFileToDirectory($"{assetDir}/repos/publish-rpm.sh", $"{artifactsDir}/linuxpackages");
    CopyFileToDirectory($"{assetDir}/repos/test-apt.sh", $"{artifactsDir}/linuxpackages");
    CopyFileToDirectory($"{assetDir}/repos/test-apt-dists.sh", $"{artifactsDir}/linuxpackages");
    CopyFileToDirectory($"{assetDir}/repos/test-rpm.sh", $"{artifactsDir}/linuxpackages");
    CopyFileToDirectory($"{assetDir}/repos/test-rpm-dists.sh", $"{artifactsDir}/linuxpackages");
    TarGzip($"{artifactsDir}/linuxpackages", $"{artifactsDir}/OctopusTools.Packages.linux-x64.{nugetVersion}");
    var buildSystem = BuildSystemAliases.BuildSystem(Context);
    buildSystem.TeamCity.PublishArtifacts($"{artifactsDir}/OctopusTools.Packages.linux-x64.{nugetVersion}.tar.gz");
    DeleteDirectory($"{artifactsDir}/linuxpackages", new DeleteDirectorySettings { Recursive = true, Force = true });
});

Task("CreateDockerContainerAndLinuxPackages")
    .IsDependentOn("BuildDockerImage")
    .IsDependentOn("CreateLinuxPackages");

private void SignBinaries(string path)
{
    Information($"Signing binaries in {path}");
	var files = GetFiles(path + "/**/Octopus.*.dll");
    files.Add(GetFiles(path + "/**/octo.dll"));
    files.Add(GetFiles(path + "/**/octo.exe"));
    files.Add(GetFiles(path + "/**/octo"));
    files.Add(GetFiles(path + "/**/dotnet-octo.dll"));

	Sign(files, new SignToolSignSettings {
			ToolPath = MakeAbsolute(File("./certificates/signtool.exe")),
            TimeStampUri = new Uri("http://timestamp.verisign.com/scripts/timstamp.dll"),
            CertPath = signingCertificatePath,
            Password = signingCertificatePassword
    });
}

private void TarGzip(string path, string outputFile, bool insertCapitalizedOctoWrapper = false, bool insertCapitalizedDotNetWrapper = false)
{
    var outFile = $"{outputFile}.tar.gz";
    Information("Creating TGZ file {0} from {1}", outFile, path);
    using (var tarMemStream = new MemoryStream())
    {
        using (var tar = WriterFactory.Open(tarMemStream, ArchiveType.Tar, new TarWriterOptions(CompressionType.None, true)))
        {
            // If using a capitalized wrapper, insert it first so it wouldn't overwrite the main payload on a case-insensitive system.
            if (insertCapitalizedOctoWrapper) {
                tar.Write("Octo", $"{assetDir}/OctoWrapper.sh");
            } else if (insertCapitalizedDotNetWrapper) {
                tar.Write("Octo", $"{assetDir}/octo");
            }

            // Add the remaining files
            tar.WriteAll(path, "*", SearchOption.AllDirectories);
        }

        tarMemStream.Seek(0, SeekOrigin.Begin);

        using (Stream stream = System.IO.File.Open(outFile, FileMode.Create))
        using (var zip = WriterFactory.Open(stream, ArchiveType.GZip, CompressionType.GZip))
            zip.Write($"{outputFile}.tar", tarMemStream);
    }
    Information("Successfully created TGZ file: {0}", outFile);
}

private void UnTarGZip(string path, string destination)
{
    using (var packageStream = new FileStream(path, FileMode.Open, FileAccess.Read))
    {
        using (var gzipReader = GZipReader.Open(packageStream))
        {
            gzipReader.MoveToNextEntry();
            using (var compressionStream = (Stream) gzipReader.OpenEntryStream())
            {
                using (var reader = (IReader) TarReader.Open(compressionStream))
                {
                    while (reader.MoveToNextEntry())
                    {
                        reader.WriteEntryToDirectory(destination, new ExtractionOptions {ExtractFullPath = true, Overwrite = true});
                    }
                }
            }
        }
    }
}


//////////////////////////////////////////////////////////////////////
// TASKS
//////////////////////////////////////////////////////////////////////
Task("Default")
    .IsDependentOn("CopyToLocalPackages");

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////
RunTarget(target);
