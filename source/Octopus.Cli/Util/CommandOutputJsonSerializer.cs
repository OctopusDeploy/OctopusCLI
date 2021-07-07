using System;
using Octopus.Client.Serialization;
using Octopus.CommandLine;

namespace Octopus.Cli.Util
{
    public class CommandOutputJsonSerializer : ICommandOutputJsonSerializer
    {
        public string SerializeObjectToJson(object o)
        {
            return JsonSerialization.SerializeObject(o);
        }
    }
}
