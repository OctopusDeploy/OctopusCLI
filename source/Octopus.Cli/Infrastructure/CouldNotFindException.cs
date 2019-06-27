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

        public CouldNotFindException(string resourceTypeDisplayName, string nameOrId,
            string enclosingContextDescription) : base(
            $"Cannot find the {resourceTypeDisplayName} with name or id '{nameOrId}'{enclosingContextDescription}. "
            + $"Please check the spelling and that the account has sufficient access to that {resourceTypeDisplayName}. Please use Configuration > Test Permissions to confirm.")
        {
        }
    }
}