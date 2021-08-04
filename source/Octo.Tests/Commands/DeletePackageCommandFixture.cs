using System;
using System.Threading.Tasks;
using System.IO;
using FluentAssertions;
using Newtonsoft.Json;
using NSubstitute;
using NUnit.Framework;
using Octopus.Cli.Commands.Releases;
using Octopus.Client.Model;
using Octopus.CommandLine.Commands;

namespace Octo.Tests.Commands
{
    public class DeletePackageCommandFixture : ApiCommandFixtureBase
    {
        DeletePackageCommand deletePackageCommand;
        string packageId;

        [SetUp]
        public void Setup()
        {
            deletePackageCommand = new DeletePackageCommand(RepositoryFactory,
                FileSystem,
                ClientFactory,
                CommandOutputProvider);

            packageId = Guid.NewGuid().ToString();
            
        }

        [Test]
        public async Task DefaultOutput_ShouldDeleteTheGivenPackage()
        {
            deletePackageCommand.PackageId = packageId;
            Repository.BuiltInPackageRepository.GetPackage(packageId, null)
            .Returns(new PackageFromBuiltInFeedResource{Id = packageId});
            
            await deletePackageCommand.Execute(CommandLineArgs.ToArray()).ConfigureAwait(false);

            LogLines.Should().Contain($"Deleting package");
        }

        [Test]
        public async Task ErrorOutput_ShouldThrowErrorWhenPackageIsNotAvailable()
        {
            deletePackageCommand.PackageId = packageId;
            
            await deletePackageCommand.Execute(CommandLineArgs.ToArray()).ConfigureAwait(false);

            LogLines.Should().Contain($"There is no available package to be deleted");
        }

        [Test]
        public void CommandException_ShouldNotSearchForPackageWhenThereISNoPackageId()
        {            
            Func<Task> exec = () => deletePackageCommand.Execute(CommandLineArgs.ToArray());
            exec.ShouldThrow<CommandException>()
                .WithMessage("Please specify a package name using the parameter: --package=XYZ");

    
        }
    }
}
