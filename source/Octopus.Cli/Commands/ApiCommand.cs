using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Octopus.Cli.Diagnostics;
using Octopus.Cli.Infrastructure;
using Octopus.Cli.Model;
using Octopus.Cli.Repositories;
using Octopus.Cli.Util;
using Octopus.Client;
using Octopus.Client.Model;
using System.Diagnostics;
using Serilog;
using Serilog.Events;

namespace Octopus.Cli.Commands
{
    public abstract class ApiCommand : CommandBase
    {
        /// <summary>
        /// The environment variable that can hold the Octopus Server
        /// </summary>
        public const string ServerUrlEnvVar = "OCTOPUS_CLI_SERVER";
        /// <summary>
        /// The environment variable that can hold the API key
        /// </summary>
        public const string ApiKeyEnvVar = "OCTOPUS_CLI_API_KEY";
        /// <summary>
        /// The environment variable that can hold the username
        /// </summary>
        public const string UsernameEnvVar = "OCTOPUS_CLI_USERNAME";
        /// <summary>
        /// The environment variable that can hold the password
        /// </summary>
        public const string PasswordEnvVar = "OCTOPUS_CLI_PASSWORD";
        readonly IOctopusClientFactory clientFactory;
        readonly IOctopusAsyncRepositoryFactory repositoryFactory;
        string apiKey;
        string serverBaseUrl;
        bool enableDebugging;
        bool ignoreSslErrors;

        string password;
        string username;
        readonly OctopusClientOptions clientOptions = new OctopusClientOptions();
        string spaceNameOrId;
#if NETFRAMEWORK
        int keepAlive;
#endif

        protected ApiCommand(IOctopusClientFactory clientFactory, IOctopusAsyncRepositoryFactory repositoryFactory, IOctopusFileSystem fileSystem, ICommandOutputProvider commandOutputProvider) : base(commandOutputProvider)
        {
            this.clientFactory = clientFactory;
            this.repositoryFactory = repositoryFactory;
            this.FileSystem = fileSystem;

            var options = Options.For("Common options");
            options.Add<string>("server=", $"[Optional] The base URL for your Octopus Server, e.g., 'https://octopus.example.com/'. This URL can also be set in the {ServerUrlEnvVar} environment variable.", v => serverBaseUrl = v);
            options.Add<string>("apiKey=", $"[Optional] Your API key. Get this from the user profile page. You must provide an apiKey or username and password. If the guest account is enabled, a key of API-GUEST can be used. This key can also be set in the {ApiKeyEnvVar} environment variable.", v => apiKey = v, sensitive: true);
            options.Add<string>("user=", $"[Optional] Username to use when authenticating with the server. You must provide an apiKey or username and password. This Username can also be set in the {UsernameEnvVar} environment variable.", v => username = v);
            options.Add<string>("pass=", $"[Optional] Password to use when authenticating with the server. This Password can also be set in the {PasswordEnvVar} environment variable.", v => password = v, sensitive: true);

            options.Add<string>("configFile=", "[Optional] Text file of default values, with one 'key = value' per line.", v => ReadAdditionalInputsFromConfigurationFile(v));
            options.Add<bool>("debug", "[Optional] Enable debug logging", v => enableDebugging = true);
            options.Add<bool>("ignoreSslErrors", "[Optional] Set this flag if your Octopus Server uses HTTPS but the certificate is not trusted on this machine. Any certificate errors will be ignored. WARNING: this option may create a security vulnerability.", v => ignoreSslErrors = true);
            options.Add<bool>("enableServiceMessages", "[Optional] Enable TeamCity or Team Foundation Build service messages when logging.", v => commandOutputProvider.EnableServiceMessages());
            options.Add<string>("timeout=", $"[Optional] Timeout in seconds for network operations. Default is {ApiConstants.DefaultClientRequestTimeout/1000}.", v =>
            {
                if (int.TryParse(v, out var parsedInt))
                    clientOptions.Timeout = TimeSpan.FromSeconds(parsedInt);
                else if (TimeSpan.TryParse(v, out var parsedTimeSpan))
                    clientOptions.Timeout = parsedTimeSpan;
                else
                    throw new CommandException($"Unable to parse '{v}' as a timespan or an integer.");
            });
            options.Add<string>("proxy=", $"[Optional] The URL of the proxy to use, e.g., 'https://proxy.example.com'.", v => clientOptions.Proxy = v);
            options.Add<string>("proxyUser=", $"[Optional] The username for the proxy.", v => clientOptions.ProxyUsername = v);
            options.Add<string>("proxyPass=", $"[Optional] The password for the proxy. If both the username and password are omitted and proxyAddress is specified, the default credentials are used.", v => clientOptions.ProxyPassword = v, sensitive: true);
            options.Add<string>("space=", $"[Optional] The name or ID of a space within which this command will be executed. The default space will be used if it is omitted.", v => spaceNameOrId = v);
#if NETFRAMEWORK
            options.Add<int>("keepalive=", "[Optional] How frequently (in seconds) to send a TCP keepalive packet.", input => keepAlive = input * 1000);
#endif
            options.AddLogLevelOptions();
        }

