using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Octopus.Cli.Infrastructure;

namespace Octopus.Cli.Util
{
    public static class StringExtensions
    {
        public static string CommaSeperate(this IEnumerable<object> items)
        {
            return string.Join(", ", items);
        }

        public static string NewlineSeperate(this IEnumerable<object> items)
        {
            return string.Join(Environment.NewLine, items);
        }

        public static string NormalizeNewLinesForWindows(this string originalString)
        {
            return Regex.Replace(originalString, @"\r\n|\n\r|\n|\r", "\r\n");
        }

        public static string NormalizeNewLinesForNix(this string originalString)
        {
            return Regex.Replace(originalString, @"\r\n|\n\r|\n|\r", "\n");
        }

        public static string NormalizeNewLines(this string originalString)
        {
            return Regex.Replace(originalString, @"\r\n|\n\r|\n|\r", Environment.NewLine);
        }

        public static void CheckForIllegalPathCharacters(this string path, string name)
        {
            if (path.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
                throw new CommandException($"Argument {name} has a value of {path} which contains invalid path characters. If your path has a trailing backslash either remove it or escape it correctly by using \\\\");
        }
    }
}
