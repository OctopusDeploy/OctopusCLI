using System;
using System.IO;
using System.Runtime.InteropServices;
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

        [SetUp]
        public void SetUp()
        {
            fileSystem = Substitute.For<IOctopusFileSystem>();
            commandOutputProvider = Substitute.For<ICommandOutputProvider>();
            installAutoCompleteCommand = new InstallAutoCompleteCommand(commandOutputProvider, fileSystem);
        }
        
        [Test]
        public async Task ShouldSupportZShell()
        {
            await installAutoCompleteCommand.Execute(new[] {"--shell=zsh"});
            var zshProfile = UserProfileHelper.ZshProfile;
            fileSystem.Received()
                .OverwriteFile(zshProfile, Arg.Is<string>(arg => arg.Contains(UserProfileHelper.ZshProfileScript)));
        }

        [Test]
        public async Task ShouldSupportPwsh()
        {
            await installAutoCompleteCommand.Execute(new[] {"--shell=pwsh"});
            var profile = UserProfileHelper.PwshProfile;
            
            fileSystem.Received()
                .OverwriteFile(profile, Arg.Is<string>(arg => arg.Contains(UserProfileHelper.PwshProfileScript)));
        }
#if NETFRAMEWORK        
        [Test]
        public async Task ShouldSupportPowershell()
        {
            await installAutoCompleteCommand.Execute(new[] {"--shell=powershell"});
            var profile = UserProfileHelper.PowershellProfile;

            fileSystem.Received()
                .OverwriteFile(profile, Arg.Is<string>(arg => arg.Contains(UserProfileHelper.PwshProfileScript)));
        }
#endif        
#if NETCOREAPP
        [Test]
        public async Task ShouldPreventInstallationOfPowershellOnLinux()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                try
                {
                    await installAutoCompleteCommand.Execute(new[] {"--shell=powershell"});
                }
                catch (NotSupportedException)
                {
                    Assert.Pass();
                }
                Assert.Fail($"Expected a {nameof(NotSupportedException)}");
            }
            else
            {
                Assert.Ignore("This test doesn't run on windows environments.");
            }
        }

        [Test]
        public async Task ShouldAllowInstallationOfPowershellOnWindows()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                await installAutoCompleteCommand.Execute(new[] {"--shell=powershell"});
                var profile = UserProfileHelper.PowershellProfile;

                fileSystem.Received()
                    .OverwriteFile(profile, Arg.Is<string>(arg => arg.Contains(UserProfileHelper.PwshProfileScript)));
            }
            else
            {
                Assert.Ignore("This test doesn't run on non-windows environments.");
            }
        }
#endif

        [Test]
        public async Task ShouldSupportBourneAgainShell()
        {
            await installAutoCompleteCommand.Execute(new[] {"--shell=bash"});

            fileSystem.Received()
                .OverwriteFile(UserProfileHelper.BashProfile,
                    Arg.Is<string>(arg => arg.Contains(UserProfileHelper.BashProfileScript)));
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
        public async Task ShouldEnsureProfileDirectoryExists()
        {
            SetupMockNoProfileFile();

            await installAutoCompleteCommand.Execute(new[] {"--shell=bash"});
            
            fileSystem.Received()
                .EnsureDirectoryExists(Path.GetDirectoryName(UserProfileHelper.BashProfile));

            void SetupMockNoProfileFile()
            {
                fileSystem.FileExists(UserProfileHelper.BashProfile).Returns(false);
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
    }
}