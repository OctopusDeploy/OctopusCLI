using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Cli.Commands.Runbooks;
using Octopus.Cli.Infrastructure;
using Octopus.Client.Extensibility;
using Octopus.Client.Model;

namespace Octo.Tests.Commands
{
    [TestFixture]
    public class RunRunbookCommandTestFixture : ApiCommandFixtureBase
    {
        RunRunbookCommand runRunbookCommand;
        const string ProjectName = "TestProject";
        const string RunbookName = "Runbook";
        const string EnvironmentName = "Dev";
        TaskResource taskResource;

        [SetUp]
        public void SetUp()
        {
            runRunbookCommand = new RunRunbookCommand(RepositoryFactory, FileSystem, ClientFactory, CommandOutputProvider, ExecutionResourceWaiterFactory);

            var links = new LinkCollection {{"CreateRunbookRun", "test"}};
            var project = new ProjectResource();
            var runbook = new RunbookResource() { Links = links};
            var runbookRun = new RunbookRunResource() { TaskId = "Task-1" };

            taskResource = new TaskResource() { Id = "Task-1" };

            Repository.Projects.FindByName(ProjectName).Returns(project);
            Repository.Runbooks.FindByName(project, RunbookName).Returns(runbook);

            Repository.Client.Post<RunbookRunParameters, RunbookRunResource[]>("test", Arg.Any<RunbookRunParameters>()).Returns(Task.FromResult(new [] { runbookRun }));
            Repository.Tasks.Get(runbookRun.TaskId).Returns(taskResource);

            Repository.Tasks
                .When(x => x.WaitForCompletion(Arg.Any<TaskResource[]>(), Arg.Any<int>(), TimeSpan.FromSeconds(1), Arg.Any<Func<TaskResource[], Task>>()))
                .Do(x => throw new TimeoutException());
        }

        private void AddRequiredArgs()
        {
            CommandLineArgs.Add("--projectNameOrId=" + ProjectName);
            CommandLineArgs.Add("--runbookNameOrId=" + RunbookName);
            CommandLineArgs.Add("--environmentNameOrId=" + ValidEnvironment);
        }

        [Test]
        public void ShouldCancelDeploymentOnTimeoutIfRequested()
        {
            AddRequiredArgs();
            CommandLineArgs.Add("--runTimeout=00:00:01");
            CommandLineArgs.Add("--progress");
            CommandLineArgs.Add("--cancelontimeout");

            Func<Task> exec = () => runRunbookCommand.Execute(CommandLineArgs.ToArray());
            exec.ShouldThrow<CommandException>();

            Repository.Tasks.Received().Cancel(taskResource).GetAwaiter().GetResult();
        }

        [Test]
        public void ShouldNotCancelDeploymentOnTimeoutIfNotRequested()
        {
            AddRequiredArgs();
            CommandLineArgs.Add("--runTimeout=00:00:01");
            CommandLineArgs.Add("--progress");

            Func<Task> exec = () => runRunbookCommand.Execute(CommandLineArgs.ToArray());
            exec.ShouldThrow<CommandException>();

            Repository.Tasks.DidNotReceive().Cancel(Arg.Any<TaskResource>());
        }

        [Test]
        [TestCase("--projectNameOrId=" + ProjectName, "--environmentNameOrId=" + ValidEnvironment)]
        [TestCase("--runbookNameOrId=" + RunbookName, "--environmentNameOrId=" + ValidEnvironment)]
        [TestCase("--projectNameOrId=" + ProjectName, "--runbookNameOrId=" + RunbookName)]
        public void WhenARequiredParameterIsNotSupplied_ShouldThrowException(params string[] arguments)
        {
            foreach (var arg in arguments)
            {
                CommandLineArgs.Add(arg);
            }

            Func<Task> exec = () => runRunbookCommand.Execute(CommandLineArgs.ToArray());
            exec.ShouldThrow<CommandException>();
        }

        [Test]
        public void WhenAllRequiredParametersAreSupplied_ShouldNotThrowException()
        {
            AddRequiredArgs();

            Func<Task> exec = () => runRunbookCommand.Execute(CommandLineArgs.ToArray());
            exec.ShouldNotThrow<CommandException>();
        }

        [Test]
        public void WhenRunAtSuppliedIsAfterNotRunAfter_ShouldThrowException()
        {
            AddRequiredArgs();
            CommandLineArgs.Add("--runAt=" + "2020-07-16T03:00:00Z");
            CommandLineArgs.Add("--notRunAfter=" + "2020-07-15T03:00:00Z");

            Func<Task> exec = () => runRunbookCommand.Execute(CommandLineArgs.ToArray());
            exec.ShouldThrow<CommandException>();
        }

        [Test]
        public void WhenIncludedMachinesIntersectsWithExcludedMachines_ShouldThrowException()
        {
            AddRequiredArgs();
            CommandLineArgs.Add("--includeMachines=" + "one,two");
            CommandLineArgs.Add("--excludeMachines=" + "two,three");

            Func<Task> exec = () => runRunbookCommand.Execute(CommandLineArgs.ToArray());
            exec.ShouldThrow<CommandException>();
        }

        [Test]
        public void WhenATentantWildcardIsSupplied_NoOtherTenantIdsOrTagsCanBeSupplied()
        {
            AddRequiredArgs();
            CommandLineArgs.Add("--tenantIds=" + "*");
            CommandLineArgs.Add("--tenantTags=" + "beta,stable");

            Func<Task> exec = () => runRunbookCommand.Execute(CommandLineArgs.ToArray());
            exec.ShouldThrow<CommandException>();
        }
    }
}