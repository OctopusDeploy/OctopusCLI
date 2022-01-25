using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using Octopus.Client.AutomationEnvironments;
using Octopus.Client.Model;
using Octopus.CommandLine;

namespace Octopus.Cli.Util
{
    public static class CommandOutputProviderExtensionMethods
    {
        static readonly Dictionary<string, string> Escapes;
        static bool serviceMessagesEnabled;
        static AutomationEnvironment buildEnvironment;
        internal static AutomationEnvironmentProvider automationEnvironmentProvider = new AutomationEnvironmentProvider();

        static CommandOutputProviderExtensionMethods()
        {
            serviceMessagesEnabled = false;
            buildEnvironment = AutomationEnvironment.NoneOrUnknown;

            // As per: http://confluence.jetbrains.com/display/TCD65/Build+Script+Interaction+with+TeamCity#BuildScriptInteractionwithTeamCity-ServiceMessages
            Escapes = new Dictionary<string, string>
            {
                { "|", "||" },
                { "'", "|'" },
                { "\n", "|n" },
                { "\r", "|r" },
                { "\u0085", "|x" },
                { "\u2028", "|l" },
                { "\u2029", "|p" },
                { "[", "|[" },
                { "]", "|]" }
            };
        }

        public static bool IsKnownEnvironment()
        {
            return buildEnvironment != AutomationEnvironment.NoneOrUnknown;
        }

        public static void EnableServiceMessages(this ICommandOutputProvider commandOutputProvider)
        {
            serviceMessagesEnabled = true;

            buildEnvironment = automationEnvironmentProvider.DetermineAutomationEnvironment();
        }

        public static void DisableServiceMessages(this ICommandOutputProvider commandOutputProvider)
        {
            serviceMessagesEnabled = false;
        }

        public static bool ServiceMessagesEnabled(this ICommandOutputProvider commandOutputProvider)
        {
            return serviceMessagesEnabled;
        }

        public static bool IsVSTS(this ICommandOutputProvider commandOutputProvider)
        {
            return buildEnvironment == AutomationEnvironment.AzureDevOps;
        }

        public static void ServiceMessage(this ICommandOutputProvider commandOutputProvider, string messageName, string value)
        {
            if (!serviceMessagesEnabled)
                return;

            if (buildEnvironment == AutomationEnvironment.TeamCity)
                commandOutputProvider.Information("##teamcity[{MessageName:l} {Value:l}]", messageName, EscapeValue(value));
            else
                commandOutputProvider.Information("{MessageName:l} {Value:l}", messageName, EscapeValue(value));
        }

        public static void ServiceMessage(this ICommandOutputProvider commandOutputProvider, string messageName, IDictionary<string, string> values)
        {
            if (!serviceMessagesEnabled)
                return;

            var valueSummary = string.Join(" ", values.Select(v => $"{v.Key}='{EscapeValue(v.Value)}'"));
            if (buildEnvironment == AutomationEnvironment.TeamCity)
                commandOutputProvider.Information("##teamcity[{MessageName:l} {ValueSummary:l}]", messageName, valueSummary);
            else
                commandOutputProvider.Information("{MessageName:l} {ValueSummary:l}", messageName, valueSummary);
        }

        public static void ServiceMessage(this ICommandOutputProvider commandOutputProvider, string messageName, object values)
        {
            if (!serviceMessagesEnabled)
                return;

            if (values is string)
            {
                ServiceMessage(commandOutputProvider, messageName, values.ToString());
            }
            else
            {
                var properties = TypeDescriptor.GetProperties(values).Cast<PropertyDescriptor>();
                var valueDictionary = properties.ToDictionary(p => p.Name, p => (string)p.GetValue(values));
                ServiceMessage(commandOutputProvider, messageName, valueDictionary);
            }
        }

        public static void TfsServiceMessage(this ICommandOutputProvider commandOutputProvider, string serverBaseUrl, ProjectResource project, ReleaseResource release)
        {
            if (!serviceMessagesEnabled)
                return;
            if (buildEnvironment == AutomationEnvironment.AzureDevOps || buildEnvironment == AutomationEnvironment.NoneOrUnknown)
            {
#if HAS_APP_CONTEXT
                var workingDirectory = Environment.GetEnvironmentVariable("SYSTEM_DEFAULTWORKINGDIRECTORY") ?? AppContext.BaseDirectory;
#else
                var workingDirectory = Environment.GetEnvironmentVariable("SYSTEM_DEFAULTWORKINGDIRECTORY") ?? AppDomain.CurrentDomain.BaseDirectory;
#endif
                var selflink = new Uri(new Uri(serverBaseUrl), release.Links["Web"].AsString());
                var markdown = $"[Release {release.Version} created for '{project.Name}']({selflink})";
                var markdownFile = Path.Combine(workingDirectory, Guid.NewGuid() + ".md");

                try
                {
                    File.WriteAllText(markdownFile, markdown);
                }
                catch (UnauthorizedAccessException uae)
                {
                    throw new UnauthorizedAccessException($"Could not write the TFS service message file '{markdownFile}'. Please make sure the SYSTEM_DEFAULTWORKINGDIRECTORY environment variable is set to a writeable directory. If this command is not being run on a build agent, omit the --enableServiceMessages parameter.", uae);
                }

                commandOutputProvider.Information("##vso[task.addattachment type=Distributedtask.Core.Summary;name=Octopus Deploy;]{MarkdownFile:l}", markdownFile);
            }
        }

        static string EscapeValue(string value)
        {
            if (value == null)
                return string.Empty;

            return Escapes.Aggregate(value, (current, escape) => current.Replace(escape.Key, escape.Value));
        }
    }
}
