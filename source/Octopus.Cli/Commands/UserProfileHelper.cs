using System;
using System.IO;
using System.Runtime.InteropServices;
using Octopus.Cli.Infrastructure;
using Octopus.Cli.Util;

namespace Octopus.Cli.Commands
{
    public static class UserProfileHelper
    {

        private static string LinuxHomeLocation => System.Environment.GetEnvironmentVariable("HOME");
        private static string PowershellProfileFilename => "Microsoft.PowerShell_profile.ps1";
        private static string LinuxPwshConfigLocation => Path.Combine(LinuxHomeLocation, ".config", "powershell");
        private static string WindowsPwshConfigLocation => Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments),
                "Powershell"
            );
        private static string WindowsPowershellConfigLocation => Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments),
            "WindowsPowershell"
        );

        // ZSH
        public static readonly string ZshProfile = $"{LinuxHomeLocation}/.zshrc";

        public const string ZshProfileScript = @"_octo_zsh_complete()
{
    local completions=(""$(octo complete $words)"")
    reply=( ""${(ps:\n:)completions}"" )
}
compctl -K _octo_zsh_complete octo
compctl -K _octo_zsh_complete Octo";

        // POWERSHELL & PWSH
        public static readonly string PowershellProfile =
            Path.Combine(WindowsPowershellConfigLocation, PowershellProfileFilename);
        
        public static readonly string PwshProfile =
            ExecutionEnvironment.IsRunningOnWindows
                ? Path.Combine(WindowsPwshConfigLocation, PowershellProfileFilename)
                : Path.Combine(LinuxPwshConfigLocation, PowershellProfileFilename);
        public const string PwshProfileScript =
            @"
Register-ArgumentCompleter -Native -CommandName octo -ScriptBlock {
    param($wordToComplete, $commandAst, $cursorPosition)
    $parms = $commandAst.ToString() -replace 'octo ',''
    octo complete $parms.Split(' ') | % {
        [System.Management.Automation.CompletionResult]::new($_, $_, 'ParameterName', $_)
    }
}";
        
        // BASH
        public static readonly string BashProfile = $"{LinuxHomeLocation}/.bashrc";
        public const string BashProfileScript = 
            @"_octo_bash_complete()
{
    local params=${COMP_WORDS[@]:1}
    local completions=""$(octo complete ${params})""
    COMPREPLY=( $(compgen -W ""$completions"") )
}
complete -F _octo_bash_complete octo
complete -F _octo_bash_complete Octo";

        public const string AllShellsPrefix = "# start: octo CLI Autocomplete script";
        public const string AllShellsSuffix = "# end: octo CLI Autocomplete script";
    }
}