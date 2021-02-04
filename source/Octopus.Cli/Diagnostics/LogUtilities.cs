using System;
using System.Collections.Generic;
using System.Linq;
using Octopus.Cli.Infrastructure;
using Serilog.Core;
using Serilog.Events;

namespace Octopus.Cli.Diagnostics
{
    static class LogUtilities
    {
        static readonly Dictionary<string, LogEventLevel> Lookup;

        static LogUtilities()
        {
            LevelSwitch = new LoggingLevelSwitch(DefaultLogLevel);
            Lookup = ((LogEventLevel[])Enum.GetValues(typeof(LogEventLevel)))
                .ToDictionary(key => key.ToString().ToLowerInvariant(), value => value);
        }

        public static LoggingLevelSwitch LevelSwitch { get; }
        public static LogEventLevel DefaultLogLevel => LogEventLevel.Debug;

        public static LogEventLevel ParseLogLevel(string value)
        {
            if (Lookup.TryGetValue(value, out var level))
                return level;

            throw new CommandException($"Unrecognized loglevel '{value}'. Valid options are {GetValidOptions()}. " +
                $"Defaults to '{DefaultLogLevel.ToString().ToLowerInvariant()}'.");
        }

        public static void AddLogLevelOptions(this OptionSet options)
        {
            var description = $"[Optional] The log level. Valid options are {GetValidOptions()}. " +
                $"Defaults to '{DefaultLogLevel.ToString().ToLowerInvariant()}'.";
            options.Add<LogEventLevel>("logLevel=", description, s => LevelSwitch.MinimumLevel = s);
        }

        static string GetValidOptions()
        {
            var values = Lookup.Where(kv => kv.Key.Length > 1).OrderBy(x => x.Value).Select(x => x.Key).ToArray();
            return $"{string.Join(", ", values.Take(values.Length - 1))} and {values.Last()}";
        }
    }
}
