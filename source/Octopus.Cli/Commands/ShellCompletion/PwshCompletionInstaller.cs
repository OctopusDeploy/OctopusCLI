using System;
using System.IO;
using Octopus.Cli.Util;

namespace Octopus.Cli.Commands.ShellCompletion
{
    public class PwshCompletionInstaller : PowershellCompletionInstallerBase
    {
        public PwshCompletionInstaller(ICommandOutputProvider commandOutputProvider, IOctopusFileSystem fileSystem) : base(commandOutputProvider, fileSystem)
        {
        }

        public override SupportedShell SupportedShell => SupportedShell.Pwsh;
        string LinuxPwshConfigLocation => Path.Combine(HomeLocation, ".config", "powershell");

        static string WindowsPwshConfigLocation => Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments),
            "Powershell"
        );

        public override string ProfileLocation => ExecutionEnvironment.IsRunningOnWindows
            ? Path.Combine(WindowsPwshConfigLocation, PowershellProfileFilename)
            : Path.Combine(LinuxPwshConfigLocation, PowershellProfileFilename);

        public override string ProfileScript => base.ProfileScript.NormalizeNewLines();
    }
}
