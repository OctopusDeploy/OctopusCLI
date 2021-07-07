using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Octopus.Client;
using Octopus.Client.Model;
using Octopus.CommandLine;
using Octopus.CommandLine.Commands;

namespace Octopus.Cli.Commands.Deployment
{
    public interface IExecutionResourceWaiter
    {
        Task WaitForDeploymentToComplete(
            IReadOnlyList<DeploymentResource> resources,
            ProjectResource project,
            ReleaseResource release,
            bool showProgress,
            bool noRawLog,
            string rawLogFile,
            bool cancelOnTimeout,
            TimeSpan deploymentStatusCheckSleepCycle,
            TimeSpan deploymentTimeout);

        Task WaitForRunbookRunToComplete(
            IReadOnlyList<RunbookRunResource> resources,
            ProjectResource project,
            bool showProgress,
            bool noRawLog,
            string rawLogFile,
            bool cancelOnTimeout,
            TimeSpan deploymentStatusCheckSleepCycle,
            TimeSpan deploymentTimeout);
    }

    public class ExecutionResourceWaiter : IExecutionResourceWaiter
    {
        public delegate IExecutionResourceWaiter Factory(IOctopusAsyncRepository repository, string serverBaseUrl);

        readonly TaskOutputProgressPrinter printer = new TaskOutputProgressPrinter();
        readonly ICommandOutputProvider commandOutputProvider;
        readonly IOctopusAsyncRepository repository;
        readonly string serverBaseUrl;

        public ExecutionResourceWaiter(
            ICommandOutputProvider commandOutputProvider,
            IOctopusAsyncRepository repository,
            string serverBaseUrl)
        {
            this.commandOutputProvider = commandOutputProvider;
            this.repository = repository;
            this.serverBaseUrl = serverBaseUrl;
        }

        public async Task WaitForDeploymentToComplete(
            IReadOnlyList<DeploymentResource> resources,
            ProjectResource project,
            ReleaseResource release,
            bool showProgress,
            bool noRawLog,
            string rawLogFile,
            bool cancelOnTimeout,
            TimeSpan deploymentStatusCheckSleepCycle,
            TimeSpan deploymentTimeout)
        {
            async Task GuidedFailureWarning(IExecutionResource guidedFailureDeployment)
            {
                var environment = await repository.Environments.Get(((DeploymentResource)guidedFailureDeployment).Link("Environment")).ConfigureAwait(false);
                commandOutputProvider.Warning("  - {Environment:l}: {Url:l}",
                    environment.Name,
                    GetPortalUrl(
                        $"/app#/projects/{project.Slug}/releases/{release.Version}/deployments/{guidedFailureDeployment.Id}"));
            }

            await WaitForExecutionToComplete(
                resources,
                showProgress,
                noRawLog,
                rawLogFile,
                cancelOnTimeout,
                deploymentStatusCheckSleepCycle,
                deploymentTimeout,
                GuidedFailureWarning,
                "deployment");
        }

        public async Task WaitForRunbookRunToComplete(
            IReadOnlyList<RunbookRunResource> resources,
            ProjectResource project,
            bool showProgress,
            bool noRawLog,
            string rawLogFile,
            bool cancelOnTimeout,
            TimeSpan deploymentStatusCheckSleepCycle,
            TimeSpan deploymentTimeout)
        {
            async Task GuidedFailureWarning(IExecutionResource runExecution)
            {
                var runbookRun = (RunbookRunResource)runExecution;
                var environment = await repository.Environments.Get(runbookRun.Link("Environment")).ConfigureAwait(false);

                commandOutputProvider.Warning("  - {Environment:l}: {Url:l}",
                    environment.Name,
                    GetPortalUrl(
                        $"/app#/projects/{project.Slug}/operations/runbooks/{runbookRun.RunbookId}/snapshots/{runbookRun.RunbookSnapshotId}/runs/{runbookRun.Id}"));
            }

            await WaitForExecutionToComplete(
                resources,
                showProgress,
                noRawLog,
                rawLogFile,
                cancelOnTimeout,
                deploymentStatusCheckSleepCycle,
                deploymentTimeout,
                GuidedFailureWarning,
                "runbook run"
            );
        }

