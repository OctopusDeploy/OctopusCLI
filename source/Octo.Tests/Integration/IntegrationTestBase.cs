using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Nancy;
using Nancy.Extensions;
using Nancy.IO;
using Nancy.ModelBinding;
using Nancy.Owin;
using Nancy.Responses.Negotiation;
using Newtonsoft.Json;
using NUnit.Framework;
using Octopus.Client.Extensibility;
using Octopus.Client.Model;
using Serilog;
using Octopus.Client.Serialization;

namespace Octopus.Cli.Tests.Integration
{

    public abstract class IntegrationTestBase : NancyModule
    {
        public static readonly string HostBaseUri = "http://localhost:18362";
        private static IWebHost _currentHost;

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


        protected IntegrationTestBase()
        {
            TestRootPath = $"/{GetType().Name}";
            Get($"{TestRootPath}/api", p => Response.AsJson(
                new RootResource()
                {
                    ApiVersion = "3.0.0",
                    Links = new LinkCollection()
                     {
                         { "CurrentUser", TestRootPath + "/api/users/me" },
                         { "Environments", TestRootPath + "/api/environments{/id}{?skip,ids}" },
                         { "PackageUpload", TestRootPath + "/api/packages/raw{?replace}" },
                         { "SpaceHome", TestRootPath + "/api/{spaceId}" }
                     }
                }
            ));
            Get($"{TestRootPath}/api/users/me", p => Response.AsJson(
                new UserResource()
                {
                }
            ));
        }

        protected string TestRootPath { get; }

        internal class ExecuteResult
        {
            public int Code { get; }
            public string LogOutput { get; }

            public ExecuteResult(int code, string logOutput)
            {
                Code = code;
                LogOutput = logOutput;
            }
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
