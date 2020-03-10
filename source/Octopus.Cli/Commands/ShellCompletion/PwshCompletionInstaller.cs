using System.IO;
using Octopus.Cli.Util;

namespace Octopus.Cli.Commands.ShellCompletion
{
    public class PwshCompletionInstaller : PowershellCompletionInstallerBase
    {
        public override SupportedShell SupportedShell => SupportedShell.Pwsh;
        private string LinuxPwshConfigLocation => Path.Combine(HomeLocation, ".config", "powershell");
        private static string WindowsPwshConfigLocation => Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments),
            "Powershell"
        );
        public override string ProfileLocation => ExecutionEnvironment.IsRunningOnWindows
            ? Path.Combine(WindowsPwshConfigLocation, PowershellProfileFilename)
            : Path.Combine(LinuxPwshConfigLocation, PowershellProfileFilename);
        public PwshCompletionInstaller(ICommandOutputProvider commandOutputProvider, IOctopusFileSystem fileSystem) : base(commandOutputProvider, fileSystem) { }
    }
}