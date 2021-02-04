using System;
using Octopus.Client.Logging;
using Serilog;
using Serilog.Context;
using Serilog.Events;

namespace Octopus.Cli.Util
{
    public class CliSerilogLogProvider : ILogProvider
    {
        public CliSerilogLogProvider(ILogger logger)
        {
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public ILogger Logger { get; set; }

        public static bool PrintMessages { get; set; }

        public Logger GetLogger(string name)
        {
            return new SerilogLogger(Logger.ForContext("SourceContext", name, false)).Log;
        }

        public IDisposable OpenNestedContext(string message)
        {
            return LogContext.PushProperty("Context", message);
        }

        public IDisposable OpenMappedContext(string key, string value)
        {
            return LogContext.PushProperty(key, value);
        }

        internal class SerilogLogger
        {
            readonly ILogger logger;

            public SerilogLogger(ILogger logger)
            {
                this.logger = logger;
            }

            public bool Log(LogLevel logLevel, Func<string> messageFunc, Exception exception, params object[] formatParameters)
            {
                if (!PrintMessages)
                    return false;

                var translatedLevel = TranslateLevel(logLevel);
                if (messageFunc == null)
                    return logger.IsEnabled(translatedLevel);

                if (!logger.IsEnabled(translatedLevel))
                    return false;

                if (exception != null)
                    LogException(translatedLevel, messageFunc, exception, formatParameters);
                else
                    LogMessage(translatedLevel, messageFunc, formatParameters);

                return true;
            }

            void LogMessage(LogEventLevel logLevel, Func<string> messageFunc, object[] formatParameters)
            {
                logger.Write(logLevel, messageFunc(), formatParameters);
            }

            void LogException(LogEventLevel logLevel, Func<string> messageFunc, Exception exception, object[] formatParams)
            {
                logger.Write(logLevel, exception, messageFunc(), formatParams);
            }

            static LogEventLevel TranslateLevel(LogLevel logLevel)
            {
                switch (logLevel)
                {
                    case LogLevel.Fatal:
                        return LogEventLevel.Fatal;
                    case LogLevel.Error:
                        return LogEventLevel.Error;
                    case LogLevel.Warn:
                        return LogEventLevel.Warning;
                    case LogLevel.Info:
                        return LogEventLevel.Information;
                    case LogLevel.Trace:
                        return LogEventLevel.Verbose;
                    default:
                        return LogEventLevel.Debug;
                }
            }
        }
    }
}