        protected string ServerBaseUrl => string.IsNullOrWhiteSpace(serverBaseUrl)
                    ? System.Environment.GetEnvironmentVariable(ServerUrlEnvVar)
                    : serverBaseUrl;

        string ApiKey => string.IsNullOrWhiteSpace(apiKey)
            ? System.Environment.GetEnvironmentVariable(ApiKeyEnvVar)
            : apiKey;

        string Username => string.IsNullOrWhiteSpace(username)
            ? System.Environment.GetEnvironmentVariable(UsernameEnvVar)
            : username;

        string Password => string.IsNullOrWhiteSpace(password)
            ? System.Environment.GetEnvironmentVariable(PasswordEnvVar)
            : password;

        protected IOctopusAsyncRepository Repository { get; private set; }

        protected OctopusRepositoryCommonQueries RepositoryCommonQueries { get; private set; }

        protected IOctopusFileSystem FileSystem { get; }

        public override async Task Execute(string[] commandLineArguments)
        {
            var remainingArguments = Options.Parse(commandLineArguments);

            if (printHelp)
            {
                this.GetHelp(Console.Out, commandLineArguments);

                return;
            }

            if (remainingArguments.Count > 0)
                throw new CommandException("Unrecognized command arguments: " + string.Join(", ", remainingArguments));

            if (string.IsNullOrWhiteSpace(ServerBaseUrl))
                throw new CommandException("Please specify the Octopus Server URL using --server=http://your-server/. " +
                    $"The Octopus Server URL can also be set in the {ServerUrlEnvVar} environment variable.");

            if (!string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(Username))
                throw new CommandException("Please provide an API Key OR a username and password, not both. " +
                                           "These values may have been passed in as command line arguments, or may have been set in the " +
                                           $"{ApiKeyEnvVar} and {UsernameEnvVar} environment variables.");

            if (string.IsNullOrWhiteSpace(ApiKey) && string.IsNullOrWhiteSpace(Username))
                throw new CommandException("Please specify your API key using --apiKey=ABCDEF123456789 OR a username and password. " +
                                           $"The API key can also be set in the {ApiKeyEnvVar} environment variable, " +
                                           $"while the username and password can be set in the {UsernameEnvVar} and {PasswordEnvVar} " +
                                           "environment variables respectively. Learn more at: https://github.com/OctopusDeploy/Octopus-Tools");

            var endpoint = string.IsNullOrWhiteSpace(ApiKey)
                ? new OctopusServerEndpoint(ServerBaseUrl)
                : new OctopusServerEndpoint(ServerBaseUrl, ApiKey);

#if NETFRAMEWORK
            /*
             * There may be a delay between the completion of a large file upload and when Octopus responds
             * to finish the HTTP connection. This delay can be several minutes. During this time, no traffic is
             * sent, and some networking infrastructure will close the connection. For example, Azure VMs will
             * close idle connections after 4 minutes, and AWS VMs will close them after 350 seconds. The
             * TCP keepalive option will ensure that the connection is not idle at the end of the file upload.
             *
             * This is the bug that explains why this doesn't work with .NET Core:
             * https://github.com/dotnet/corefx/issues/26013
             */
            if (keepAlive > 0)
            {
                ServicePointManager.FindServicePoint(new Uri(ServerBaseUrl)).SetTcpKeepAlive(true, keepAlive, keepAlive);
            }
#endif

#if HTTP_CLIENT_SUPPORTS_SSL_OPTIONS
            clientOptions.IgnoreSslErrors = ignoreSslErrors;
#else
            ServicePointManager.ServerCertificateValidationCallback = ServerCertificateValidationCallback;
#endif

            commandOutputProvider.PrintMessages = OutputFormat == OutputFormat.Default || enableDebugging;
            CliSerilogLogProvider.PrintMessages = commandOutputProvider.PrintMessages;
            commandOutputProvider.PrintHeader();

            var client = await clientFactory.CreateAsyncClient(endpoint, clientOptions).ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(Username))
            {
                await client.Repository.Users.SignIn(Username, Password).ConfigureAwait(false);
            }

