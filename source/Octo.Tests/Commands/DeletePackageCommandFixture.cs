using System;
using System.Threading.Tasks;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Cli.Commands.Package;
using Octopus.Client.Model;
using Octopus.CommandLine.Commands;

namespace Octo.Tests.Commands
{
    public class DeletePackageCommandFixture : ApiCommandFixtureBase
    {
        DeletePackageCommand deletePackageCommand;
        string packageId;
        string packageVersion;

        [SetUp]
        public void Setup()
        {
            deletePackageCommand = new DeletePackageCommand(RepositoryFactory,
                FileSystem,
                ClientFactory,
                CommandOutputProvider);

            packageId = "TestPackage";
            packageVersion = "1.0.0";
        }

        [Test]
        public async Task DefaultOutput_ShouldDeleteTheGivenPackage()
        {
            deletePackageCommand.PackageId = packageId;
            deletePackageCommand.PackageVersion = packageVersion;
            Repository.BuiltInPackageRepository.GetPackage(packageId, packageVersion)
                .Returns(new PackageFromBuiltInFeedResource { PackageId = packageId, Version = packageVersion});

            await deletePackageCommand.Execute(CommandLineArgs.ToArray()).ConfigureAwait(false);
            
            LogLines.Should().Contain("Package deleted");
            
            deletePackageCommand.PrintJsonOutput();
            
            var logOutput = LogOutput.ToString();
            Assert.True(logOutput.Contains("\"Status\": \"Success\""));
            Assert.True(logOutput.Contains($"\"PackageId\": \"{packageId}\""));
            Assert.True(logOutput.Contains($"\"Version\": \"{packageVersion}\""));
        }

        [Test]
        public void CommandException_ShouldNotSearchForPackageWhenThereIsNoPackageId()
        {
            Func<Task> exec = () => deletePackageCommand.Execute(CommandLineArgs.ToArray());
            exec.ShouldThrow<CommandException>()
                .WithMessage("Please specify a package id using the parameter: --packageId=XYZ");
        }
        
        [Test]
        public void CommandException_ShouldNotSearchForPackageWhenThereIsNoPackageVersion()
        {
            deletePackageCommand.PackageId = "TestPackage";
            Func<Task> exec = () => deletePackageCommand.Execute(CommandLineArgs.ToArray());
            exec.ShouldThrow<CommandException>()
                .WithMessage("Please specify a package version using the parameter: --version=1.0.0");
        }
    }
}
