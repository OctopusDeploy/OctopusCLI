using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Cli.Commands.Releases;
using Octopus.Cli.Infrastructure;
using Octopus.Cli.Model;
using Octopus.Cli.Util;
using Octopus.Client;
using Octopus.Client.Extensibility;
using Octopus.Client.Model;
using Octopus.Client.Repositories.Async;
using Octopus.CommandLine;
using Octopus.CommandLine.Commands;
using Serilog;

namespace Octo.Tests.Commands
{
    [TestFixture]
    public class ReleasePlanBuilderTests
    {
        public const string BuiltInFeedId = "feeds-builtin";
        readonly ChannelVersionRuleTestResult channelVersionRuleTestResult = new ChannelVersionRuleTestResult();
        readonly List<PackageResource> packages = new List<PackageResource>();
        ReleasePlanBuilder builder;
        ILogger logger;
        IPackageVersionResolver versionResolver;
        IChannelVersionRuleTester versionRuleTester;
        IOctopusAsyncRepository repository;
        IDeploymentProcessRepository deploymentProcessRepository;
        IDeploymentProcessRepositoryBeta deploymentProcessRepositoryBeta;
        IReleaseRepository releaseRepository;
        IFeedRepository feedRepository;
        ICommandOutputProvider commandOutputProvider;

        ProjectResource projectResource;
        ChannelResource channelResource;

        DeploymentProcessResource deploymentProcessResource;
        ReleaseTemplateResource releaseTemplateResource;
        List<ChannelVersionRuleResource> channelVersionRules;
        FeedResource feedResource;
        string gitReference;

        [SetUp]
        public void Setup()
        {
            // setup data objects
            channelVersionRules = new List<ChannelVersionRuleResource>();
            projectResource = new ProjectResource
            {
                DeploymentProcessId = TestHelpers.GetId("deploymentprocess"),
                Id = TestHelpers.GetId("project")
            };
            deploymentProcessResource = new DeploymentProcessResource
            {
                ProjectId = projectResource.Id,
                Id = projectResource.DeploymentProcessId
            };

            releaseTemplateResource = new ReleaseTemplateResource
            {
                DeploymentProcessId = projectResource.DeploymentProcessId,
                Packages = new List<ReleaseTemplatePackage>(),
                Id = TestHelpers.GetId("releaseTemplate")
            };
            channelResource = new ChannelResource
            {
                IsDefault = true,
                Id = TestHelpers.GetId("channel"),
                ProjectId = projectResource.Id,
                Rules = channelVersionRules,
                Name = TestHelpers.GetId("channelname")
            };
            feedResource = new FeedResource
            {
                Id = BuiltInFeedId,
                Name = "Built in feed",
                Links = new LinkCollection { { "SearchTemplate", TestHelpers.GetId("searchUri") } }
            };

            // setup mocks
            logger = Substitute.For<ILogger>();
            versionResolver = Substitute.For<IPackageVersionResolver>();
            versionRuleTester = Substitute.For<IChannelVersionRuleTester>();
            commandOutputProvider = Substitute.For<ICommandOutputProvider>();

            deploymentProcessRepository = Substitute.For<IDeploymentProcessRepository>();
            deploymentProcessRepository.Get(projectResource.DeploymentProcessId)
                .Returns(Task.FromResult(deploymentProcessResource));
            deploymentProcessRepository
                .GetTemplate(Arg.Is(deploymentProcessResource),
                    Arg.Is(channelResource))
                .Returns(Task.FromResult(releaseTemplateResource));
            versionRuleTester
                .Test(Arg.Any<IOctopusAsyncRepository>(), Arg.Any<ChannelVersionRuleResource>(), Arg.Any<string>(), Arg.Any<string>())
                .Returns(Task.FromResult(channelVersionRuleTestResult));

            deploymentProcessRepositoryBeta = Substitute.For<IDeploymentProcessRepositoryBeta>();
            deploymentProcessRepositoryBeta.Get(projectResource, Arg.Any<string>())
                .Returns(Task.FromResult(deploymentProcessResource));

            var feeds = new List<FeedResource>
            {
                feedResource
            };
            releaseRepository = Substitute.For<IReleaseRepository>();
            feedRepository = Substitute.For<IFeedRepository>();
            feedRepository.Get(Arg.Any<string[]>()).Returns(feeds);
            feedRepository.FindByNames(Arg.Any<IEnumerable<string>>()).Returns(feeds);

            repository = Substitute.For<IOctopusAsyncRepository>();
            repository.DeploymentProcesses.Returns(deploymentProcessRepository);
            repository.DeploymentProcesses.Beta().Returns(deploymentProcessRepositoryBeta);
            repository.Releases.Returns(releaseRepository);
            repository.Feeds.Returns(feedRepository);
            repository.Client
                .Get<List<PackageResource>>(Arg.Any<string>(), Arg.Any<IDictionary<string, object>>())
                .Returns(packages);

            builder = new ReleasePlanBuilder(logger, versionResolver, versionRuleTester, commandOutputProvider);
            gitReference = null;
        }

