using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Octopus.Cli.Infrastructure;
using Octopus.Cli.Util;

namespace Octopus.Cli.Commands
{
    public enum SupportedShell
    {
        Unspecified,
        Pwsh,
        Zsh,
        Bash,
        Powershell
    }
    
    [Command(name: "install-autocomplete", Description = "Install a shell auto-complete script into your shell profile, if they aren't already there. Supports pwsh, zsh, bash & powershell.")]
    public class InstallAutoCompleteCommand : CommandBase, ICommand
    {
        private readonly IOctopusFileSystem fileSystem;

        public InstallAutoCompleteCommand(ICommandOutputProvider commandOutputProvider, IOctopusFileSystem fileSystem) : base(commandOutputProvider)
        {
            this.fileSystem = fileSystem;
            var supportedShells = 
                Enum.GetNames(typeof(SupportedShell))
                    .Except(new [] {SupportedShell.Unspecified.ToString()})
                    .ReadableJoin();
            var options = Options.For("Install AutoComplete");
            options.Add("shell=",
                $"The type of shell to install auto-complete scripts for. This will alter your shell configuration files. Supported shells are {supportedShells}.",
                v =>
                {
                    ShellSelection = Enum.TryParse(v, ignoreCase: true, out SupportedShell type)
                        ? type
                        : throw new CommandException(
                            $"Unable to install autocomplete scripts into the {v} shell. Supported shells are {supportedShells}.");
                });
            options.Add("dryRun",
                "[Optional] Dry run will output the proposed changes to console, instead of writing to disk.",
                v => DryRun = true);
        }

        public bool DryRun { get; set; }

        public SupportedShell ShellSelection { get; set; }

        public Task Execute(string[] commandLineArguments)
        {
            Options.Parse(commandLineArguments);
            if (ShellSelection == SupportedShell.Unspecified) throw new CommandException("Please specify the type of shell to install autocompletion for: --shell=XYZ");

            
            commandOutputProvider.PrintHeader();
            if (DryRun) commandOutputProvider.Warning("DRY RUN");
            commandOutputProvider.Information($"Installing auto-complete scripts for {ShellSelection}");
            return Task.Run(() =>
            {
                switch (ShellSelection)
                {
                    case SupportedShell.Pwsh:
                        InstallForPwsh();
                        break;
                    case SupportedShell.Zsh:
                        InstallForZsh();
                        break;
                    case SupportedShell.Bash:
                        InstallForBash();
                        break;
                    case SupportedShell.Powershell:
                        InstallForPowershell();
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(ShellSelection));
                }
                
            });
        }

        private void Install(string profilePath, string scriptToInject)
        {
            commandOutputProvider.Information($"Installing scripts in {profilePath}");
            var tempOutput = new StringBuilder();
            if (fileSystem.FileExists(profilePath))
            {
                var profileText = fileSystem.ReadAllText(profilePath);
                if (!DryRun)
                {
                    if (profileText.Contains(UserProfileHelper.AllShellsPrefix) || profileText.Contains(UserProfileHelper.AllShellsSuffix) || profileText.Contains(scriptToInject))
                    {
                        commandOutputProvider.Information("Looks like command line completion is already installed. Nothing to do.");
                        return;
                    }
                    
                    var backupPath = profilePath + ".orig";
                    commandOutputProvider.Information($"Backing up the existing profile to {backupPath}");
                    fileSystem.CopyFile(profilePath, backupPath);
                }

                commandOutputProvider.Information($"Updating profile at {profilePath}");
                tempOutput.AppendLine(profileText);
            }
            else
            {
                commandOutputProvider.Information($"Creating profile at {profilePath}");
                fileSystem.EnsureDirectoryExists(Path.GetDirectoryName(profilePath));
            }

            tempOutput.AppendLine(UserProfileHelper.AllShellsPrefix);
            tempOutput.AppendLine(scriptToInject);
            tempOutput.AppendLine(UserProfileHelper.AllShellsSuffix);

            if (DryRun)
            {
                commandOutputProvider.Warning("Preview of script changes: ");
                commandOutputProvider.Information(tempOutput.ToString());
                commandOutputProvider.Warning("Preview of script changes finished. ");
            }
            else
            {
                fileSystem.OverwriteFile(profilePath, tempOutput.ToString());
                commandOutputProvider.Warning("All Done! Please reload your shell or dot source your profile to get started! Use the <tab> key to autocomplete.");
            }
        }

        private void InstallForBash()
        {
            Install(
                UserProfileHelper.BashProfile, 
                UserProfileHelper.BashProfileScript);
        }

        private void InstallForZsh()
        {
            Install(
                UserProfileHelper.ZshProfile, 
                UserProfileHelper.ZshProfileScript);
        }

        private void InstallForPwsh()
        {
            var profilePath = UserProfileHelper.PwshProfile;
            Install(profilePath, UserProfileHelper.PwshProfileScript);
        }

        private void InstallForPowershell()
        {
            if (ExecutionEnvironment.IsRunningOnWindows)
            {
                Install(
                    UserProfileHelper.PowershellProfile,
                    UserProfileHelper.PwshProfileScript);
            }
            else
            {
                throw new NotSupportedException("Unable to install for powershell on non-windows platforms. Please use --shell=pwsh instead.");
            }
        }
    }
}
