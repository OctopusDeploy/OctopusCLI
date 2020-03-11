using Octopus.Cli.Util;

namespace Octopus.Cli.Commands.ShellCompletion
{
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
compctl -K _octo_zsh_complete Octo".NormalizeNewLinesForNix();
        public ZshCompletionInstaller(ICommandOutputProvider commandOutputProvider, IOctopusFileSystem fileSystem) : base(commandOutputProvider, fileSystem) { }
    }
}