        [Test]
        public void NoPackageOrScriptSteps_ShouldNotBeViable()
        {
            // act
            var plan = ExecuteBuild();

            // assert
            plan.Should().NotBeNull();
            plan.IsViableReleasePlan().Should().BeFalse();
        }

        [Test]
        public void DisabledScriptSteps_ShouldNotBeAViablePlan()
        {
            // arrange
            var deploymentStepResource = ResourceBuilderHelpers.GetStep();
            deploymentStepResource.Actions.Add(ResourceBuilderHelpers.GetAction().Disabled().WithChannel(channelResource.Id));
            deploymentProcessResource.Steps.Add(deploymentStepResource);

            // act
            var plan = ExecuteBuild();

            // assert
            plan.IsViableReleasePlan().Should().BeFalse();
        }

        [Test]
        public void SingleEnabledScriptStep_ShouldBeAViablePlan()
        {
            // arrange
            var deploymentStepResource = ResourceBuilderHelpers.GetStep();
            deploymentStepResource.Actions.Add(ResourceBuilderHelpers.GetAction().WithChannel(channelResource.Id));
            deploymentProcessResource.Steps.Add(deploymentStepResource);

            // act
            var plan = ExecuteBuild();

            // assert
            plan.IsViableReleasePlan().Should().BeTrue();
        }

        [Test]
        public void SingleEnabledScriptStepScopedToDifferentChannel_ShouldNotBeAViablePlan()
        {
            // arrange
            var deploymentStepResource = ResourceBuilderHelpers.GetStep();
            deploymentStepResource.Actions.Add(ResourceBuilderHelpers.GetAction().WithChannel(TestHelpers.GetId("differentchannel")));
            deploymentProcessResource.Steps.Add(deploymentStepResource);

            // act
            var plan = ExecuteBuild();

            // assert
            plan.IsViableReleasePlan().Should().BeFalse();
        }

        [Test]
        public void SingleEnabledScriptStepScopedToMatchingChannel_ShouldBeViablePlan()
        {
            // arrange
            var deploymentStepResource = ResourceBuilderHelpers.GetStep();
            deploymentStepResource.Actions.Add(ResourceBuilderHelpers.GetAction().WithChannel(channelResource.Id));
            deploymentProcessResource.Steps.Add(deploymentStepResource);

            // act
            var plan = ExecuteBuild();

            // assert
            plan.IsViableReleasePlan().Should().BeTrue();
        }

        [TestCase("1.0.0")]
        [TestCase("v1.0.0")]
        [TestCase("blah")]
        public void SinglePackageStep_ShouldBeViablePlan(string version)
        {
            // arrange
            var deploymentStepResource = ResourceBuilderHelpers.GetStep();
            deploymentStepResource.Actions.Add(ResourceBuilderHelpers.GetAction().WithChannel(channelResource.Id).WithPackage());
            deploymentProcessResource.Steps.Add(deploymentStepResource);

            releaseTemplateResource.Packages.Add(GetReleaseTemplatePackage().WithPackage().WithVersion(version, versionResolver));
            channelVersionRuleTestResult.IsSatisfied();

            // act
            var plan = ExecuteBuild();

            // assert
            plan.IsViableReleasePlan().Should().BeTrue();
        }

        [Test]
        public void StepWithNoPackageVersion_ShouldNotBeViablePlan()
        {
            // arrange
            releaseTemplateResource.Packages.Add(GetReleaseTemplatePackage()
                .WithPackage()
                .WithVersion(string.Empty, versionResolver));

            // act
            var plan = ExecuteBuild();

            // assert
            plan.IsViableReleasePlan().Should().BeFalse();
        }

