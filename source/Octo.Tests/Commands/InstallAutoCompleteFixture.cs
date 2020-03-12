using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using NSubstitute;
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
            SetupMockExistingProfileFile();

            await installAutoCompleteCommand.Execute(new[] {$"--shell={installer.SupportedShell.ToString()}"});
        
            fileSystem.Received()
                .CopyFile(installer.ProfileLocation, installer.ProfileLocation + ".orig");
            
            void SetupMockExistingProfileFile()
            {
                fileSystem.FileExists(installer.ProfileLocation).Returns(true);
            }    
        }
        
        [Test]
        [TestCaseSource(nameof(GetShellCompletionInstallers))]
        public async Task ShouldEnsureProfileDirectoryExists(ShellCompletionInstaller installer)
        {
            SetupMockNoProfileFile();

            await installAutoCompleteCommand.Execute(new[] {$"--shell={installer.SupportedShell.ToString()}"});
            fileSystem.Received()
                .EnsureDirectoryExists(Path.GetDirectoryName(installer.ProfileLocation));
            
            void SetupMockNoProfileFile()
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

        [Test]
        public async Task PwshCompletionInstaller_ShouldUseCorrectNewlinesForPlatform()
        {
            var pwshInstaller = new PwshCompletionInstaller(commandOutputProvider, fileSystem);
            await installAutoCompleteCommand.Execute(new[] {$"--shell={pwshInstaller.SupportedShell.ToString()}", "--dryRun"});
            if (ExecutionEnvironment.IsRunningOnWindows)
            {
                commandOutputProvider.Received().Information(Arg.Is<string>(arg => arg.Contains(pwshInstaller.ProfileScript.NormalizeNewLinesForWindows())));
            }

            if (ExecutionEnvironment.IsRunningOnMac || ExecutionEnvironment.IsRunningOnNix)
            {
                commandOutputProvider.Received().Information(Arg.Is<string>(arg => arg.Contains(pwshInstaller.ProfileScript.NormalizeNewLinesForNix())));
            }
        }

        [Test]
        public async Task PowershellCompletionInstaller_ShouldUseWindowsLineEndings()
        {
            if (ExecutionEnvironment.IsRunningOnWindows)
            {
                var powershellInstaller = new PowershellCompletionInstaller(commandOutputProvider, fileSystem);
                await installAutoCompleteCommand.Execute(new[]
                    {$"--shell={powershellInstaller.SupportedShell.ToString()}", "--dryRun"});
                commandOutputProvider.Received().Information(Arg.Is<string>(arg =>
                    arg.Contains(powershellInstaller.ProfileScript.NormalizeNewLinesForWindows())));
            }
            else
            {
                Assert.Inconclusive("This test doesn't run on non-windows environments.");
            }
        }
        
        [Test]
        public async Task BashCompletionInstaller_ShouldUseNixLineEndings()
        {
            if (ExecutionEnvironment.IsRunningOnMac || ExecutionEnvironment.IsRunningOnNix)
            {
                var bashInstaller = new BashCompletionInstaller(commandOutputProvider, fileSystem);
                await installAutoCompleteCommand.Execute(new[]
                    {$"--shell={bashInstaller.SupportedShell.ToString()}", "--dryRun"});
                commandOutputProvider.Received().Information(Arg.Is<string>(arg =>
                    arg.Contains(bashInstaller.ProfileScript.NormalizeNewLinesForNix())));
            }
            else
            {
                Assert.Inconclusive("This test doesn't run on windows environments.");
            }
        }
        
        [Test]
        public async Task ZshCompletionInstaller_ShouldUseNixLineEndings()
        {
            if (ExecutionEnvironment.IsRunningOnMac || ExecutionEnvironment.IsRunningOnNix)
            {
                var zshInstaller = new ZshCompletionInstaller(commandOutputProvider, fileSystem);
                await installAutoCompleteCommand.Execute(new[]
                    {$"--shell={zshInstaller.SupportedShell.ToString()}", "--dryRun"});
                commandOutputProvider.Received().Information(Arg.Is<string>(arg =>
                    arg.Contains(zshInstaller.ProfileScript.NormalizeNewLinesForNix())));
            }
            else
            {
                Assert.Inconclusive("This test doesn't run on windows environments.");
            }
        }
    }
}
