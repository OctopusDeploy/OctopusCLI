using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Markdig.Extensions.ListExtras;
using Octopus.Cli.Infrastructure;
using Octopus.Cli.Util;
using Serilog.Events;

namespace Octopus.Cli.Commands
{
    public enum SupportedShell
    {
        Pwsh,
        Zsh,
        Bash,
    }
    
    [Command(name: "install-autocomplete", Description = "Install a shell auto-complete script into your shell profile, if they aren't already there. Supports Powershell (pwsh), Z Shell (zsh), Bourne Again Shell (bash) & Friendly Interactive Shell (fish).")]
    public class InstallAutoCompleteCommand : CommandBase, ICommand
    {
        private readonly IOctopusFileSystem fileSystem;
        private readonly IShellCommandExecutor shellCommandExecutor;

        public InstallAutoCompleteCommand(ICommandOutputProvider commandOutputProvider, IOctopusFileSystem fileSystem, IShellCommandExecutor shellCommandExecutor) : base(commandOutputProvider)
        {
            this.fileSystem = fileSystem;
            this.shellCommandExecutor = shellCommandExecutor;

            var options = Options.For("Install AutoComplete");
            options.Add("shell=",
                "The type of shell to install auto-complete scripts for. This will alter your shell configuration files. Supported shells: zsh, bash and pwsh",
                v => ShellSelection = Enum.TryParse(v, ignoreCase: true, out SupportedShell type) 
                    ? type 
                    : throw new InvalidCastException($"Unable to install autocomplete scripts into the {v} shell. Supported shells are zsh, bash and pwsh."));
            options.Add("dryRun",
                "Dry run will output the proposed changes to console, instead of writing to disk.",
                v => DryRun = true);
        }

        public bool DryRun { get; set; }

        public SupportedShell ShellSelection { get; set; }

        public Task Execute(string[] commandLineArguments)
        {
            Options.Parse(commandLineArguments);
            commandOutputProvider.PrintHeader();
            if (DryRun) commandOutputProvider.Warning("DRY RUN");
            commandOutputProvider.Information($"Install auto-complete scripts for {ShellSelection}");
            return Task.Run(() =>
            {
                switch (ShellSelection)
                {
                    case SupportedShell.Pwsh:
                        InstallForPowershell();
                        break;
                    case SupportedShell.Zsh:
                        InstallForZsh();
                        break;
                    case SupportedShell.Bash:
                        InstallForBash();
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(ShellSelection));
                }
                
            });
        }

        private void Install(string profilePath, string scriptToInject, ProcessStartInfo commandOnComplete)
        {
            commandOutputProvider.Information($"Installing scripts in {profilePath}");
            var tempOutput = new StringBuilder();
            if (fileSystem.FileExists(profilePath))
            {
                var profileText = fileSystem.ReadAllText(profilePath);
                if (!DryRun)
                {
                    var backupPath = profilePath + ".orig";
                    commandOutputProvider.Warning($"Backing up to {backupPath}");
                    fileSystem.CopyFile(profilePath, backupPath);
                }

                if (profileText.Contains(UserProfileHelper.AllShellsPrefix) || profileText.Contains(UserProfileHelper.AllShellsSuffix) || profileText.Contains(scriptToInject))
                {
                    commandOutputProvider.Information("Looks like this is already installed. Bailing out.");
                    return;
                }

                commandOutputProvider.Information($"Updating profile at {profilePath}");
                tempOutput.AppendLine(profileText);
            }
            else
            {
                commandOutputProvider.Information($"Creating profile at {profilePath}");
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
                shellCommandExecutor.Execute(commandOnComplete);
                commandOutputProvider.Information("All done, start using autocomplete by using your tab key!");
                commandOutputProvider.Information("");
            }
        }
        
        private void InstallForBash()
        {
            Install(
                UserProfileHelper.BashProfile, 
                UserProfileHelper.BashProfileScript, 
                new ProcessStartInfo("/usr/bin/bash", $"-c \"source {UserProfileHelper.BashProfile}\""));
        }

        private void InstallForZsh()
        {
            Install(
                UserProfileHelper.ZshProfile, 
                UserProfileHelper.ZshProfileScript, 
                new ProcessStartInfo("/usr/bin/zsh", $"-c \"source {UserProfileHelper.ZshProfile}\""));
        }

        private void InstallForPowershell()
        {
            var profilePath = UserProfileHelper.GetPwshProfileForOperatingSystem();
            Install(profilePath, UserProfileHelper.PwshProfileScript, new ProcessStartInfo("powershell", "-c \". $PROFILE\""));
        }
    }
    public static class UserProfileHelper
    {
        public static readonly string EnvHome = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "HOMEPATH" : "HOME";
        public static readonly string HomeDirectory = System.Environment.GetEnvironmentVariable(EnvHome);
        public static readonly string ZshProfile = $"{HomeDirectory}/.zshrc";
        public const string ZshProfileScript = 
@"_octo_zsh_complete()
{
    local completions=(""$(octo complete $words)"")
    reply=( ""${(ps:\n:)completions}"" )
}
compctl -K _octo_zsh_complete octo";

        public static readonly string PwshProfileLocationWindows = $"{HomeDirectory}\\Documents\\WindowsPowerShell\\Microsoft.PowerShell_profile.ps1";
        public static readonly string PwshProfileLocationNix =  $"{HomeDirectory}/.config/powershell/Microsoft.PowerShell_profile.ps1";
        public const string PwshProfileScript =
@"Register-ArgumentCompleter -Native -CommandName octo -ScriptBlock {
    param($wordToComplete, $commandAst, $cursorPosition)
    $parms = ($commandAst.ToString()).Replace('octo ','')
    octo complete $parms.Split(' ') | % {
        [System.Management.Automation.CompletionResult]::new($_, $_, 'ParameterName', $_)
    }
}";

        public static readonly string BashProfile = $"{HomeDirectory}/.bashrc";
        public const string BashProfileScript = 
@"_octo_bash_complete()
{
    local params=${COMP_WORDS[@]:1}
    local completions=""$(octo complete ${params})""
    COMPREPLY=( $(compgen -W ""$completions"") )
}
complete -F _octo_bash_complete octo";

        public const string AllShellsPrefix = "# start: octo CLI Autocomplete script";
        public const string AllShellsSuffix = "# end: octo CLI Autocomplete script";

        public static string GetPwshProfileForOperatingSystem()
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? PwshProfileLocationWindows
                : PwshProfileLocationNix;
        }
    }

    public interface IShellCommandExecutor
    {
        void Execute(ProcessStartInfo processStartInfo);
    }

    public class ShellCommandExecutor : IShellCommandExecutor
    {
        public void Execute(ProcessStartInfo processStartInfo)
        {
            Process.Start(processStartInfo);
        }
    }
}