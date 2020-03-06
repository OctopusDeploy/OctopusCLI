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

            var options = Options.For("Install AutoComplete");
            options.Add("shell=",
                "The type of shell to install auto-complete scripts for. This will alter your shell configuration files. Supported shells: zsh, bash, pwsh and powershell.",
                v => ShellSelection = Enum.TryParse(v, ignoreCase: true, out SupportedShell type) 
                    ? type 
                    : throw new InvalidCastException($"Unable to install autocomplete scripts into the {v} shell. Supported shells are zsh, bash, pwsh and powershell."));
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
            commandOutputProvider.Information($"Install auto-complete scripts for {ShellSelection}");
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
                        commandOutputProvider.Information("Looks like this is already installed. Bailing out.");
                        return;
                    }
                    
                    var backupPath = profilePath + ".orig";
                    commandOutputProvider.Warning($"Backing up to {backupPath}");
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
#if NETFRAMEWORK
            Install(
                UserProfileHelper.PowershellProfileLocation,
                UserProfileHelper.PwshProfileScript);
#else
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Install(
                    UserProfileHelper.PowershellProfile,
                    UserProfileHelper.PwshProfileScript);
            }
            else
            {
                throw new NotSupportedException("Unable to install for powershell on non-windows platforms. Please use --shell=pwsh instead.");
            }
#endif
        }
    }
    public static class UserProfileHelper
    {
#if NETFRAMEWORK
        public static readonly string EnvHome = "HOMEPATH";
#else
        public static readonly string EnvHome = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "HOMEPATH" : "HOME";
#endif
        public static readonly string HomeDirectory = System.Environment.GetEnvironmentVariable(EnvHome);
        public static readonly string ZshProfile = $"{HomeDirectory}/.zshrc";
        public const string ZshProfileScript = 
@"_octo_zsh_complete()
{
    local completions=(""$(octo complete $words)"")
    reply=( ""${(ps:\n:)completions}"" )
}
compctl -K _octo_zsh_complete octo";

        public static readonly string PowershellProfile = 
            $"{System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments)}{Path.DirectorySeparatorChar}" +
            $"WindowsPowerShell{Path.DirectorySeparatorChar}Microsoft.PowerShell_profile.ps1";
        
        public static readonly string PwshProfile =  
            $"{HomeDirectory}{Path.DirectorySeparatorChar}.config{Path.DirectorySeparatorChar}powershell" +
            $"{Path.DirectorySeparatorChar}Microsoft.PowerShell_profile.ps1";
        
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
    }
}
