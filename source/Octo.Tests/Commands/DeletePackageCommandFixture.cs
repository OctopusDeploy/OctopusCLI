using System;
using System.Threading.Tasks;
using System.IO;
using FluentAssertions;
using Newtonsoft.Json;
using NSubstitute;
using NUnit.Framework;
using Octopus.Cli.Commands.Releases;
using Octopus.Client.Model;

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
            deletePackageCommand.PackageName = packageId;
            Repository.BuiltInPackageRepository.GetPackage(packageId, null)
            .Returns(new PackageFromBuiltInFeedResource{Id = packageId});
            
            await deletePackageCommand.Execute(CommandLineArgs.ToArray()).ConfigureAwait(false);

            LogLines.Should().Contain($"Deleting package");
        }
    }
}
