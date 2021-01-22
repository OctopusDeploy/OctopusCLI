using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using Octopus.Cli.Infrastructure;
using Octopus.Cli.Util;
using Octopus.Versioning.Octopus;
using Serilog;

namespace Octopus.Cli.Commands.Releases
{
    /// <summary>
    /// Represents the package that a version applies to
    /// </summary>
    class PackageKey : IEqualityComparer<PackageKey>
    {
        public string StepNameOrPackageId { get; }
        public string PackageReferenceName { get; }

        public PackageKey()
        {

        }

        public PackageKey(string stepNameOrPackageId)
        {
            StepNameOrPackageId = stepNameOrPackageId;
        }

        public PackageKey(string stepNameOrPackageId, string packageReferenceName)
        {
            StepNameOrPackageId = stepNameOrPackageId;
            PackageReferenceName = packageReferenceName;
        }

        public bool Equals(PackageKey x, PackageKey y)
        {
            return Object.Equals(x, y);
        }

        public int GetHashCode(PackageKey obj)
        {
            return obj.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            var key = obj as PackageKey;
            return key != null &&
                   string.Equals(StepNameOrPackageId, key.StepNameOrPackageId, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(PackageReferenceName, key.PackageReferenceName, StringComparison.OrdinalIgnoreCase);
        }

        public override int GetHashCode()
        {
            var hashCode = 475932885;
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(StepNameOrPackageId?.ToLower());
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(PackageReferenceName?.ToLower());
            return hashCode;
        }
    }

    public class PackageVersionResolver : IPackageVersionResolver
    {
        private static readonly OctopusVersionParser OctopusVersionParser = new OctopusVersionParser();

        /// <summary>
        /// Used to indicate a match with any matching step name or package reference name
        /// </summary>
        private const string WildCard = "*";
        /// <summary>
        /// The characters we support for breaking up step, package name and version in the supplied strings
        /// </summary>
        private static readonly char[] Delimiters = new [] {':', '=', '/'};

        static readonly string[] SupportedZipFilePatterns = { "*.zip", "*.tgz", "*.tar.gz", "*.tar.Z", "*.tar.bz2", "*.tar.bz", "*.tbz", "*.tar" };

        readonly ILogger log;
        private readonly IOctopusFileSystem fileSystem;
        readonly IDictionary<PackageKey, string> stepNameToVersion = new Dictionary<PackageKey, string>(new PackageKey());
        string defaultVersion;

        public PackageVersionResolver(ILogger log, IOctopusFileSystem fileSystem)
        {
            this.log = log;
            this.fileSystem = fileSystem;
        }

        public void AddFolder(string folderPath)
        {
            log.Debug("Using package versions from folder: {FolderPath:l}", folderPath);
            foreach (var file in fileSystem.EnumerateFilesRecursively(folderPath, "*.nupkg"))
            {
                log.Debug("Package file: {File:l}", file);

                if (TryReadPackageIdentity(file, out var packageIdentity))
                {
                    Add(packageIdentity.Id, null, packageIdentity.Version.ToString());
                }
            }
            foreach (var file in fileSystem.EnumerateFilesRecursively(folderPath, SupportedZipFilePatterns))
            {
                log.Debug("Package file: {File:l}", file);

                if (TryParseZipIdAndVersion(file, out var packageIdentity))
                {
                    Add(packageIdentity.Id, null, packageIdentity.Version.ToString());
                }
            }
        }

        public void Add(string stepNameOrPackageIdAndVersion)
        {
            var split = stepNameOrPackageIdAndVersion.Split(Delimiters);
            if (split.Length < 2)
                throw new CommandException("The package argument '" + stepNameOrPackageIdAndVersion + "' does not use expected format of : {Step Name}:{Version}");

            var stepNameOrPackageId = split[0];
            var packageReferenceName = split.Length > 2 ? split[1] : WildCard;
            var version = split.Length > 2 ? split[2] : split[1];

            if (string.IsNullOrWhiteSpace(stepNameOrPackageId) || string.IsNullOrWhiteSpace(version))
            {
                throw new CommandException("The package argument '" + stepNameOrPackageIdAndVersion + "' does not use expected format of : {Step Name}:{Version}");
            }

            Add(stepNameOrPackageId, packageReferenceName, version);
        }

        public void Add(string stepNameOrPackageId, string packageVersion)
        {
            Add(stepNameOrPackageId, string.Empty, packageVersion);
        }

