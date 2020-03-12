using System;
using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Cli.Util;

namespace Octo.Tests.Extensions
{
    [TestFixture]
    public class StringExtensionsFixture
    {
        [Test]
        [TestCaseSource(nameof(GetTestCases))]
        public void ShouldNormalizeLineEndings(string input, string windowsResult, string nixResult, string platformAgnosticResult)
        {
            input.NormalizeNewLinesForWindows().Should().Be(windowsResult, $"windows requires carriage return, line feed.");
            input.NormalizeNewLinesForNix().Should().Be(nixResult, "*nix requires line feed.");
            input.NormalizeNewLines().Should().Be(platformAgnosticResult, "should use the environments preference.");
        }

        public static IEnumerable<TestCaseData> GetTestCases()
        {
            var newLine = Environment.NewLine;

            yield return new TestCaseData(string.Empty, string.Empty, string.Empty, string.Empty);
            yield return new TestCaseData("ABC 123 foo )&*)(*&\nbar\r\nABC 123 foo )&*)(*&\n\r", "ABC 123 foo )&*)(*&\r\nbar\r\nABC 123 foo )&*)(*&\r\n", "ABC 123 foo )&*)(*&\nbar\nABC 123 foo )&*)(*&\n", $"ABC 123 foo )&*)(*&{newLine}bar{newLine}ABC 123 foo )&*)(*&{newLine}");
            yield return new TestCaseData("\n\r\n\n\r", "\r\n\r\n\r\n", "\n\n\n", $"{newLine}{newLine}{newLine}");
            yield return new TestCaseData("\n foo \n\n", "\r\n foo \r\n\r\n", "\n foo \n\n", $"{newLine} foo {newLine}{newLine}");
            yield return new TestCaseData("\t\n\0", "\t\r\n\0", "\t\n\0", $"\t{newLine}\0");
            yield return new TestCaseData($"{newLine}{newLine}{newLine}", "\r\n\r\n\r\n", "\n\n\n", $"{newLine}{newLine}{newLine}");
        }
    }
}