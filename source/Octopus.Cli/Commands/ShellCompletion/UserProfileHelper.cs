using System;
using System.IO;
using System.Text;
using Octopus.Cli.Infrastructure;
using Octopus.Cli.Util;

namespace Octopus.Cli.Commands.ShellCompletion
{
    public abstract class ShellCompletionInstaller
    {
        private readonly ICommandOutputProvider commandOutputProvider;
        private readonly IOctopusFileSystem fileSystem;
        public static string HomeLocation => System.Environment.GetEnvironmentVariable("HOME");
        public abstract string ProfileLocation { get; }
        public abstract string ProfileScript { get; }
        public abstract SupportedShell SupportedShell { get; }

        public ShellCompletionInstaller(ICommandOutputProvider commandOutputProvider, IOctopusFileSystem fileSystem)
        {
            this.commandOutputProvider = commandOutputProvider;
            this.fileSystem = fileSystem;
        }

        public virtual void Install(bool dryRun)
        {
            commandOutputProvider.Information($"Installing scripts in {ProfileLocation}");
            var tempOutput = new StringBuilder();
            if (fileSystem.FileExists(ProfileLocation))
            {
                var profileText = fileSystem.ReadAllText(ProfileLocation);
                if (!dryRun)
                {
                    if (profileText.Contains(AllShellsPrefix) || profileText.Contains(AllShellsSuffix) || profileText.Contains(ProfileScript))
                    {
                        commandOutputProvider.Information("Looks like command line completion is already installed. Nothing to do.");
                        return;
                    }
                    
                    var backupPath = ProfileLocation + ".orig";
                    commandOutputProvider.Information($"Backing up the existing profile to {backupPath}");
                    fileSystem.CopyFile(ProfileLocation, backupPath);
                }

                commandOutputProvider.Information($"Updating profile at {ProfileLocation}");
                tempOutput.AppendLine(profileText);
            }
            else
            {
                commandOutputProvider.Information($"Creating profile at {ProfileLocation}");
                fileSystem.EnsureDirectoryExists(Path.GetDirectoryName(ProfileLocation));
            }

            tempOutput.AppendLine(AllShellsPrefix);
            tempOutput.AppendLine(ProfileScript);
            tempOutput.AppendLine(AllShellsSuffix);

            if (dryRun)
            {
                commandOutputProvider.Warning("Preview of script changes: ");
                commandOutputProvider.Information(tempOutput.ToString());
                commandOutputProvider.Warning("Preview of script changes finished. ");
            }
            else
            {
                fileSystem.OverwriteFile(ProfileLocation, tempOutput.ToString());
                commandOutputProvider.Warning("All Done! Please reload your shell or dot source your profile to get started! Use the <tab> key to autocomplete.");
            }
        }
        
        public const string AllShellsPrefix = "# start: Octopus CLI (octo) Autocomplete script";
        public const string AllShellsSuffix = "# end: Octopus CLI (octo) Autocomplete script";
    }

    public class ZshCompletionInstaller : ShellCompletionInstaller
    {
        public override SupportedShell SupportedShell => SupportedShell.Zsh;
        public override string ProfileLocation => $"{HomeLocation}/.zshrc";
        public override string ProfileScript => 
@"_octo_zsh_complete()
{
    local completions=(""$(octo complete $words)"")
    reply=( ""${(ps:\n:)completions}"" )
}
compctl -K _octo_zsh_complete octo
compctl -K _octo_zsh_complete Octo";
        public ZshCompletionInstaller(ICommandOutputProvider commandOutputProvider, IOctopusFileSystem fileSystem) : base(commandOutputProvider, fileSystem) { }
    }

    public class BashCompletionInstaller : ShellCompletionInstaller
    {
        public override SupportedShell SupportedShell => SupportedShell.Bash;
        public override string ProfileLocation => $"{HomeLocation}/.bashrc";
        public override string ProfileScript => 
@"_octo_bash_complete()
{
    local params=${COMP_WORDS[@]:1}
    local completions=""$(octo complete ${params})""
    COMPREPLY=( $(compgen -W ""$completions"") )
}
complete -F _octo_bash_complete octo
complete -F _octo_bash_complete Octo";
        public BashCompletionInstaller(ICommandOutputProvider commandOutputProvider, IOctopusFileSystem fileSystem) : base(commandOutputProvider, fileSystem) { }
    }

    public abstract class PowershellCompletionInstallerBase : ShellCompletionInstaller
    {
        protected static string PowershellProfileFilename => "Microsoft.PowerShell_profile.ps1";
        public override string ProfileScript =>
@"Register-ArgumentCompleter -Native -CommandName octo -ScriptBlock {
    param($wordToComplete, $commandAst, $cursorPosition)
    $parms = $commandAst.ToString().Split(' ') | select -skip 1
    octo complete $parms | % {
        [System.Management.Automation.CompletionResult]::new($_, $_, 'ParameterName', $_)
    }
}";
        public PowershellCompletionInstallerBase(ICommandOutputProvider commandOutputProvider, IOctopusFileSystem fileSystem) : base(commandOutputProvider, fileSystem) { }
    }

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

    public class PowershellCompletionInstaller : PowershellCompletionInstallerBase
    {
        public override SupportedShell SupportedShell => SupportedShell.Powershell;
        private static string WindowsPowershellConfigLocation => Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments),
            "WindowsPowershell"
        );
        public override string ProfileLocation => Path.Combine(WindowsPowershellConfigLocation, PowershellProfileFilename);
        public PowershellCompletionInstaller(ICommandOutputProvider commandOutputProvider, IOctopusFileSystem fileSystem) : base(commandOutputProvider, fileSystem) { }

        public override void Install(bool dryRun)
        {
            if (ExecutionEnvironment.IsRunningOnNix || ExecutionEnvironment.IsRunningOnMac || ExecutionEnvironment.IsRunningOnMono) 
                throw new CommandException("Unable to install for powershell on non-windows platforms. Please use --shell=pwsh instead.");
            base.Install(dryRun);
        }
    }
}
