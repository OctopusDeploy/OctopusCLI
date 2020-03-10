using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NUnit.Framework;
using Octopus.Cli.Commands;
using Octopus.Cli.Infrastructure;
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
    
        [Test]
        public async Task ShouldSupportPowershell()
        {
            if (!ExecutionEnvironment.IsRunningOnWindows) Assert.Inconclusive("This test requires windows.");
            
            await installAutoCompleteCommand.Execute(new[] {"--shell=powershell"});
            var profile = UserProfileHelper.PowershellProfile;

            fileSystem.Received()
                .OverwriteFile(profile, Arg.Is<string>(arg => arg.Contains(UserProfileHelper.PwshProfileScript)));
        }

        [Test]
        public async Task ShouldThrowOnIllegalShellValues()
        {
            try
            {
                await installAutoCompleteCommand.Execute(new[] {"--shell=666"});
            }
            catch (CommandException)
            {
                Assert.Pass();
            }
            Assert.Fail($"Expected a {nameof(CommandException)} to be thrown.");
        }
        
        [Test]
        public async Task ShouldPreventInstallationOfPowershellOnLinux()
        {
            if (ExecutionEnvironment.IsRunningOnNix || ExecutionEnvironment.IsRunningOnMac)
            {
                try
                {
                    await installAutoCompleteCommand.Execute(new[] {"--shell=powershell"});
                }
                catch (CommandException)
                {
                    Assert.Pass();
                }
                Assert.Fail($"Expected a {nameof(CommandException)}");
            }
            else
            {
                Assert.Inconclusive("This test doesn't run on windows environments.");
            }
        }

        [Test]
        public async Task ShouldAllowInstallationOfPowershellOnWindows()
        {
            if (ExecutionEnvironment.IsRunningOnWindows)
            {
                await installAutoCompleteCommand.Execute(new[] {"--shell=powershell"});
                var profile = UserProfileHelper.PowershellProfile;

                fileSystem.Received()
                    .OverwriteFile(profile, Arg.Is<string>(arg => arg.Contains(UserProfileHelper.PwshProfileScript)));
            }
            else
            {
                Assert.Inconclusive("This test doesn't run on non-windows environments.");
            }
        }

        [Test]
        public async Task ShouldSupportBash()
        {
            await installAutoCompleteCommand.Execute(new[] {"--shell=bash"});

            fileSystem.Received()
                .OverwriteFile(UserProfileHelper.BashProfile,
                    Arg.Is<string>(arg => arg.Contains(UserProfileHelper.BashProfileScript)));
        }

        [Test]
        [TestCaseSource(nameof(GetBackupTestCases))]
        public async Task ShouldTakeABackup((string shellType, string shellProfile) shellData)
        {
            SetupMockExistingProfileFile();

            await installAutoCompleteCommand.Execute(new[] {$"--shell={shellData.shellType}"});
            
            fileSystem.Received()
                .CopyFile(shellData.shellProfile, shellData.shellProfile + ".orig");

            void SetupMockExistingProfileFile()
            {
                fileSystem.FileExists(shellData.shellProfile).Returns(true);
            }
        }

        private static IEnumerable<(string, string)> GetBackupTestCases()
        {
            yield return (SupportedShell.Bash.ToString(), UserProfileHelper.BashProfile);
            if (ExecutionEnvironment.IsRunningOnWindows)
            {
                yield return (SupportedShell.Powershell.ToString(), UserProfileHelper.PowershellProfile);    
            }
            yield return (SupportedShell.Pwsh.ToString(), UserProfileHelper.PwshProfile);
            yield return (SupportedShell.Zsh.ToString(), UserProfileHelper.ZshProfile);
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
