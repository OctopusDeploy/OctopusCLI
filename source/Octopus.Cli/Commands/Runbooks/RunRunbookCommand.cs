using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Octopus.Cli.Commands.Deployment;
using Octopus.Cli.Infrastructure;
using Octopus.Cli.Repositories;
using Octopus.Cli.Util;
using Octopus.Client;
using Octopus.Client.Model;
using Octostache;

namespace Octopus.Cli.Commands.Runbooks
{
    public class RunbookRunParameters
    {
        public RunbookRunParameters()
        {
            FormValues = new Dictionary<string, string>();
            EnvironmentIds = new string[] { };
            TenantTagNames = new string[] { };
            TenantIds = new string[] { };
        }

        public bool UseDefaultSnapshot { get; set; } // should be set to false by default in the CLI

        public string RunbookId { get; set; }
        public string ProjectId { get; set; }
        // public string EnvironmentId { get; set; } // unused in cli - for endpoint backwards compat
        public string[] EnvironmentIds { get; set; }
        public string RunbookSnapshotNameOrId { get; set; }
        public bool ForcePackageDownload { get; set; }
        public bool UseGuidedFailure { get; set; }
        public string[] SpecificMachineIds { get; set; }
        public string[] ExcludedMachineIds { get; set; }
        public string[] TenantIds { get; set; }
        public string[] TenantTagNames { get; set; }
        public string[] SkipActions { get; set; }
        public DateTimeOffset? QueueTime { get; set; }
        public DateTimeOffset? QueueTimeExpiry { get; set; }
        public Dictionary<string, string> FormValues { get; set; }
    }

    [Command("run-runbook", Description = "Runs a Runbook.")]
    public class RunRunbookCommand : ApiCommand, ISupportFormattedOutput
    {
        private readonly ExecutionResourceWaiter.Factory executionResourceWaiterFactory;
        private readonly Dictionary<string, string> variables = new Dictionary<string, string>();

        private RunbookRunResource[] runbookRuns { get; set; }

        private string ProjectNameOrId { get; set; } // required for tenant filtering with *, runbook filtering
        private string RunbookNameOrId { get; set; }
        private List<string> EnvironmentNamesOrIds { get; } = new List<string>();
        private string Snapshot { get; set; }
        private bool ForcePackageDownload { get; set; }
        private bool GuidedFailure { get; set; }
        private List<string> IncludedMachineIds { get; } = new List<string>();
        private List<string> ExcludedMachineIds { get; } = new List<string>();
        private List<string> StepNamesToSkip { get; } = new List<string>();
        private bool UseDefaultSnapshot { get; } = false;
        private List<string> TenantIds { get; } = new List<string>();
        private List<string> TenantTagNames { get; } = new List<string>();
        private DateTimeOffset? RunAt { get; set; }
        private DateTimeOffset? NoRunAfter { get; set; }

        private bool WaitForRun { get; set; }
        private bool Progress { get; set; }
        private TimeSpan RunTimeout { get; set; } = TimeSpan.FromMinutes(10);
        private bool CancelOnTimeout { get; set; }
        private TimeSpan RunCheckSleepCycle { get; set; } = TimeSpan.FromSeconds(10);
        private bool NoRawLog { get; set; }
        private string RawLogFile { get; set; }