        [Test]
        public void StepFlaggedAsUnresolvable_ShouldNotBeViablePlan()
        {
            // arrange
            releaseTemplateResource.Packages.Add(GetReleaseTemplatePackage().WithPackage().IsNotResolvable());

            // act
            var plan = ExecuteBuild();

            // assert
            plan.IsViableReleasePlan().Should().BeFalse();
        }

        [TestCase("1.0.0")]
        [TestCase("v1.0.0")]
        [TestCase("blah")]
        public void MultipleSteps_OneNotResolvable_ShouldNotBeViablePlan(string version)
        {
            // arrange
            var releaseTemplatePackage = GetReleaseTemplatePackage().WithPackage();
            releaseTemplatePackage.IsResolvable = false;
            releaseTemplateResource.Packages.Add(releaseTemplatePackage);

            releaseTemplatePackage = GetReleaseTemplatePackage().WithPackage();
            releaseTemplatePackage.IsResolvable = true;
            releaseTemplateResource.Packages.Add(releaseTemplatePackage);

            repository.Client.Get<IList<PackageResource>>(Arg.Any<string>(), Arg.Any<IDictionary<string, object>>())
                .Returns(new List<PackageResource> { new PackageResource { Version = version } });

            // act
            var plan = ExecuteBuild();

            // assert
            plan.IsViableReleasePlan().Should().BeFalse();
        }

        [Test]
        public void SingleStep_ResolvableUsingFeed_ShouldBeViablePlan()
        {
            // arrange
            releaseTemplateResource.Packages.Add(GetReleaseTemplatePackage().WithPackage().IsResolvable());
            packages.Add(new PackageResource { Version = "1.0.0" });

            // act
            var plan = ExecuteBuild();

            // assert
            plan.IsViableReleasePlan().Should().BeTrue();
        }

        [Test]
        public void ChannelVersionRuleForNamedPackageReference_ShouldBeUsedToFilterPackages()
        {
            // arrange
            var deploymentStepResource = ResourceBuilderHelpers.GetStep();
            var action = ResourceBuilderHelpers.GetAction();
            action.Packages.Add(new PackageReference("Acme", "Acme", BuiltInFeedId, PackageAcquisitionLocation.Server));
            deploymentStepResource.Actions.Add(action);
            deploymentProcessResource.Steps.Add(deploymentStepResource);
            channelResource.AddRule(new ChannelVersionRuleResource
            {
                ActionPackages = new List<DeploymentActionPackageResource>
                {
                    new DeploymentActionPackageResource(action.Name, "Acme")
                },
                VersionRange = "(,1.0)"
            });

            packages.Add(new PackageResource { Version = "1.0.1" });

            releaseTemplateResource.Packages.Add(new ReleaseTemplatePackage { ActionName = action.Name, PackageReferenceName = "Acme", IsResolvable = true, FeedId = BuiltInFeedId });
            channelVersionRuleTestResult.IsSatisfied();

            repository.Client
                .Get<List<PackageResource>>(Arg.Any<string>(), Arg.Is<IDictionary<string, object>>(d => d.ContainsKey("versionRange") && (string)d["versionRange"] == "(,1.0)"))
                .Returns(new List<PackageResource>());

            // act
            var plan = ExecuteBuild();

            // assert
            plan.IsViableReleasePlan().Should().BeFalse();
        }

        [Test]
        public void VersionedControlledProject_ShouldRequireGitReference()
        {
            projectResource.IsVersionControlled = true;
            var ex = Assert.ThrowsAsync<CommandException>(ExecuteBuildAsync);
            ex.Message.Should().Be(ReleasePlanBuilder.GitReferenceMissingForVersionControlledProjectErrorMessage);
        }

        [Test]
        public void VersionControlledProject_WithGitReference_ShouldBeViablePlan()
        {
            gitReference = "main";
            projectResource.IsVersionControlled = true;
            var deploymentStepResource = ResourceBuilderHelpers.GetStep();
            deploymentStepResource.Actions.Add(ResourceBuilderHelpers.GetAction().WithChannel(channelResource.Id));
            deploymentProcessResource.Steps.Add(deploymentStepResource);

            var plan = ExecuteBuild();

            plan.IsViableReleasePlan().Should().BeTrue();
        }

