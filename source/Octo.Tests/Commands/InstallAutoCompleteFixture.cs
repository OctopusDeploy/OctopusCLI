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
        private static ICommandOutputProvider commandOutputProvider;
        private static IOctopusFileSystem fileSystem;

        [SetUp]
        public void SetUp()
        {
            fileSystem = Substitute.For<IOctopusFileSystem>();
            commandOutputProvider = Substitute.For<ICommandOutputProvider>();

            installAutoCompleteCommand = new InstallAutoCompleteCommand(commandOutputProvider, GetShellCompletionInstallers());
        }

        private static IEnumerable<ShellCompletionInstaller> GetShellCompletionInstallers()
        {
            
            var zshCompletionInstaller = new ZshCompletionInstaller(commandOutputProvider, fileSystem);
            var pwshCompletionInstaller = new PwshCompletionInstaller(commandOutputProvider, fileSystem);
            var bashCompletionInstaller = new BashCompletionInstaller(commandOutputProvider, fileSystem);
            var powershellCompletionInstaller = new PowershellCompletionInstaller(commandOutputProvider, fileSystem);

            var installers = new List<ShellCompletionInstaller>
            {
                zshCompletionInstaller, bashCompletionInstaller, pwshCompletionInstaller
            };

            if (ExecutionEnvironment.IsRunningOnWindows)
            {
                installers.Add(powershellCompletionInstaller);
            }

            return installers;
        }

        [Test]
        [TestCaseSource(nameof(GetShellCompletionInstallers))]
        public async Task ShouldSupportAllShellInstallers(ShellCompletionInstaller installer)
        {
                await installAutoCompleteCommand.Execute(new[] {$"--shell={installer.SupportedShell.ToString()}"});
                var zshProfile = installer.ProfileLocation;
                fileSystem.Received()
                    .OverwriteFile(zshProfile, Arg.Is<string>(arg => arg.Contains(installer.ProfileScript)));
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
                    var installers = new[] {new PowershellCompletionInstaller(commandOutputProvider, fileSystem) };
                    installAutoCompleteCommand = new InstallAutoCompleteCommand(commandOutputProvider, installers);
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
            var powershellCompletionInstaller = new PowershellCompletionInstaller(commandOutputProvider, fileSystem);
            
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
        [TestCaseSource(nameof(GetShellCompletionInstallers))]
        public async Task ShouldTakeABackup(ShellCompletionInstaller installer)
        {
            SetupMockExistingProfileFile(installer);

            await installAutoCompleteCommand.Execute(new[] {$"--shell={installer.SupportedShell.ToString()}"});
        
            fileSystem.Received()
                .CopyFile(installer.ProfileLocation, installer.ProfileLocation + ".orig");
            
            void SetupMockExistingProfileFile(ShellCompletionInstaller installer)
            {
                fileSystem.FileExists(installer.ProfileLocation).Returns(true);
            }    
        }
        
        [Test]
        [TestCaseSource(nameof(GetShellCompletionInstallers))]
        public async Task ShouldEnsureProfileDirectoryExists(ShellCompletionInstaller installer)
        {
            SetupMockNoProfileFile(installer);

            await installAutoCompleteCommand.Execute(new[] {$"--shell={installer.SupportedShell.ToString()}"});
            fileSystem.Received()
                .EnsureDirectoryExists(Path.GetDirectoryName(installer.ProfileLocation));
            
            void SetupMockNoProfileFile(ShellCompletionInstaller installer)
            {
                fileSystem.FileExists(installer.ProfileLocation).Returns(false);
            }
        }

        [Test]
        [TestCaseSource(nameof(GetShellCompletionInstallers))]
        public async Task ShouldNotWriteToDiskAndWriteToConsoleIfDryRun(ShellCompletionInstaller installer)
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
