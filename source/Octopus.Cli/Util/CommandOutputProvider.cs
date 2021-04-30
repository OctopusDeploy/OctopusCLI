using System;
using Octopus.Cli.Diagnostics;
using Octopus.Client.Model;
using Octopus.Client.Serialization;
using Octopus.CommandLine;
using Serilog;

namespace Octopus.Cli.Util
{
    public interface IOctopusCliCommandOutputProvider : ICommandOutputProvider
    {
        void EnableServiceMessages();
        bool ServiceMessagesEnabled();
        bool IsVSTS();
        void ServiceMessage(string messageName, object o);
        void TfsServiceMessage(string serverBaseUrl, ProjectResource project, ReleaseResource release);
    }

    public class CommandOutputProvider : CommandOutputProviderBase, IOctopusCliCommandOutputProvider
    {
        readonly ILogger logger;

        public CommandOutputProvider(ILogger logger) : base(logger)
        {
            this.logger = logger;
        }

        protected override string GetAppVersion() => typeof(CliProgram).GetInformationalVersion();

        protected override string SerializeObjectToJason(object o) => JsonSerialization.SerializeObject(o);

        public bool ServiceMessagesEnabled() => logger.ServiceMessagesEnabled();

        public bool IsVSTS() => logger.IsVSTS();

        public void EnableServiceMessages() => logger.EnableServiceMessages();

        public void ServiceMessage(string messageName, object o)
        {
            if (PrintMessages)
                logger.ServiceMessage(messageName, o);
        }

        public void TfsServiceMessage(string serverBaseUrl, ProjectResource project, ReleaseResource release)
        {
            if (PrintMessages)
                logger.TfsServiceMessage(serverBaseUrl, project, release);
        }
    }
}
