using System;
using Octopus.Cli.Util;

namespace Octopus.Cli.Commands.ShellCompletion
{
    public abstract class PowershellCompletionInstallerBase : ShellCompletionInstaller
    {
        public PowershellCompletionInstallerBase(ICommandOutputProvider commandOutputProvider, IOctopusFileSystem fileSystem) : base(commandOutputProvider, fileSystem)
        {
        }

        protected static string PowershellProfileFilename => "Microsoft.PowerShell_profile.ps1";

        public override string ProfileScript =>
            @"Register-ArgumentCompleter -Native -CommandName octo -ScriptBlock {
    param($wordToComplete, $commandAst, $cursorPosition)
    $parms = $commandAst.ToString().Split(' ') | select -skip 1
    octo complete $parms | % {
        [System.Management.Automation.CompletionResult]::new($_, $_, 'ParameterName', $_)
    }
}";
    }
}