        async Task WaitForExecutionToComplete(
            IReadOnlyCollection<IExecutionResource> resources,
            bool showProgress,
            bool noRawLog,
            string rawLogFile,
            bool cancelOnTimeout,
            TimeSpan deploymentStatusCheckSleepCycle,
            TimeSpan deploymentTimeout,
            Func<IExecutionResource, Task> guidedFailureWarningGenerator,
            string alias
        )
        {
            var getTasks = resources.Select(dep => repository.Tasks.Get(dep.TaskId));
            var deploymentTasks = await Task.WhenAll(getTasks).ConfigureAwait(false);
            if (showProgress && resources.Count > 1)
                commandOutputProvider.Information("Only progress of the first task ({Task:l}) will be shown", deploymentTasks.First().Name);

            try
            {
                commandOutputProvider.Information($"Waiting for {{NumberOfTasks}} {alias}(s) to complete....", deploymentTasks.Length);
                await repository.Tasks.WaitForCompletion(deploymentTasks.ToArray(), deploymentStatusCheckSleepCycle.Seconds, deploymentTimeout, PrintTaskOutput).ConfigureAwait(false);
                var failed = false;
                foreach (var deploymentTask in deploymentTasks)
                {
                    var updated = await repository.Tasks.Get(deploymentTask.Id).ConfigureAwait(false);
                    if (updated.FinishedSuccessfully)
                    {
                        commandOutputProvider.Information("{Task:l}: {State}", updated.Description, updated.State);
                    }
                    else
                    {
                        commandOutputProvider.Error("{Task:l}: {State}, {Error:l}", updated.Description, updated.State, updated.ErrorMessage);
                        failed = true;

                        if (noRawLog)
                            continue;

                        try
                        {
                            var raw = await repository.Tasks.GetRawOutputLog(updated).ConfigureAwait(false);
                            if (!string.IsNullOrEmpty(rawLogFile))
                                File.WriteAllText(rawLogFile, raw);
                            else
                                commandOutputProvider.Error(raw);
                        }
                        catch (Exception ex)
                        {
                            commandOutputProvider.Error(ex, "Could not retrieve the raw task log for the failed task.");
                        }
                    }
                }

                if (failed)
                    throw new CommandException($"One or more {alias} tasks failed.");

                commandOutputProvider.Information("Done!");
            }
            catch (TimeoutException e)
            {
                commandOutputProvider.Error(e.Message);

                await CancelExecutionOnTimeoutIfRequested(deploymentTasks, cancelOnTimeout, alias).ConfigureAwait(false);

                var guidedFailureDeployments =
                    from d in resources
                    where d.UseGuidedFailure
                    select d;
                if (guidedFailureDeployments.Any())
                {
                    commandOutputProvider.Warning($"One or more {alias} are using guided failure. Use the links below to check if intervention is required:");
                    foreach (var guidedFailureDeployment in guidedFailureDeployments)
                        await guidedFailureWarningGenerator(guidedFailureDeployment);
                }

                throw new CommandException(e.Message);
            }
        }

        Task CancelExecutionOnTimeoutIfRequested(IReadOnlyList<TaskResource> deploymentTasks, bool cancelOnTimeout, string alias)
        {
            if (!cancelOnTimeout)
                return Task.WhenAll();

            var tasks = deploymentTasks.Select(async task =>
            {
                commandOutputProvider.Warning($"Cancelling {alias} task '{{Task:l}}'", task.Description);
                try
                {
                    await repository.Tasks.Cancel(task).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    commandOutputProvider.Error($"Failed to cancel {alias} task '{{Task:l}}': {{ExceptionMessage:l}}", task.Description, ex.Message);
                }
            });
            return Task.WhenAll(tasks);
        }

        Task PrintTaskOutput(TaskResource[] taskResources)
        {
            var task = taskResources.First();
            return printer.Render(repository, commandOutputProvider, task);
        }

        string GetPortalUrl(string path)
        {
            if (!path.StartsWith("/")) path = '/' + path;
            var uri = new Uri(serverBaseUrl + path);
            return uri.AbsoluteUri;
        }
    }
}
