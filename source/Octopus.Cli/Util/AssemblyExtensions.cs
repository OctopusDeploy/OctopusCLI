using System;
using System.IO;
using System.Reflection;

namespace Octopus.Cli.Util
{
    public static class AssemblyExtensions
    {
        public static string FullLocalPath(this Assembly assembly)
        {
#if NETFRAMEWORK
            //SYSLIB0012: 'Assembly.CodeBase' is obsolete: 'Assembly.CodeBase and Assembly.EscapedCodeBase are
            ////only included for .NET Framework compatibility. Use Assembly.Location instead.'
            
            var codeBase = assembly.CodeBase;
            var uri = new UriBuilder(codeBase);
            var root = Uri.UnescapeDataString(uri.Path);
            root = root.Replace('/', Path.DirectorySeparatorChar);
            return root;
#else
            return assembly.Location;
#endif
            
        }

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
