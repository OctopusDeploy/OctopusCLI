using System;
using System.Threading.Tasks;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Cli.Commands.Releases;
using Octopus.Cli.Model;
using Octopus.Client;
using Octopus.Client.Model;

namespace Octo.Tests.Commands
{
    [TestFixture]
    public class ChannelVersionRuleTesterFixture
    {
        const string FeedId = "feeds-builtin";

        [Test]
        public async Task NoRuleShouldReturnNullResult()
        {
            var repo = Substitute.For<IOctopusAsyncRepository>();

            var result = await new ChannelVersionRuleTester().Test(repo, null, "1.0.0", FeedId);
            result.IsNull.Should().BeTrue();

            result.SatisfiesVersionRange.Should().BeTrue();
            result.SatisfiesPreReleaseTag.Should().BeTrue();

            await repo.Client.DidNotReceive().Post<object, ChannelVersionRuleTestResult>(Arg.Any<string>(), Arg.Any<object>());
        }

        [Test]
        public async Task NoPackageVersionShouldReturnFailedResult()
        {
            var repo = Substitute.For<IOctopusAsyncRepository>();

            var rule = new ChannelVersionRuleResource
            {
                VersionRange = "[1.0,)",
                Tag = "$^"
            };
            var result = await new ChannelVersionRuleTester().Test(repo, rule, "", FeedId);
            result.IsNull.Should().BeFalse();

            result.SatisfiesVersionRange.Should().BeFalse();
            result.SatisfiesPreReleaseTag.Should().BeFalse();

            await repo.Client.DidNotReceive().Post<object, ChannelVersionRuleTestResult>(Arg.Any<string>(), Arg.Any<object>());
        }

        [Test]
        public async Task PackageVersionShouldReturnSuccessfulResult()
        {
            var expectedTestResult = new ChannelVersionRuleTestResult
            {
                SatisfiesVersionRange = true,
                SatisfiesPreReleaseTag = true
            };
            var repo = Substitute.For<IOctopusAsyncRepository>();
            repo.Client.Post<object, ChannelVersionRuleTestResult>(Arg.Any<string>(), Arg.Any<object>())
                .Returns(Task.FromResult(expectedTestResult));

            var rule = new ChannelVersionRuleResource
            {
                VersionRange = "[1.0,)",
                Tag = "$^"
            };
            var result = await new ChannelVersionRuleTester().Test(repo, rule, "1.0.0", FeedId);
            result.IsNull.Should().BeFalse();

            result.SatisfiesVersionRange.Should().BeTrue();
            result.SatisfiesPreReleaseTag.Should().BeTrue();

            await repo.Client.Received(1).Post<object, ChannelVersionRuleTestResult>(Arg.Any<string>(), Arg.Any<object>());
        }
    }
}
