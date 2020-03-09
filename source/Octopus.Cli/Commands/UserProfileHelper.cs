using System.IO;
using System.Runtime.InteropServices;

namespace Octopus.Cli.Commands
{
    public static class UserProfileHelper
    {
#if NETFRAMEWORK
        public static readonly string EnvHome = "HOMEPATH";
#else
        public static readonly string EnvHome = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "HOMEPATH" : "HOME";
#endif
        public static readonly string HomeDirectory = System.Environment.GetEnvironmentVariable(EnvHome);

        // ZSH
        public static readonly string ZshProfile = $"{HomeDirectory}/.zshrc";

        public const string ZshProfileScript = @"_octo_zsh_complete()
{
    local completions=(""$(octo complete $words)"")
    reply=( ""${(ps:\n:)completions}"" )
}
compctl -K _octo_zsh_complete octo
compctl -K _octo_zsh_complete Octo";

        // POWERSHELL & PWSH
        public static readonly string PowershellProfile = 
            $"{System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments)}{Path.DirectorySeparatorChar}" +
            $"WindowsPowerShell{Path.DirectorySeparatorChar}Microsoft.PowerShell_profile.ps1";
        
        public static readonly string PwshProfile =  
            $"{HomeDirectory}{Path.DirectorySeparatorChar}.config{Path.DirectorySeparatorChar}powershell" +
            $"{Path.DirectorySeparatorChar}Microsoft.PowerShell_profile.ps1";
        
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
        public static readonly string BashProfile = $"{HomeDirectory}/.bashrc";
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