        public RunRunbookCommand(
            IOctopusAsyncRepositoryFactory repositoryFactory,
            IOctopusFileSystem fileSystem,
            IOctopusClientFactory clientFactory,
            ICommandOutputProvider commandOutputProvider,
            ExecutionResourceWaiter.Factory executionResourceWaiterFactory) : base(clientFactory,
            repositoryFactory, fileSystem, commandOutputProvider)
        {
            this.executionResourceWaiterFactory = executionResourceWaiterFactory;
            var options = Options.For("Run Runbook");

            options.Add<string>("project=",
                "Name or ID of the project. This is optional if the runbook argument is an ID",
                v => ProjectNameOrId = v);

            options.Add<string>("runbook=",
                "Name or ID of the runbook. If the name is supplied, the project parameter must also be specified.",
                v => RunbookNameOrId = v);

            options.Add<string>("environment=",
                "Name or ID of the environment to deploy to, e.g ., 'Production' or 'Environments-1'; specify this argument multiple times to run on multiple environments.",
                v => EnvironmentNamesOrIds.Add(v));

            options.Add<string>("snapshot=",
                "[Optional] Name or ID of the snapshot to run. If not supplied, the published snapshot should be used.",
                v => Snapshot = v);

            options.Add<bool>("forcePackageDownload",
                "[Optional] Whether to force downloading of already installed packages (flag, default false).",
                v => ForcePackageDownload = true);

            options.Add<bool>("guidedFailure=",
                "[Optional] Whether to use Guided Failure mode. (True or False. If not specified, will use default setting from environment)",
                v => GuidedFailure = v);

            options.Add<string>("specificMachines=",
                "[Optional] A comma-separated list of machine names to target in the deployed environment. If not specified all machines in the environment will be considered.",
                v => IncludedMachineIds.AddRange(v.Split(new[] {','}, StringSplitOptions.RemoveEmptyEntries)
                    .Select(m => m.Trim())));

            options.Add<string>("excludeMachines=",
                "[Optional] A comma-separated list of machine names to exclude in the deployed environment. If not specified all machines in the environment will be considered.",
                v => ExcludedMachineIds.AddRange(v.Split(new[] {','}, StringSplitOptions.RemoveEmptyEntries)
                    .Select(m => m.Trim())));

            options.Add<string>("tenantId=",
                "[Optional] Create a deployment for the tenant with this name or ID; specify this argument multiple times to add multiple tenants or use `*` wildcard to deploy to all tenants who are ready for this release (according to lifecycle).",
                v => TenantIds.Add(v));

            options.Add<string>("tenantTag=",
                "[Optional] Create a deployment for tenants matching this tag; specify this argument multiple times to build a query/filter with multiple tags, just like you can in the user interface.",
                v => TenantTagNames.Add(v));

            options.Add<string>("StepNamesToSkip=", "[Optional] Skip a step by name", v => StepNamesToSkip.Add(v));

            options.Add<DateTimeOffset>("runAt=",
                "[Optional] Time at which deployment should start (scheduled deployment), specified as any valid DateTimeOffset format, and assuming the time zone is the current local time zone.",
                v => RunAt = v);

            options.Add<DateTimeOffset>("noRunAfter=",
                "[Optional] Time at which scheduled deployment should expire, specified as any valid DateTimeOffset format, and assuming the time zone is the current local time zone.",
                v => NoRunAfter = v);

            options.Add<string>("v|variable=",
                "[Optional] Values for any prompted variables in the format Label:Value. For JSON values, embedded quotation marks should be escaped with a backslash. Specify this argument multiple times to add multiple variables.",
                ParseVariable);

            options.Add<bool>("waitForRun", "[Optional] Whether to wait synchronously for deployment to finish.",
                v => WaitForRun = true);

            options.Add<bool>("progress", "[Optional] Show progress of the deployment", v => { Progress = true; WaitForRun = true; NoRawLog = true; });

            options.Add<TimeSpan>("runTimeout=",
                "[Optional] Specifies maximum time (timespan format) that the console session will wait for the deployment to finish(default 00:10:00). This will not stop the deployment. Requires -- waitfordeployment parameter set.",
                v => RunTimeout = v);

            options.Add<bool>("cancelOnTimeout",
                "[Optional] Whether to cancel the deployment if the deployment timeout is reached (flag, default false).",
                v => CancelOnTimeout = true);

            options.Add<TimeSpan>("runCheckSleepCycle=",
                "[Optional] Specifies how much time (timespan format) should elapse between deployment status checks (default 00:00:10)",
                v => RunCheckSleepCycle = v);

            options.Add<bool>("noRawLog", "[Optional] Don't print the raw log of failed tasks", v => NoRawLog = true);

            options.Add<string>("rawLogFile=", "[Optional] Redirect the raw log of failed tasks to a file",
                v => RawLogFile = v);
        }

