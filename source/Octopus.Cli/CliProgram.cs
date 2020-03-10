﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using Autofac;
using Octopus.Cli.Commands.Releases;
using Octopus.Cli.Commands.ShellCompletion;
using Octopus.Cli.Diagnostics;
using Octopus.Cli.Exporters;
using Octopus.Cli.Importers;
using Octopus.Cli.Infrastructure;
using Octopus.Cli.Repositories;
using Octopus.Cli.Util;
using Octopus.Client;
using Octopus.Client.Exceptions;
using Serilog;
using AssemblyExtensions = Octopus.Cli.Util.AssemblyExtensions;

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
                var first = GetFirstArgument(args);
                var command = GetCommand(first, commandLocator);
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
            
            Client.Logging.LogProvider.SetCurrentLogProvider(new CliSerilogLogProvider(Log.Logger));
        }

        static IContainer BuildContainer()
        {
            var builder = new ContainerBuilder();
            var thisAssembly = typeof (CliProgram).GetTypeInfo().Assembly;

            builder.RegisterModule(new LoggingModule());

            builder.RegisterAssemblyTypes(thisAssembly).As<ICommand>().AsSelf();
            builder.RegisterType<CommandLocator>().As<ICommandLocator>();

            builder.RegisterType<CommandOutputProvider>().As<ICommandOutputProvider>().SingleInstance();

            builder.RegisterAssemblyTypes(thisAssembly).As<IExporter>().AsSelf();
            builder.RegisterAssemblyTypes(thisAssembly).As<IImporter>().AsSelf();
            builder.RegisterType<ExporterLocator>().As<IExporterLocator>();
            builder.RegisterType<ImporterLocator>().As<IImporterLocator>();

            builder.RegisterType<ReleasePlanBuilder>().As<IReleasePlanBuilder>().SingleInstance();
            builder.RegisterType<PackageVersionResolver>().As<IPackageVersionResolver>().SingleInstance();
            builder.RegisterType<ChannelVersionRuleTester>().As<IChannelVersionRuleTester>().SingleInstance();

            builder.RegisterType<OctopusClientFactory>().As<IOctopusClientFactory>();
            builder.RegisterType<OctopusRepositoryFactory>().As<IOctopusAsyncRepositoryFactory>();

            builder.RegisterType<OctopusPhysicalFileSystem>().As<IOctopusFileSystem>();
            builder.RegisterAssemblyTypes(thisAssembly)
                .Where(t => t.IsSubclassOf(typeof(ShellCompletionInstaller)))
                .As<ShellCompletionInstaller>()
                .AsSelf();
            
            return builder.Build();
        }

        static ICommand GetCommand(string first, ICommandLocator commandLocator)
        {
            if (string.IsNullOrWhiteSpace(first))
            {
                return commandLocator.Find("help");
            }

            var command = commandLocator.Find(first);
            if (command == null)
                throw new CommandException("Error: Unrecognized command '" + first + "'");

            return command;
        }

        static string GetFirstArgument(IEnumerable<string> args)
        {
            return (args.FirstOrDefault() ?? string.Empty).ToLowerInvariant().TrimStart('-', '/');
        }

        static int PrintError(Exception ex)
        {
            var agg = ex as AggregateException;
            if (agg != null)
            {
                var errors = new HashSet<Exception>(agg.InnerExceptions);
                if (agg.InnerException != null)
                    errors.Add(ex.InnerException);

                var lastExit = 0;
                foreach (var inner in errors)
                {
                    lastExit = PrintError(inner);
                }

                return lastExit;
            }

            var securityException = ex as OctopusSecurityException;
            if (securityException != null) 
            {
                if (!string.IsNullOrWhiteSpace(securityException.HelpText))
                {
                    Log.Error(securityException.HelpText);
                }

                Log.Error(securityException.Message);
                return -5;
            }

            var cmd = ex as CommandException;
            if (cmd != null)
            {
                Log.Error(ex.Message);
                if (LogExtensions.IsKnownEnvironment())
                {
                    Log.Error($"This error is most likely occurring while executing {AssemblyExtensions.GetExecutableName()} as part of an automated build process. The following doc is recommended to get some tips on how to troubleshoot this: https://g.octopushq.com/OctoexeTroubleshooting");
                }
                return -1;
            }
            var reflex = ex as ReflectionTypeLoadException;
            if (reflex != null)
            {
                Log.Error(ex, string.Empty);

                foreach (var loaderException in reflex.LoaderExceptions)
                {
                    Log.Error(loaderException, string.Empty);
                }

                return -43;
            }

            var octo = ex as OctopusException;
            if (octo != null)
            {
                Log.Information("{HttpErrorMessage:l}", octo.Message);
                Log.Error("Error from Octopus Server (HTTP {StatusCode} {StatusDescription})", octo.HttpStatusCode, (HttpStatusCode) octo.HttpStatusCode);
                return -7;
            }

            Log.Error(ex, string.Empty);
            return -3;
        }
    }
}