            var serverHasSpaces = await client.ForSystem().HasLink("Spaces").ConfigureAwait(false);

            if (!string.IsNullOrEmpty(spaceNameOrId))
            {
                if (!serverHasSpaces)
                {
                    throw new CommandException($"The server {endpoint.OctopusServer} has no spaces. Try invoking {AssemblyExtensions.GetExecutableName()} without specifying the space name as an argument");
                }

                var space = await client.ForSystem().Spaces.FindByNameOrIdOrFail(spaceNameOrId).ConfigureAwait(false);

                Repository = repositoryFactory.CreateRepository(client, RepositoryScope.ForSpace(space));
                commandOutputProvider.Debug("Space name specified, process is now running in the context of space: {space:l}", space.Name);
            }
            else
            {
                Repository = repositoryFactory.CreateRepository(client);

                if (!serverHasSpaces)
                {
                    commandOutputProvider.Debug("Process will run in backwards compatible mode for older versions of Octopus Server");
                }
                else
                {
                    var defaultSpace = await client.ForSystem().Spaces.FindOne(space => space.IsDefault)
                        .ConfigureAwait(false);

                    if (defaultSpace == null)
                    {
                        throw new CommandException("Octopus Server does not have a default space enabled, hence you need to specify the space name as an argument");
                    }

                    commandOutputProvider.Debug("Space name unspecified, process will run in the default space context");
                }
            }

            RepositoryCommonQueries = new OctopusRepositoryCommonQueries(Repository, commandOutputProvider);

            if (enableDebugging)
            {
                Repository.Client.SendingOctopusRequest += request => commandOutputProvider.Debug("{Method:l} {Uri:l}", request.Method, request.Uri);
            }

            commandOutputProvider.Debug("Handshaking with Octopus Server: {Url:l}", ServerBaseUrl);

            var root = await Repository.LoadRootDocument().ConfigureAwait(false);

            commandOutputProvider.Debug("Handshake successful. Octopus version: {Version:l}; API version: {ApiVersion:l}", root.Version, root.ApiVersion);

            var user = await Repository.Users.GetCurrent().ConfigureAwait(false);
            if (user != null)
            {
                if (string.IsNullOrEmpty(user.EmailAddress))
                    commandOutputProvider.Debug("Authenticated as: {Name:l} {IsService:l}", user.DisplayName, user.IsService ? "(a service account)" : "");
                else
                    commandOutputProvider.Debug("Authenticated as: {Name:l} <{EmailAddress:l}> {IsService:l}", user.DisplayName, user.EmailAddress, user.IsService ? "(a service account)" : "");
            }

            await ValidateParameters().ConfigureAwait(false);
            await Execute().ConfigureAwait(false);
        }

        protected virtual Task ValidateParameters() { return Task.WhenAll();}

