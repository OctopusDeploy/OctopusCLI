using Octopus.Cli.Util;

namespace Octopus.Cli.Commands.ShellCompletion
{
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
complete -F _octo_bash_complete Octo".NormalizeNewLinesForNix();
        public BashCompletionInstaller(ICommandOutputProvider commandOutputProvider, IOctopusFileSystem fileSystem) : base(commandOutputProvider, fileSystem) { }
    }
}