        [Test]
        public void VersionControlledProject_ResolvableUsingFeed_ShouldBeViablePlan()
        {
            // arrange
            gitReference = "main";
            projectResource.IsVersionControlled = true;
            releaseTemplateResource.Packages.Add(GetReleaseTemplatePackage().WithPackage(ResourceBuilderHelpers.KeyedBy.Name).IsResolvable());
            packages.Add(new PackageResource { Version = "1.0.0" });

            // act
            var plan = ExecuteBuild();

            // assert
            plan.IsViableReleasePlan().Should().BeTrue();
        }

        [Test]
        public void DatabaseProject_ShouldRejectGitReference()
        {
            projectResource.IsVersionControlled = false;
            gitReference = "main";
            var ex = Assert.ThrowsAsync<CommandException>(ExecuteBuildAsync);
            ex.Message.Should().Be(ReleasePlanBuilder.GitReferenceSuppliedForDatabaseProjectErrorMessage(gitReference));
        }

        static ReleaseTemplatePackage GetReleaseTemplatePackage()
        {
            return new ReleaseTemplatePackage
            {
                ActionName = TestHelpers.GetId("step")
            };
        }

        ReleasePlan ExecuteBuild()
        {
            var task = ExecuteBuildAsync();
            return task.Result;
        }

        async Task<ReleasePlan> ExecuteBuildAsync()
        {
            return await builder.Build(repository,
                projectResource,
                channelResource,
                string.Empty,
                gitReference);
        }
    }

    public static class ResourceBuilderHelpers
    {
        public enum KeyedBy
        {
            Id,
            Name
        }

        public static DeploymentActionResource WithPackage(this DeploymentActionResource action)
        {
            action.Properties["Octopus.Action.Package.PackageId"] = TestHelpers.GetId("package");
            return action;
        }

        public static DeploymentActionResource Disabled(this DeploymentActionResource action)
        {
            action.IsDisabled = true;
            return action;
        }

        public static DeploymentActionResource WithChannel(this DeploymentActionResource action, string channelId)
        {
            action.Channels.Add(channelId);
            return action;
        }

        public static ReleaseTemplatePackage WithPackage(this ReleaseTemplatePackage releaseTemplatePackage, KeyedBy keyedBy = KeyedBy.Id)
        {
            releaseTemplatePackage.PackageId = TestHelpers.GetId("package");
            var feedKey = keyedBy == KeyedBy.Id ? ReleasePlanBuilderTests.BuiltInFeedId : "Built in feed";
            releaseTemplatePackage.FeedId = feedKey;
            return releaseTemplatePackage;
        }

        public static ReleaseTemplatePackage IsResolvable(this ReleaseTemplatePackage releaseTemplatePackage)
        {
            releaseTemplatePackage.IsResolvable = true;
            return releaseTemplatePackage;
        }

        public static ReleaseTemplatePackage IsNotResolvable(this ReleaseTemplatePackage releaseTemplatePackage)
        {
            releaseTemplatePackage.IsResolvable = false;
            return releaseTemplatePackage;
        }

        public static ReleaseTemplatePackage WithVersion(this ReleaseTemplatePackage releaseTemplatePackage,
            string version,
            IPackageVersionResolver versionResolver)
        {
            versionResolver.ResolveVersion(releaseTemplatePackage.ActionName, releaseTemplatePackage.PackageId)
                .Returns(version);
            versionResolver.ResolveVersion(releaseTemplatePackage.ActionName, releaseTemplatePackage.PackageId, null)
                .Returns(version);
            return releaseTemplatePackage;
        }

        public static ChannelVersionRuleTestResult IsSatisfied(this ChannelVersionRuleTestResult versionRuleTestResult)
        {
            versionRuleTestResult.SatisfiesPreReleaseTag = true;
            versionRuleTestResult.SatisfiesVersionRange = true;
            return versionRuleTestResult;
        }

        public static DeploymentStepResource GetStep()
        {
            return new DeploymentStepResource
            {
                Id = TestHelpers.GetId("deploymentstep"),
                Name = TestHelpers.GetId("deploymentstepName")
            };
        }

        public static DeploymentActionResource GetAction()
        {
            return new DeploymentActionResource
            {
                IsDisabled = false,
                Id = TestHelpers.GetId("action"),
                Name = TestHelpers.GetId("actionname")
            };
        }
    }

    public static class TestHelpers
    {
        public static string GetId()
        {
            return Guid.NewGuid().ToString().Replace("-", string.Empty);
        }

        public static string GetId(string prefix)
        {
            return $"{prefix}-{GetId()}";
        }
    }
}
