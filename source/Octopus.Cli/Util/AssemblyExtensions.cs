using System;
using System.Reflection;

namespace Octopus.Cli.Util
{
    public static class AssemblyExtensions
    {
        public static string GetInformationalVersion(this Type type)
        {
            return type.GetTypeInfo().Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;
        }

        public static string GetExecutableName()
        {
            return Assembly.GetEntryAssembly()?.GetName().Name ?? "octo";
        }
    }
}
