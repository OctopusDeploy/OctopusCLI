using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NSubstitute;
using NUnit.Framework;
using Octopus.Cli.Commands.Deployment;
using Octopus.Cli.Repositories;
using Octopus.Cli.Tests.Helpers;
using Octopus.Cli.Util;
using Octopus.Client;
using Octopus.Client.Model;
using Octopus.CommandLine;
using Serilog;

namespace Octo.Tests.Commands
{
    public abstract class ApiCommandFixtureBase
    {
        protected const string ValidEnvironment = "Test Environment";
        static string _previousCurrentDirectory;

        public StringBuilder LogOutput { get; set; }
        public string[] LogLines => LogOutput.ToString().Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        public IOctopusClientFactory ClientFactory { get; set; }

        public ILogger Log { get; set; }

        public ILogger FormattedOutputLogger { get; set; }

        public IOctopusAsyncRepositoryFactory RepositoryFactory { get; set; }

        public IOctopusAsyncRepository Repository { get; set; }

        public IOctopusFileSystem FileSystem { get; set; }

        public ICommandOutputProvider CommandOutputProvider { get; set; }

        public ExecutionResourceWaiter.Factory ExecutionResourceWaiterFactory => (repository, serverBaseUrl) => new ExecutionResourceWaiter(Substitute.For<ICommandOutputProvider>(), repository, serverBaseUrl);

        public List<string> CommandLineArgs { get; set; }

        [OneTimeSetUp]
        public static void OneTimeSetUp()
        {
            _previousCurrentDirectory = Directory.GetCurrentDirectory();
            Directory.SetCurrentDirectory(AppContext.BaseDirectory);
        }

        [OneTimeTearDown]
        public static void OneTimeTearDown()
        {
            Directory.SetCurrentDirectory(_previousCurrentDirectory);
        }

        [SetUp]
        public void BaseSetup()
        {
            LogOutput = new StringBuilder();

            Log = new LoggerConfiguration()
                .WriteTo.TextWriter(new StringWriter(LogOutput), outputTemplate: "{Message}{NewLine}{Exception}", formatProvider: new StringFormatter(null))
                .CreateLogger();

            var consoleWriter = new ConsoleWriter();
            consoleWriter.WriteEvent += (sender, args) => LogOutput.Append(args.Value);
            consoleWriter.WriteLineEvent += (sender, args) => LogOutput.AppendLine(args.Value);
            Console.SetOut(consoleWriter);

            var rootDocument = Substitute.For<RootResource>();
            rootDocument.ApiVersion = "2.0";
            rootDocument.Version = "2.0";
            rootDocument.Links.Add("Tenants", "http://tenants.org");

            Repository = Substitute.For<IOctopusAsyncRepository>();
            Repository.LoadRootDocument().Returns(rootDocument);
            Repository.HasLink("Spaces").Returns(true);
            Repository.HasLink(Arg.Is<string>(arg => arg != "Spaces")).Returns(async call => (await Repository.LoadRootDocument()).HasLink(call.Arg<string>()));
            Repository.Link(Arg.Any<string>()).Returns(async call => (await Repository.LoadRootDocument()).Link(call.Arg<string>()));

            Repository.Machines.FindByNames(Arg.Any<IEnumerable<string>>(), Arg.Any<string>(), Arg.Any<object>())
                .Returns(new List<MachineResource>());

            Repository.Environments.FindByName(ValidEnvironment)
                .Returns(new EnvironmentResource { Name = ValidEnvironment });

            ClientFactory = Substitute.For<IOctopusClientFactory>();

            RepositoryFactory = Substitute.For<IOctopusAsyncRepositoryFactory>();
            RepositoryFactory.CreateRepository(null).ReturnsForAnyArgs(Repository);

            FileSystem = Substitute.For<IOctopusFileSystem>();

            CommandOutputProvider = new CommandOutputProvider("Octopus Deploy Command Line Tool", "1.0.0", new CommandOutputJsonSerializer(), Log);

            CommandLineArgs = new List<string>
            {
                "--server=http://the-server",
                "--apiKey=ABCDEF123456789"
            };
        }

        class StringFormatter : IFormatProvider
        {
            readonly IFormatProvider basedOn;

            public StringFormatter(IFormatProvider basedOn)
            {
                this.basedOn = basedOn;
            }

            public object GetFormat(Type formatType)
            {
                if (formatType == typeof(string))
                    return "s";
                return basedOn;
            }
        }
    }
}
