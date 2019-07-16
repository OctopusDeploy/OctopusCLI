using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Octopus.Cli.Infrastructure;
using Octopus.Cli.Model;
using Octopus.Cli.Repositories;
using Octopus.Cli.Util;
using Octopus.Client;
using Octopus.Client.Model;

namespace Octopus.Cli.Commands.Package
{
    [Command("push", Description = "Pushes a package (.nupkg, .zip, .tar.gz, etc.) package to the built-in NuGet repository in an Octopus Server.")]
    public class PushCommand : ApiCommand, ISupportFormattedOutput
    {
        private List<string> pushedPackages;
        private List<Tuple<string, Exception>> failedPackages;

        public PushCommand(IOctopusAsyncRepositoryFactory repositoryFactory, IOctopusFileSystem fileSystem, IOctopusClientFactory clientFactory, ICommandOutputProvider commandOutputProvider)
            : base(clientFactory, repositoryFactory, fileSystem, commandOutputProvider)
        {
            var options = Options.For("Package pushing");
            options.Add("package=", "Package file to push. Specify multiple packages by specifying this argument multiple times: \n--package package1 --package package2", 
                package => Packages.Add(EnsurePackageExists(fileSystem, package)));
            options.Add("overwrite-mode=", "If the package already exists in the repository, the default behavior is to reject the new package being pushed (FailIfExists). You can use the overwrite mode to OverwriteExisting or IgnoreIfExists.", mode => OverwriteMode = (OverwriteMode)Enum.Parse(typeof(OverwriteMode), mode, true));
            options.Add("replace-existing", "If the package already exists in the repository, the default behavior is to reject the new package being pushed. You can pass this flag to overwrite the existing package. This flag may be deprecated in a future version; passing it is the same as using the OverwriteExisting overwrite-mode.", 
                replace => OverwriteMode = OverwriteMode.OverwriteExisting);
            options.Add("use-delta-compression=", "Allows disabling of delta compression when uploading packages to the Octopus Server. (True or False. Defaults to true.)",
                v =>
                {
                    if (!bool.TryParse(v, out var desiredValue))
                        throw new CommandException($"The value '{v}' is not valid. Valid values are true or false.");
                    UseDeltaCompression = desiredValue;
                });
            options.Add("keepalive=", "How frequently (in seconds) to send a TCP keepalive packet while transferring files.", input => KeepAlive = int.Parse(input));
            pushedPackages = new List<string>();
            failedPackages = new List<Tuple<string, Exception>>();
        }

        public HashSet<string> Packages { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase); 
        public OverwriteMode OverwriteMode { get; set; }
        public bool UseDeltaCompression { get; set; } = true;

        public int KeepAlive { get; set; }
 
        public async Task Request()
        {
            if (Packages.Count == 0) throw new CommandException("Please specify a package to push");

            /*
             * There may be a delay between the completion of a large file upload and when Octopus responds
             * to finish the HTTP connection. This delay can be several minutes. During this time, no traffic is
             * sent, and some networking infrastructure will close the connection. For example, Azure VMs will
             * close idle connections after 4 minutes, and AWS VMs will close them after 350 seconds. The
             * TCP keepalive option will ensure that the connection is not idle at the end of the file upload.
             */
            if (KeepAlive > 0)
            {
                ServicePointManager.SetTcpKeepAlive(true, KeepAlive, KeepAlive);
            }

            foreach (var package in Packages)
            {
                commandOutputProvider.Debug("Pushing package: {Package:l}...", package);

                try
                {
                    using (var fileStream = FileSystem.OpenFile(package, FileAccess.Read))
                    {
                        await Repository.BuiltInPackageRepository
                            .PushPackage(Path.GetFileName(package), fileStream, OverwriteMode, UseDeltaCompression)
                            .ConfigureAwait(false);
                    }

                    pushedPackages.Add(package);
                }
                catch (Exception ex)
                {
                    if (OutputFormat == OutputFormat.Default)
                    {
                        throw;
                    }
                    failedPackages.Add(new Tuple<string, Exception>(package, ex));
                }
            }
        }

        public void PrintDefaultOutput()
        {
            commandOutputProvider.Debug("Push successful");
        }

        public void PrintJsonOutput()
        {
            commandOutputProvider.Json(new
            {
                SuccessfulPackages = pushedPackages,
                FailedPackages = failedPackages.Select(x=>new { Package = x.Item1, Reason = x.Item2.Message.Replace("\r\n", string.Empty) })
            });
        }

        static string EnsurePackageExists(IOctopusFileSystem fileSystem, string package)
        {
            var path = fileSystem.GetFullPath(package);
            if (!File.Exists(path)) throw new CommandException("Package file not found: " + path);
            return path;
        }
    }
}