        protected virtual async Task Execute()
        {
            if (formattedOutputInstance != null)
            {
                await formattedOutputInstance.Request();

                Respond();
            }
            else
            {
                throw new Exception($"Need to override the Execute method or implement the {nameof(ISupportFormattedOutput)} interface");
            }
        }

        private void Respond()
        {
            if (formattedOutputInstance != null)
            {
                if (OutputFormat == OutputFormat.Json)
                {
                    formattedOutputInstance.PrintJsonOutput();
                }
                else
                {
                    formattedOutputInstance.PrintDefaultOutput();
                }
            }
        }

        protected List<string> ReadAdditionalInputsFromConfigurationFile(string configFile)
        {
            configFile = FileSystem.GetFullPath(configFile);

            commandOutputProvider.Debug("Loading additional arguments from config file: {ConfigFile:l}", configFile);

            if (!FileSystem.FileExists(configFile))
            {
                throw new CommandException("Unable to find config file " + configFile);
            }

            var results = new List<string>();
            using (var fileStream = FileSystem.OpenFile(configFile, FileAccess.Read))
            using (var file = new StreamReader(fileStream))
            {
                string line;
                while ((line = file.ReadLine()) != null)
                {
                    if (!string.IsNullOrWhiteSpace(line) && !line.Trim().StartsWith("#"))
                    {
                        results.Add("--" + line.Trim());
                    }
                }
            }

            var remainingArguments = Options.Parse(results);
            if (remainingArguments.Count > 0)
                throw new CommandException("Unrecognized arguments in configuration file: " + string.Join(", ", remainingArguments));

            return results;
        }

        protected static IEnumerable<string> FormatReleasePropertiesAsStrings(ReleaseResource release)
        {
            var releaseProperties = new List<string>
            {
                "Version: " + release.Version,
                "Assembled: " + release.Assembled,
                "Package Versions: " + GetPackageVersionsAsString(release.SelectedPackages),
                "Release Notes: " + GetReleaseNotes(release)
            };
            if (!string.IsNullOrEmpty(release.VersionControlReference?.GitRef))
            {
                releaseProperties.Add("Git Reference: " + release.VersionControlReference.GitRef);
            }
            if (!string.IsNullOrEmpty(release.VersionControlReference?.GitCommit))
            {
                releaseProperties.Add("Git Commit: " + release.VersionControlReference.GitCommit);
            }

            return releaseProperties;
        }

        protected static string GetReleaseNotes(ReleaseResource release)
        {
            return release.ReleaseNotes != null ? release.ReleaseNotes.Replace(System.Environment.NewLine, @"\n") : "";
        }

        protected static string GetPackageVersionsAsString(IEnumerable<SelectedPackage> packages)
        {
            var packageVersionsAsString = "";

            foreach (var package in packages)
            {
                var packageVersionAsString = package.ActionName + " " + package.Version;

                if (packageVersionsAsString.Contains(packageVersionAsString))
                {
                    continue;
                }
                if (!String.IsNullOrEmpty(packageVersionsAsString))
                {
                    packageVersionsAsString += "; ";
                }
                packageVersionsAsString += packageVersionAsString;
            }
            return packageVersionsAsString;
        }

#if !HTTP_CLIENT_SUPPORTS_SSL_OPTIONS
        private bool ServerCertificateValidationCallback(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors errors)
        {
            if (errors == SslPolicyErrors.None)
                return true;

            var certificate2 = (X509Certificate2)certificate;
            var warning = "The following certificate errors were encountered when establishing the HTTPS connection to the server: " + errors + System.Environment.NewLine +
                             "Certificate subject name: " + certificate2.SubjectName.Name + System.Environment.NewLine +
                             "Certificate thumbprint:   " + ((X509Certificate2)certificate).Thumbprint;

            if (ignoreSslErrors)
            {
                commandOutputProvider.Warning(warning);
                commandOutputProvider.Warning("Because --ignoreSslErrors was set, this will be ignored.");
                return true;
            }

            commandOutputProvider.Error(warning);
            return false;
        }
#endif
    }
}
