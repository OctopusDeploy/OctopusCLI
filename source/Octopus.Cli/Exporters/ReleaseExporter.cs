﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Octopus.Cli.Extensions;
using Octopus.Cli.Infrastructure;
using Octopus.Cli.Util;
using Octopus.Client;
using Octopus.Client.Model;
using Octopus.CommandLine.Commands;
using Octopus.Versioning.Octopus;
using Serilog;

namespace Octopus.Cli.Exporters
{
    [Exporter("release", "List", Description = "Exports either a single release, or multiple releases.")]
    public class ReleaseExporter : BaseExporter
    {
        static readonly OctopusVersionParser OctopusVersionParser = new OctopusVersionParser();

        public ReleaseExporter(IOctopusAsyncRepository repository, IOctopusFileSystem fileSystem, ILogger log) :
            base(repository, fileSystem, log)
        {
        }

        protected override async Task Export(Dictionary<string, string> paramDictionary)
        {
            if (string.IsNullOrWhiteSpace(paramDictionary["Project"])) throw new CommandException("Please specify the project name using the parameter: --project=XYZ");
            if (string.IsNullOrWhiteSpace(paramDictionary["ReleaseVersion"])) throw new CommandException("Please specify the release, or range of releases using the parameter: --releaseVersion=1.0.0 for a single release, or --releaseVersion=1.0.0-1.0.3 for a range of releases");
            var projectName = paramDictionary["Project"];
            var releaseVersion = paramDictionary["ReleaseVersion"];

            Log.Debug("Finding project: {Project:l}", projectName);
            var project = await Repository.Projects.FindByName(projectName).ConfigureAwait(false);
            if (project == null)
                throw new CouldNotFindException("a project named", projectName);

            OctopusVersion minVersionToExport;
            OctopusVersion maxVersionToExport;

            // I don't think -> works on the command line unless it is quoted --releaseVersion="1.0.0->1.0.1"
            if (releaseVersion.IndexOf("->", StringComparison.Ordinal) > 0)
            {
                var releaseVersions = releaseVersion.Split(new[] { "->" }, StringSplitOptions.RemoveEmptyEntries);
                if (releaseVersions.Count() > 2)
                    throw new CommandException("Incorrect format for exporting multiple releases, please specify the release versions as --releaseVersion=1.0.0-1.0.3");
                minVersionToExport = OctopusVersionParser.Parse(releaseVersions[0]);
                maxVersionToExport = OctopusVersionParser.Parse(releaseVersions[1]);
            }
            else if (releaseVersion.IndexOf("-", StringComparison.Ordinal) > 0)
            {
                var releaseVersions = releaseVersion.Split(new[] { '-' }, StringSplitOptions.RemoveEmptyEntries);
                if (releaseVersions.Count() > 2)
                    throw new CommandException("Incorrect format for exporting multiple releases, please specify the release versions as --releaseVersion=1.0.0-1.0.3");

                minVersionToExport = OctopusVersionParser.Parse(releaseVersions[0]);
                if (!OctopusVersionParser.TryParse(releaseVersions[1], out maxVersionToExport))
                {
                    minVersionToExport = OctopusVersionParser.Parse(releaseVersion);
                    maxVersionToExport = minVersionToExport;
                }
            }
            else
            {
                minVersionToExport = OctopusVersionParser.Parse(releaseVersion);
                maxVersionToExport = minVersionToExport;
            }

            Log.Debug("Finding releases for project...");
            var releasesToExport = new List<ReleaseResource>();
            var releases = await Repository.Projects.GetReleases(project).ConfigureAwait(false);
            await releases.Paginate(Repository,
                    page =>
                    {
                        foreach (var release in page.Items)
                        {
                            var version = OctopusVersionParser.Parse(release.Version);
                            if (minVersionToExport.CompareTo(version) <= 0 && version.CompareTo(maxVersionToExport) <= 0)
                            {
                                Log.Debug("Found release {Version:l}", version);
                                releasesToExport.Add(release);

                                if (minVersionToExport == maxVersionToExport)
                                    break;
                            }
                        }

                        // Stop paging if the range is a single version, or if there is only a single release worth exporting after this page
                        return minVersionToExport != maxVersionToExport || releasesToExport.Count != 1;
                    })
                .ConfigureAwait(false);

            var metadata = new ExportMetadata
            {
                ExportedAt = DateTime.Now,
                OctopusVersion = (await Repository.LoadRootDocument().ConfigureAwait(false)).Version,
                Type = typeof(ReleaseExporter).GetAttributeValue((ExporterAttribute ea) => ea.Name),
                ContainerType = typeof(ReleaseExporter).GetAttributeValue((ExporterAttribute ea) => ea.EntityType)
            };
            FileSystemExporter.Export(FilePath, metadata, releasesToExport);
        }
    }
}
