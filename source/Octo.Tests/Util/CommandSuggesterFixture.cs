using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Cli.Util;

namespace Octo.Tests.Util
{
    [TestFixture]
    public class CompleteCommandFixture
    {
        Dictionary<string, string[]> testCompletionItems = new Dictionary<string, string[]>
        { 
            { "list-deployments", new [] { "--apiKey" } },
            { "list-environments", new [] { "--url", "--space" } },
            { "list-projects", new string[] {} },
            { "help", new [] {"--helpOutputFormat", "--help" } } 
        };
    
        [TestCaseSource(nameof(GetTestCases))]
        public void ShouldGetCorrectCompletions(string[] words, string[] expectedItems)
        {
            CommandSuggester.SuggestCommandsFor(words, testCompletionItems).ShouldBeEquivalentTo(expectedItems);
        }

        static IEnumerable<TestCaseData> GetTestCases()
        {
            yield return new TestCaseData(new [] { "list" }, new [] {"list-deployments", "list-environments", "list-projects" });
            yield return new TestCaseData(new [] { "list-e" }, new [] {"list-environments" });
            yield return new TestCaseData(new [] { "" }, new [] {"list-deployments", "list-environments", "list-projects", "help" });
            yield return new TestCaseData(new string[] { null }, new [] {"list-deployments", "list-environments", "list-projects", "help" });
            yield return new TestCaseData(new string[] { "junk" }, new string[] { });
            yield return new TestCaseData(new string[] { "list-environments", "--" }, new [] { "--url", "--space" });
            yield return new TestCaseData(new string[] { "list-environments", "--sp" }, new [] { "--space" });
            yield return new TestCaseData(new string[] { "--" }, new [] { "--help", "--helpOutputFormat" });
        }
    }
}