        public void Add(string stepNameOrPackageId, string packageReferenceName, string packageVersion)
        {
            // Double wild card == default value
            if (stepNameOrPackageId == WildCard && packageReferenceName == WildCard)
            {
                Default(packageVersion);
                return;
            }

            var key = new PackageKey(stepNameOrPackageId, packageReferenceName ?? WildCard);
            if (stepNameToVersion.TryGetValue(key, out var current))
            {
                var newVersion = OctopusVersionParser.Parse(packageVersion);
                var currentVersion = OctopusVersionParser.Parse(current);
                if (newVersion.CompareTo(currentVersion) < 0)
                {
                    return;
                }
            }

            stepNameToVersion[key] = packageVersion;
        }

        public void Default(string packageVersion)
        {
            try
            {
                OctopusVersionParser.Parse(packageVersion);
                defaultVersion = packageVersion;
            }
            catch (Exception)
            {
                if (packageVersion.Contains(":"))
                {
                    throw new ArgumentException("Invalid package version format. Use the package parameter if you need to specify the step name and version.");
                }
                throw;
            }
        }

        public string ResolveVersion(string stepName, string packageId)
        {
            return ResolveVersion(stepName, packageId, string.Empty);
        }

        public string ResolveVersion(string stepName, string packageId, string packageReferenceName)
        {
            var identifiers = new[] {stepName, packageId};

            // First attempt to get an exact match between step or package id and the package reference name
            return identifiers
                    .Select(id => new PackageKey(id, packageReferenceName ?? string.Empty))
                    .Select(key => stepNameToVersion.TryGetValue(key, out var version) ? version : null)
                    .FirstOrDefault(version => version != null)
                ??
                // If that fails, try to match on a wildcard step/package id and exact package reference name,
                // and then on an exact step/package id and wildcard package reference name
                identifiers
                    .SelectMany(id => new[]
                        {new PackageKey(WildCard, packageReferenceName ?? string.Empty), new PackageKey(id, WildCard)})
                    .Select(key => stepNameToVersion.TryGetValue(key, out var version) ? version : null)
                    .FirstOrDefault(version => version != null)
                ??
                // Finally, use the default version
                defaultVersion;
        }

        bool TryReadPackageIdentity(string packageFile, out PackageIdentity packageIdentity)
        {
            packageIdentity = null;
            try
            {
                using (var reader = new PackageArchiveReader(new FileStream(packageFile, FileMode.Open, FileAccess.Read)))
                {
                    var nuspecReader = new NuspecReader(reader.GetNuspec());
                    packageIdentity = nuspecReader.GetIdentity();
                    return true;
                }
            }
            catch (Exception ex)
            {
               log.Warning(ex, "Could not read manifest from '{PackageFile:l}'", packageFile);
            }

            return false;
        }

        /// <summary>
        /// Takes a string containing a concatenated package ID and version (e.g. a filename or database-key) and
        /// attempts to parse a package ID and semantic version.
        /// </summary>
        /// <param name="filename">The filename of the package</param>
        /// <param name="packageIdentity">The package identity</param>
        /// <returns>True if parsing was successful, else False</returns>
        static bool TryParseZipIdAndVersion(string filename, out PackageIdentity packageIdentity)
        {
            packageIdentity = null;

            var idAndVersion = Path.GetFileNameWithoutExtension(filename) ?? "";
            if (".tar".Equals(Path.GetExtension(idAndVersion), StringComparison.OrdinalIgnoreCase))
                idAndVersion = Path.GetFileNameWithoutExtension(idAndVersion);

            const string packageIdPattern = @"(?<packageId>(\w+([_.-]\w+)*?))";
            const string semanticVersionPattern = @"(?<semanticVersion>(\d+(\.\d+){0,3}" // Major Minor Patch
                 + @"(-[0-9A-Za-z-]+(\.[0-9A-Za-z-]+)*)?)" // Pre-release identifiers
                 + @"(\+[0-9A-Za-z-]+(\.[0-9A-Za-z-]+)*)?)"; // Build Metadata

            var match = Regex.Match(idAndVersion, $@"^{packageIdPattern}\.{semanticVersionPattern}$");
            var packageIdMatch = match.Groups["packageId"];
            var versionMatch = match.Groups["semanticVersion"];

            if (!packageIdMatch.Success || !versionMatch.Success)
                return false;

            var packageId = packageIdMatch.Value;

            NuGetVersion version;
            if (!NuGetVersion.TryParse(versionMatch.Value, out version))
                return false;

            packageIdentity = new PackageIdentity(packageId, version);
            return true;
        }
    }
}
