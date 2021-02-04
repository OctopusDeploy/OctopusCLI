using System;
using System.Collections.Generic;
using NuGet.Packaging;

namespace Octopus.Cli.Commands.Package
{
    public enum PackageCompressionLevel
    {
        None,
        Fast,
        Optimal
    }

    public enum PackageFormat
    {
        Zip,
        NuPkg,

        [Obsolete("This is just here for backwards compat")]
        Nuget
    }

    public interface IPackageBuilder
    {
        string[] Files { get; }

        string PackageFormat { get; }

        void BuildPackage(string basePath,
            IList<string> includes,
            ManifestMetadata metadata,
            string outFolder,
            bool overwrite,
            bool verboseInfo);

        void SetCompression(PackageCompressionLevel level);
    }
}