        public async Task Request()
        {
            var project = await Repository.Projects.FindByNameOrIdOrFail(ProjectNameOrId).ConfigureAwait(false);
            var runbook = await Repository.Runbooks.FindByNameOrIdOrFail(RunbookNameOrId, project).ConfigureAwait(false);
            var environments = await Repository.Environments.FindByNamesOrIdsOrFail(EnvironmentNamesOrIds).ConfigureAwait(false);
            LogScheduledDeployment();

            var payload = new RunbookRunParameters()
            {
                RunbookId = runbook.Id,
                ProjectId = project.Id,
                EnvironmentIds = environments.Select(env => env.Id).ToArray(),
                RunbookSnapshotNameOrId = Snapshot,
                UseDefaultSnapshot = UseDefaultSnapshot,
                ForcePackageDownload = ForcePackageDownload,
                SpecificMachineIds = IncludedMachineIds.ToArray(),
                ExcludedMachineIds = ExcludedMachineIds.ToArray(),
                SkipActions = StepNamesToSkip.ToArray(),
                UseGuidedFailure = GuidedFailure,
                TenantIds = TenantIds.ToArray(),
                TenantTagNames = TenantTagNames.ToArray(),
                QueueTime = RunAt,
                QueueTimeExpiry = NoRunAfter,
                FormValues = variables
            };
            var requestUri = runbook.Link("CreateRunbookRun");
            runbookRuns = await Repository.Client.Post<RunbookRunParameters, RunbookRunResource[]>(requestUri, payload);

            if (runbookRuns.Any() && WaitForRun)
            {
                var waiter = executionResourceWaiterFactory(Repository, ServerBaseUrl);
                await waiter.WaitForRunbookRunToComplete(
                    runbookRuns,
                    project,
                    Progress,
                    NoRawLog,
                    RawLogFile,
                    CancelOnTimeout,
                    RunCheckSleepCycle,
                    RunTimeout).ConfigureAwait(false);
            }
        }

        protected override Task ValidateParameters()
        {
            if (ProjectNameOrId == null)
            {
                throw new CommandException("A project name or id must be supplied.");
            }

            if (RunbookNameOrId == null)
            {
                throw new CommandException("Runbook name or id must be supplied.");
            }

            if (!EnvironmentNamesOrIds.Any())
            {
                throw new CommandException("One or more environment must be supplied.");
            }

            if ((RunAt ?? DateTimeOffset.Now) > NoRunAfter)
                throw new CommandException("The Run will expire before it has a chance to execute.  Please select an expiry time that occurs after the deployment is scheduled to begin");

            CheckForIntersection(IncludedMachineIds.ToList(), ExcludedMachineIds.ToList());

            if (TenantIds.Contains("*") && (TenantIds.Count > 1 || TenantTagNames.Count > 0))
                throw new CommandException(
                    "When running on all tenants using the --tenantIds=* wildcard, no other tenant filters can be provided");

            return Task.FromResult(0);
        }

        private static void CheckForIntersection(IEnumerable<string> included, IEnumerable<string> excluded)
        {
            var intersection = included.Intersect(excluded).ToArray();
            if (intersection.Any())
            {
                throw new CommandException(
                    $"Cannot specify the same machine as both included and excluded: {intersection.ReadableJoin(", ")}");
            }
        }
        private void LogScheduledDeployment()
        {
            if (RunAt == null) return;
            var now = DateTimeOffset.UtcNow;
            commandOutputProvider.Information("Runbook run will be scheduled to start in: {Duration:l}", (RunAt.Value - now).FriendlyDuration());
        }
        
        void ParseVariable(string variable)
        {
            var index = variable.IndexOfAny(new[] {':', '='});
            if (index <= 0)
                return;

            var key = variable.Substring(0, index);
            var value = (index >= variable.Length - 1) ? string.Empty : variable.Substring(index + 1);

            variables.Add(key, value);
        }

        public void PrintDefaultOutput()
        {
        }

        public void PrintJsonOutput()
        {
            foreach (var res in runbookRuns)
            {
                commandOutputProvider.Json(new
                {
                    res.SpaceId,
                    res.ProjectId,
                    res.RunbookId,
                    Environment = res.EnvironmentId,
                    Snapshot = res.RunbookSnapshotId,
                    Tenant = res.TenantId,
                    res.ForcePackageDownload,
                    res.SkipActions,
                    IncludedMachines = res.SpecificMachineIds,
                    ExcludedMachines = res.ExcludedMachineIds,
                    res.UseGuidedFailure,
                    res.QueueTime,
                    res.QueueTimeExpiry,
                    Machines = res.DeployedToMachineIds.ToList()
                        .Select(machine => machine.ToString()), // Sus - reference collection
                });
            }
        }
    }
}