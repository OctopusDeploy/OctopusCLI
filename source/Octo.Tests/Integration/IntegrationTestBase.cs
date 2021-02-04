using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Nancy;
using Nancy.Owin;
using NUnit.Framework;
using Octopus.Cli;
using Serilog;

namespace Octo.Tests.Integration
{
    public abstract class IntegrationTestBase : NancyModule
    {
        public static readonly string HostBaseUri = "http://localhost:18362";
        static IWebHost _currentHost;

        protected IntegrationTestBase()
        {
            TestRootPath = $"/{GetType().Name}";

            Get($"{TestRootPath}/api", p => LoadResponseFile("api"));
        }

        protected string TestRootPath { get; }

        [OneTimeSetUp]
        public static void OneTimeSetup()
        {
            _currentHost = new WebHostBuilder()
                .UseContentRoot(Directory.GetCurrentDirectory())
                .UseKestrel()
                .UseStartup<Startup>()
                .UseUrls(HostBaseUri)
                .Build();
            Task.Run(() =>
            {
                try
                {
                    _currentHost.Run();
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(ex);
                }
            });
            var applicationLifetime = (IApplicationLifetime)_currentHost.Services.GetService(typeof(IApplicationLifetime));
            applicationLifetime.ApplicationStarted.WaitHandle.WaitOne();
        }

        [OneTimeTearDown]
        public static void OneTimeTearDown()
        {
            _currentHost?.Dispose();
        }

        internal ExecuteResult Execute(string command, params string[] args)
        {
            var logOutput = new StringBuilder();
            Log.Logger = new LoggerConfiguration()
                .WriteTo.ColoredConsole(outputTemplate: "{Message}{NewLine}{Exception}")
                .WriteTo.TextWriter(new StringWriter(logOutput), outputTemplate: "{Message}{NewLine}{Exception}")
                .CreateLogger();

            var allArgs = new[]
                {
                    command,
                    $"--server={HostBaseUri}{TestRootPath}",
                    "--apiKey=ABCDEF123456789"
                }.Concat(args)
                .ToArray();

            var code = new CliProgram().Run(allArgs);
            return new ExecuteResult(code, logOutput.ToString());
        }

        protected Response LoadResponseFile(string path)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = $"{typeof(IntegrationTestBase).Namespace}.Responses.{path.Replace("/", ".")}.json";
            string content;
            using (var s = assembly.GetManifestResourceStream(resourceName))
            using (var sr = new StreamReader(s))
            {
                content = sr.ReadToEnd();
            }

            return Response.AsText(content.Replace("{TestRootPath}", TestRootPath), "application/json");
        }

        internal class ExecuteResult
        {
            public ExecuteResult(int code, string logOutput)
            {
                Code = code;
                LogOutput = logOutput;
            }

            public int Code { get; }
            public string LogOutput { get; }
        }

        public class Startup
        {
            public void Configure(IApplicationBuilder app)
            {
                app.UseOwin(x => x.UseNancy());
            }
        }
    }
}
