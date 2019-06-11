namespace Octopus.Cli.Infrastructure
{
    public class CouldNotFindException : CommandException
    {
        public CouldNotFindException(string what)
            : base("Could not find " + what + "; either it does not exist or you lack permissions to view it.")
        {
        }

        public CouldNotFindException(string what, string quotedName)
            : this(what + " '" + quotedName + "'")
        {
        }

        public CouldNotFindException(string typeDescription, string nameOrId, string inDescription) : base(
            $"Cannot find the {typeDescription} with name or id '{nameOrId}'{inDescription}. "
            + $"Please check the spelling and that the account has sufficient access to that {typeDescription}. Please use Configuration > Test Permissions to confirm.")
        {
        }
    }
}