using System.Collections.Generic;
using System.Linq;

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
            string enclosingContextDescription = "") : base(
            $"Cannot find the {resourceTypeDisplayName} with name or id '{nameOrId}'{enclosingContextDescription}. "
            + $"Please check the spelling and that you have permissions to view it. Please use Configuration > Test Permissions to confirm.")
        {
        }

        public CouldNotFindException(string resourceTypeDisplayName, ICollection<string> missingNamesOrIds,
            string enclosingContextDescription = "") : base(
            $"The {resourceTypeDisplayName}{(missingNamesOrIds.Count == 1 ? "" : "s")} {string.Join(", ", missingNamesOrIds.Select(m => "'" + m + "'"))} "
            + $"do{(missingNamesOrIds.Count == 1 ? "es" : "")} not exist{enclosingContextDescription} or you do not have permissions to view {(missingNamesOrIds.Count == 1 ? "it" : "them")}.")
        {
        }
    }
}