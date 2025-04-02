using System;
using System.Threading.Tasks;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Cli.Commands.Runbooks;
using Octopus.Client.Extensibility;
using Octopus.Client.Model;
using Octopus.CommandLine.Commands;

namespace Octo.Tests.Commands
{
    [TestFixture]
    public class RunRunbookCommandTestFixture : ApiCommandFixtureBase
    {
        const string ProjectName = "TestProject";
        const string RunbookName = "Runbook";
        const string EnvironmentName = "Dev";
        RunRunbookCommand runRunbookCommand;
        TaskResource taskResource;

        [SetUp]
        public void SetUp()
        {
            runRunbookCommand = new RunRunbookCommand(RepositoryFactory,
                FileSystem,
                ClientFactory,
                CommandOutputProvider,
                ExecutionResourceWaiterFactory);

            var links = new LinkCollection { { "CreateRunbookRun", "test" } };
            var project = new ProjectResource();
            var runbook = new RunbookResource { Links = links };
            var runbookRun = new RunbookRunResource { TaskId = "Task-1" };

            taskResource = new TaskResource { Id = "Task-1" };

            Repository.Projects.FindByName(ProjectName).Returns(project);
            Repository.Runbooks.FindByName(project, RunbookName).Returns(runbook);

            Repository.Runbooks.Run(runbook, Arg.Any<RunbookRunParameters>()).Returns(Task.FromResult(new[] { runbookRun }));
            Repository.Tasks.Get(runbookRun.TaskId).Returns(taskResource);

            Repository.Tasks
                .When(x => x.WaitForCompletion(Arg.Any<TaskResource[]>(), Arg.Any<int>(), TimeSpan.FromSeconds(1), Arg.Any<Func<TaskResource[], Task>>()))
                .Do(x => throw new TimeoutException());
        }

        void AddRequiredArgs()
        {
            CommandLineArgs.Add("--project=" + ProjectName);
            CommandLineArgs.Add("--runbook=" + RunbookName);
            CommandLineArgs.Add("--environment=" + ValidEnvironment);
        }

        [Test]
        public async Task ShouldCancelDeploymentOnTimeoutIfRequested()
        {
            AddRequiredArgs();
            CommandLineArgs.Add("--runTimeout=00:00:01");
            CommandLineArgs.Add("--progress");
            CommandLineArgs.Add("--cancelontimeout");

            Func<Task> exec = () => runRunbookCommand.Execute(CommandLineArgs.ToArray());
            await exec.Should().ThrowAsync<CommandException>();

            Repository.Tasks.Received().Cancel(taskResource).GetAwaiter().GetResult();
        }

        [Test]
        public async Task ShouldNotCancelDeploymentOnTimeoutIfNotRequested()
        {
            AddRequiredArgs();
            CommandLineArgs.Add("--runTimeout=00:00:01");
            CommandLineArgs.Add("--progress");

            Func<Task> exec = () => runRunbookCommand.Execute(CommandLineArgs.ToArray());
            await exec.Should().ThrowAsync<CommandException>();

            await Repository.Tasks.DidNotReceive().Cancel(Arg.Any<TaskResource>());
        }

        [Test]
        [TestCase("--project=" + ProjectName, "--environment=" + ValidEnvironment)]
        [TestCase("--runbook=" + RunbookName, "--environment=" + ValidEnvironment)]
        [TestCase("--project=" + ProjectName, "--runbook=" + RunbookName)]
        public async Task WhenARequiredParameterIsNotSupplied_ShouldThrowException(params string[] arguments)
        {
            foreach (var arg in arguments)
                CommandLineArgs.Add(arg);

            Func<Task> exec = () => runRunbookCommand.Execute(CommandLineArgs.ToArray());
            await exec.Should().ThrowAsync<CommandException>();
        }

        [Test]
        public async Task WhenAllRequiredParametersAreSupplied_ShouldNotThrowException()
        {
            AddRequiredArgs();

            Func<Task> exec = () => runRunbookCommand.Execute(CommandLineArgs.ToArray());
            await exec.Should().NotThrowAsync<CommandException>();
        }

        [Test]
        public async Task WhenRunAtSuppliedIsAfterNotRunAfter_ShouldThrowException()
        {
            AddRequiredArgs();
            CommandLineArgs.Add("--runAt=" + "2020-07-16T03:00:00Z");
            CommandLineArgs.Add("--notRunAfter=" + "2020-07-15T03:00:00Z");

            Func<Task> exec = () => runRunbookCommand.Execute(CommandLineArgs.ToArray());
            await exec.Should().ThrowAsync<CommandException>();
        }

        [Test]
        public async Task WhenIncludedMachinesIntersectsWithExcludedMachines_ShouldThrowException()
        {
            AddRequiredArgs();
            CommandLineArgs.Add("--specificMachines=" + "one,two");
            CommandLineArgs.Add("--excludeMachines=" + "two,three");

            Func<Task> exec = () => runRunbookCommand.Execute(CommandLineArgs.ToArray());
            await exec.Should().ThrowAsync<CommandException>();
        }

        [Test]
        public async Task WhenATentantWildcardIsSupplied_NoOtherTenantIdsOrTagsCanBeSupplied()
        {
            AddRequiredArgs();
            CommandLineArgs.Add("--tenant=" + "*");
            CommandLineArgs.Add("--tenantTag=" + "beta,stable");

            Func<Task> exec = () => runRunbookCommand.Execute(CommandLineArgs.ToArray());
            await exec.Should().ThrowAsync<CommandException>();
        }
    }
}
