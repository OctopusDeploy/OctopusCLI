using System.Diagnostics;
using System.Threading.Tasks;
using NSubstitute;
using NUnit.Framework;
using Octopus.Cli.Commands;
using Octopus.Cli.Util;

namespace Octo.Tests.Commands
{
    [TestFixture]
    public class InstallAutoCompleteFixture
    {
        private InstallAutoCompleteCommand installAutoCompleteCommand;
        private ICommandOutputProvider commandOutputProvider;
        private IOctopusFileSystem fileSystem;
        private IShellCommandExecutor shellCommandExecutor;

        [SetUp]
        public void SetUp()
        {
            fileSystem = Substitute.For<IOctopusFileSystem>();
            commandOutputProvider = Substitute.For<ICommandOutputProvider>();
            shellCommandExecutor = Substitute.For<IShellCommandExecutor>();
            installAutoCompleteCommand = new InstallAutoCompleteCommand(commandOutputProvider, fileSystem, shellCommandExecutor);
        }
        
        [Test]
        public async Task ShouldSupportZShell()
        {
            await installAutoCompleteCommand.Execute(new[] {"--shell=zsh"});
            var zshProfile = UserProfileHelper.ZshProfile;
            ExpectFileUpdatedAndProfileSourced();

            void ExpectFileUpdatedAndProfileSourced()
            {
                fileSystem.Received()
                    .OverwriteFile(zshProfile, Arg.Is<string>(arg => arg.Contains(UserProfileHelper.ZshProfileScript)));
                shellCommandExecutor.Received()
                    .Execute(Arg.Is<ProcessStartInfo>(psi =>
                        psi.FileName == "/usr/bin/zsh" && psi.Arguments == $"-c \"source {UserProfileHelper.ZshProfile}\""));
            }
        }

        [Test]
        public async Task ShouldSupportPowershell()
        {
            await installAutoCompleteCommand.Execute(new[] {"--shell=pwsh"});
            var profile = UserProfileHelper.GetPwshProfileForOperatingSystem();
            
            ExpectFileUpdatedAndProfileSourced();

            void ExpectFileUpdatedAndProfileSourced()
            {
                fileSystem.Received()
                    .OverwriteFile(profile, Arg.Is<string>(arg => arg.Contains(UserProfileHelper.PwshProfileScript)));
                shellCommandExecutor.Received()
                    .Execute(Arg.Is<ProcessStartInfo>(psi =>
                        psi.FileName == "powershell" && psi.Arguments == "-c \". $PROFILE\""));
            }
        }

        [Test]
        public async Task ShouldSupportBourneAgainShell()
        {
            await installAutoCompleteCommand.Execute(new[] {"--shell=bash"});
            ExpectFileUpdatedAndProfileSourced();

            void ExpectFileUpdatedAndProfileSourced()
            {
                fileSystem.Received()
                    .OverwriteFile(UserProfileHelper.BashProfile,
                        Arg.Is<string>(arg => arg.Contains(UserProfileHelper.BashProfileScript)));
                shellCommandExecutor.Received()
                    .Execute(Arg.Is<ProcessStartInfo>(psi =>
                        psi.FileName == "/usr/bin/bash" && psi.Arguments == $"-c \"source {UserProfileHelper.BashProfile}\""));
            }
        }

        [Test]
        public async Task ShouldTakeABackup()
        {
            SetupMockExistingProfileFile();

            await installAutoCompleteCommand.Execute(new[] {"--shell=bash"});
            
            fileSystem.Received()
                .CopyFile(UserProfileHelper.BashProfile, UserProfileHelper.BashProfile + ".orig");

            void SetupMockExistingProfileFile()
            {
                fileSystem.FileExists(UserProfileHelper.BashProfile).Returns(true);
            }
        }

        [Test]
        public async Task ShouldNotWriteToDiskIfDryRun()
        {
            await installAutoCompleteCommand.Execute(new[] {"--shell=bash", "--dryRun"});
            fileSystem.DidNotReceive()
                .OverwriteFile(UserProfileHelper.BashProfile, Arg.Is<string>(arg => arg.Contains(UserProfileHelper.BashProfileScript)));
        }
        
        [Test]
        public async Task ShouldWriteToConsoleOutputIfDryRun()
        {
            await installAutoCompleteCommand.Execute(new[] {"--shell=zsh", "--dryRun"});
            commandOutputProvider.Received().Information(Arg.Is<string>(arg => arg.Contains(UserProfileHelper.ZshProfileScript)));
        }

        [Test]
        public async Task ShouldExecuteDotSourcingWhenFinishedUpdatingProfile()
        {
            await installAutoCompleteCommand.Execute(new[] {"--shell=zsh"});
            
            
        }
    }
}