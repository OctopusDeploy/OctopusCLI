using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using Autofac;
using Octopus.Cli.Commands.Deployment;
using Octopus.Cli.Commands.Releases;
using Octopus.Cli.Diagnostics;
using Octopus.Cli.Repositories;
using Octopus.Cli.Util;
using Octopus.Client;
using Octopus.Client.Exceptions;
using Octopus.Client.Logging;
using Octopus.CommandLine;
using Octopus.CommandLine.Commands;
using Octopus.CommandLine.ShellCompletion;
using Serilog;

namespace Octopus.Cli
{
    public class CliProgram
    {
        public int Execute(string[] args)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls
                | SecurityProtocolType.Tls11
                | SecurityProtocolType.Tls12;

            ConfigureLogger();
            return Run(args);
        }

        internal int Run(string[] args)
        {
            try
            {
                Console.Title = "Octopus Deploy Command Line Tool";
            }
            catch
            {
                // Try best effort to set the title of the console
                // This can fail when there is no window because the window handle will be invalid
            }

            try
            {
                var container = BuildContainer();
                var commandLocator = container.Resolve<ICommandLocator>();
                var command = commandLocator.GetCommand(args);
                command.Execute(args.Skip(1).ToArray()).GetAwaiter().GetResult();
                return 0;
            }
            catch (Exception exception)
            {
                var exit = PrintError(exception);
                Console.WriteLine("Exit code: " + exit);
                return exit;
            }
        }

        public static void ConfigureLogger()
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.ControlledBy(LogUtilities.LevelSwitch)
                .WriteTo.Trace()
                .WriteTo.ColoredConsole(outputTemplate: "{Message}{NewLine}{Exception}")
                .CreateLogger();

            LogProvider.SetCurrentLogProvider(new CliSerilogLogProvider(Log.Logger));
        }

        static IContainer BuildContainer()
        {
            var builder = new ContainerBuilder();
            var thisAssembly = typeof(CliProgram).GetTypeInfo().Assembly;

            builder.RegisterModule(new LoggingModule());

            builder.RegisterAssemblyTypes(thisAssembly).As<ICommand>().AsSelf();
            builder.RegisterType<CommandLocator>().As<ICommandLocator>();
            builder.RegisterAssemblyTypes(typeof(ICommand).Assembly).As<ICommand>().AsSelf();
            builder.RegisterType<CommandOutputJsonSerializer>()
                .As<ICommandOutputJsonSerializer>();

            builder.RegisterType<CommandOutputProvider>()
                .WithParameter("applicationName", "Octopus CLI")
                .WithParameter("applicationVersion", typeof(CliProgram).GetInformationalVersion())
                .As<ICommandOutputProvider>()
                .SingleInstance();

            builder.RegisterType<ReleasePlanBuilder>().As<IReleasePlanBuilder>().SingleInstance();
            builder.RegisterType<PackageVersionResolver>().As<IPackageVersionResolver>().SingleInstance();
            builder.RegisterType<ChannelVersionRuleTester>().As<IChannelVersionRuleTester>().SingleInstance();

            builder.RegisterType<OctopusClientFactory>().As<IOctopusClientFactory>();
            builder.RegisterType<OctopusRepositoryFactory>().As<IOctopusAsyncRepositoryFactory>();
            builder.RegisterType<ExecutionResourceWaiter>().As<IExecutionResourceWaiter>();

            builder.RegisterType<OctopusPhysicalFileSystem>().As<IOctopusFileSystem>();
            builder.RegisterAssemblyTypes(typeof(ICommand).Assembly)
                .WithParameter("executableNames", new[] { "Octo", "octo" })
                .Where(t => t.IsAssignableTo<IShellCompletionInstaller>())
                .AsImplementedInterfaces()
                .AsSelf();

            return builder.Build();
        }

        static int PrintError(Exception ex)
        {
            switch (ex)
            {
                case AggregateException agg:
                {
                    var errors = new HashSet<Exception>(agg.InnerExceptions);
                    if (agg.InnerException != null)
                        errors.Add(ex.InnerException);

                    var lastExit = 0;
                    foreach (var inner in errors)
                        lastExit = PrintError(inner);

                    return lastExit;
                }
                case OctopusSecurityException securityException:
                {
                    if (!string.IsNullOrWhiteSpace(securityException.HelpText))
                        Log.Error(securityException.HelpText);

                    Log.Error(securityException.Message);
                    return -5;
                }
                case CommandException cmd:
                {
                    Log.Error(ex.Message);
                    if (CommandOutputProviderExtensionMethods.IsKnownEnvironment())
                        Log.Error($"This error is most likely occurring while executing {Util.AssemblyExtensions.GetExecutableName()} as part of an automated build process. The following doc is recommended to get some tips on how to troubleshoot this: https://g.octopushq.com/OctoexeTroubleshooting");
                    return -1;
                }
                case ReflectionTypeLoadException reflex:
                {
                    Log.Error(ex, string.Empty);

                    foreach (var loaderException in reflex.LoaderExceptions)
                        Log.Error(loaderException, string.Empty);

                    return -43;
                }
                case UnsupportedApiVersionException unsupported:
                {
                    Log.Error(unsupported.Message);
                    return -1;
                }
                case OctopusException octo:
                {
                    Log.Information("{HttpErrorMessage:l}", octo.Message);
                    Log.Error("Error from Octopus Server (HTTP {StatusCode} {StatusDescription})",
                        octo.HttpStatusCode,
                        (HttpStatusCode)octo.HttpStatusCode);
                    return -7;
                }
            }

            Log.Error(ex, string.Empty);
            return -3;
        }
    }
}
