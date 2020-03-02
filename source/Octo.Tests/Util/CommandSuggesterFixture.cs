using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Cli.Util;

namespace Octo.Tests.Util
{
    [TestFixture]
    public class CompleteCommandFixture
    {
        List<string> testCompletionItems = new List<string>
        { 
            "list-deployments", "list-environments", "list-projects" 
        };
    
        [TestCaseSource(nameof(GetTestCases))]
        public void ShouldGetCorrectCompletions(string words, string[] expectedItems)
        {
            CommandSuggester.SuggestCommandsFor(words, testCompletionItems).ShouldBeEquivalentTo(expectedItems);
        }

        static IEnumerable<TestCaseData> GetTestCases()
        {
            yield return new TestCaseData("list", new [] {"list-deployments", "list-environments", "list-projects" });
            yield return new TestCaseData("list-e", new [] {"list-environments" });
        }
    }
}