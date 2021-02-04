using System;
using System.Collections.Generic;
using System.Linq;

namespace Octopus.Cli.Importers
{
    abstract class BaseValidatedImportSettings
    {
        protected BaseValidatedImportSettings()
        {
            ErrorList = new List<string>();
        }

        public IEnumerable<string> ErrorList { get; set; }
        public bool HasErrors => ErrorList.Any();
    }
}
