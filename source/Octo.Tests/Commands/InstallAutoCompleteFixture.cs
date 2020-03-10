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
using Octopus.Cli.Commands.ShellCompletion;
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
        private ZshCompletionInstaller zshCompletionInstaller;
        private BashCompletionInstaller bashCompletionInstaller;
        private PwshCompletionInstaller pwshCompletionInstaller;
        private PowershellCompletionInstaller powershellCompletionInstaller;
        private ShellCompletionInstaller[] installers;

        [SetUp]
        public void SetUp()
        {
            fileSystem = Substitute.For<IOctopusFileSystem>();
            commandOutputProvider = Substitute.For<ICommandOutputProvider>();
            zshCompletionInstaller = new ZshCompletionInstaller(commandOutputProvider, fileSystem);
            pwshCompletionInstaller = new PwshCompletionInstaller(commandOutputProvider, fileSystem);
            bashCompletionInstaller = new BashCompletionInstaller(commandOutputProvider, fileSystem);
            powershellCompletionInstaller = new PowershellCompletionInstaller(commandOutputProvider, fileSystem);
            
            installers = new ShellCompletionInstaller[]
            {
                zshCompletionInstaller, bashCompletionInstaller, pwshCompletionInstaller, powershellCompletionInstaller
            };
            
            installAutoCompleteCommand = new InstallAutoCompleteCommand(commandOutputProvider, fileSystem, installers);
        }

        [Test]
        public async Task ShouldSupportAllShellInstallers()
        {
            foreach (var installer in installers)
            {
                await installAutoCompleteCommand.Execute(new[] {$"--shell={installer.SupportedShell.ToString()}"});
                var zshProfile = installer.ProfileLocation;
                fileSystem.Received()
                    .OverwriteFile(zshProfile, Arg.Is<string>(arg => arg.Contains(installer.ProfileScript)));
            }
        }

        // [Test]
        // public async Task ShouldSupportZShell()
        // {
        //     await installAutoCompleteCommand.Execute(new[] {"--shell=zsh"});
        //     var zshProfile = $"{System.Environment.GetEnvironmentVariable("HOME")}/.zshrc";
        //     fileSystem.Received()
        //         .OverwriteFile(zshProfile, Arg.Is<string>(arg => arg.Contains(zshProfile)));
        // }
        //
        // [Test]
        // public async Task ShouldSupportPwsh()
        // {
        //     await installAutoCompleteCommand.Execute(new[] {"--shell=pwsh"});
        //     var profile = pwshCompletionInstaller.ProfileLocation;
        //     
        //     fileSystem.Received()
        //         .OverwriteFile(profile, Arg.Is<string>(arg => arg.Contains(pwshCompletionInstaller.ProfileScript)));
        // }
        //
        // [Test]
        // public async Task ShouldSupportPowershell()
        // {
        //     if (!ExecutionEnvironment.IsRunningOnWindows) Assert.Inconclusive("This test requires windows.");
        //     
        //     await installAutoCompleteCommand.Execute(new[] {"--shell=powershell"});
        //     var profile = UserProfileHelper.PowershellProfile;
        //
        //     fileSystem.Received()
        //         .OverwriteFile(profile, Arg.Is<string>(arg => arg.Contains(UserProfileHelper.PwshProfileScript)));
        // }
        //
        // [Test]
        // public async Task ShouldSupportBash()
        // {
        //     await installAutoCompleteCommand.Execute(new[] {"--shell=bash"});
        //
        //     fileSystem.Received()
        //         .OverwriteFile(UserProfileHelper.BashProfile,
        //             Arg.Is<string>(arg => arg.Contains(UserProfileHelper.BashProfileScript)));
        // }

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
                await installAutoCompleteCommand.Execute(new[]
                {
                    $"--shell={powershellCompletionInstaller.SupportedShell.ToString()}"
                });
                var profile = powershellCompletionInstaller.ProfileLocation;

                fileSystem.Received()
                    .OverwriteFile(profile, Arg.Is<string>(arg => arg.Contains(powershellCompletionInstaller.ProfileScript)));
            }
            else
            {
                Assert.Inconclusive("This test doesn't run on non-windows environments.");
            }
        }

        [Test]
        public async Task ShouldTakeABackup()
        {
            foreach (var installer in installers)
            {
                SetupMockExistingProfileFile(installer);

                await installAutoCompleteCommand.Execute(new[] {$"--shell={installer.SupportedShell.ToString()}"});
            
                fileSystem.Received()
                    .CopyFile(installer.ProfileLocation, installer.ProfileLocation + ".orig");
                
                fileSystem.ClearReceivedCalls();
            }
            
            void SetupMockExistingProfileFile(ShellCompletionInstaller installer)
            {
                fileSystem.FileExists(installer.ProfileLocation).Returns(true);
            }    
        }
        
        [Test]
        public async Task ShouldEnsureProfileDirectoryExists()
        {
            foreach (var installer in installers)
            {
                SetupMockNoProfileFile(installer);

                await installAutoCompleteCommand.Execute(new[] {$"--shell={installer.SupportedShell.ToString()}"});
                fileSystem.Received()
                    .EnsureDirectoryExists(Path.GetDirectoryName(installer.ProfileLocation));
                fileSystem.ClearReceivedCalls();
            }
            
            void SetupMockNoProfileFile(ShellCompletionInstaller installer)
            {
                fileSystem.FileExists(installer.ProfileLocation).Returns(false);
            }
        }

        [Test]
        public async Task ShouldNotWriteToDiskAndWriteToConsoleIfDryRun()
        {
            foreach (var installer in installers)
            {
                await installAutoCompleteCommand.Execute(new[] {$"--shell={installer.SupportedShell.ToString()}", "--dryRun"});
                fileSystem.DidNotReceive()
                    .OverwriteFile(installer.ProfileLocation, Arg.Is<string>(arg => arg.Contains(installer.ProfileScript)));
                commandOutputProvider.Received()
                    .Information(Arg.Is<string>(arg => arg.Contains(installer.ProfileScript)));
                fileSystem.ClearReceivedCalls();
                commandOutputProvider.ClearReceivedCalls();
            }
        }
    }
}
