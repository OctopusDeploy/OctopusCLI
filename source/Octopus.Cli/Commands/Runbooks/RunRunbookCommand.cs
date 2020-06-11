using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Octopus.Cli.Infrastructure;
using Octopus.Cli.Repositories;
using Octopus.Cli.Util;
using Octopus.Client;
using Octopus.Client.Model;
using Octostache;

namespace Octopus.Cli.Commands.Runbooks
{
    [Command("run-runbook", Description = "Runs a Runbook.")]
    public class RunRunbookCommand : ApiCommand, ISupportFormattedOutput
    {
        private RunbookRunResource book;
        readonly VariableDictionary variables = new VariableDictionary();

        // Required arguments
        private string RunbookNameOrId { get; set; }
        private string ProjectNameOrId { get; set; }
        private string EnvironmentNameOrId { get; set; }

        //Optional arguments
        private string Snapshot { get; set; }
        private bool ForcePackageDownload { get; set; } = false;
        private bool? GuidedFailure { get; set; }
        private List<string> IncludedMachineIds { get; } = new List<string>();
        private List<string> ExcludeMachineIds { get; } = new List<string>();
        private List<string> StepsToSkip { get; } = new List<string>();
        
        private bool Progress { get; set; }
        private bool WaitForRun { get; set; }
        private TimeSpan RunTimeout { get; set; } = TimeSpan.FromMinutes(10);
        private bool CancelOnTimeout { get; set; }
        private TimeSpan RunCheckSleepCycle { get; set; } = TimeSpan.FromSeconds(10);
        private bool NoRawLog { get; set; }
        private string RawLogFile { get; set; }
        private string Variable { get; set; }
        private DateTimeOffset? RunAt { get; set; }
        private DateTimeOffset? NoRunAfter { get; set; }
        private List<string> Tenants { get; } = new List<string>();
        private List<string> TenantTags { get; } = new List<string>();

        private bool IsTenantedRunbookRun => Tenants.Any() || TenantTags.Any();
        private const char Separator = '/';

        public RunRunbookCommand(IOctopusAsyncRepositoryFactory repositoryFactory, IOctopusFileSystem fileSystem,
            IOctopusClientFactory clientFactory, ICommandOutputProvider commandOutputProvider) : base(clientFactory,
            repositoryFactory, fileSystem, commandOutputProvider)
        {
            var options = Options.For("Run Runbook");
            
            // required //    
            options.Add<string>("runbook=", // TODO can't we use the 
                "Name or ID of the runbook. If the name is supplied, the project parameter must also be specified.",
                v => RunbookNameOrId = v);
            options.Add<string>("project=",
                "Name or ID of the project. This is optional if the runbook argument is an ID",
                v => ProjectNameOrId = v);
            options.Add<string>("environment=",
                "Name or ID of the environment to deploy to, e.g ., 'Production' or 'Environments-1'; specify this argument multiple times to deploy to multiple environments.",
                v => EnvironmentNameOrId = v);
            
            // optional //
            options.Add<string>("snapshot=",
                "[Optional] Name or ID of the snapshot to run. If not supplied, the published snapshot should be used.",
                v => Snapshot = v);
            // options.Add<bool>("progress", "[Optional] Show progress of the deployment", v => Progress = true);
            options.Add<bool>("forcePackageDownload",
                "[Optional] Whether to force downloading of already installed packages (flag, default false).",
                v => ForcePackageDownload = true);
            options.Add<bool>("guidedFailure=",
                "[Optional] Whether to use Guided Failure mode. (True or False. If not specified, will use default setting from environment)",
                v => GuidedFailure = v);
            options.Add<string>("includedMachineIds=",
                "[Optional] A comma-separated list of machine names to target in the deployed environment. If not specified all machines in the environment will be considered.",
                v => IncludedMachineIds.AddRange(v.Split(new[] {','}, StringSplitOptions.RemoveEmptyEntries)
                    .Select(m => m.Trim())));
            options.Add<string>("excludedMachineIds=",
                "[Optional] A comma-separated list of machine names to exclude in the deployed environment. If not specified all machines in the environment will be considered.",
                v => ExcludeMachineIds.AddRange(v.Split(new[] {','}, StringSplitOptions.RemoveEmptyEntries)
                    .Select(m => m.Trim())));
            options.Add<string>("skip=", "[Optional] Skip a step by name", v => StepsToSkip.Add(v));

            // options.Add<bool>("waitForRun", "[Optional] Whether to wait synchronously for deployment to finish.",
            //     v => WaitForRun = v);
            // options.Add<TimeSpan>("runTimeout=",
            //     "[Optional] Specifies maximum time (timespan format) that the console session will wait for the deployment to finish(default 00:10:00). This will not stop the deployment. Requires -- waitfordeployment parameter set.",
            //     v => RunTimeout = v);
            // options.Add<bool>("cancelOnTimeout",
            //     "[Optional] Whether to cancel the deployment if the deployment timeout is reached (flag, default false).",
            //     v => CancelOnTimeout = true);
            // options.Add<TimeSpan>("runCheckSleepCycle=",
            //     "[Optional] Specifies how much time (timespan format) should elapse between deployment status checks (default 00:00:10)",
            //     v => RunCheckSleepCycle = v);
            // options.Add<bool>("noRawLog", "[Optional] Don't print the raw log of failed tasks", v => NoRawLog = true);
            // options.Add<string>("rawLogFile=", "[Optional] Redirect the raw log of failed tasks to a file",
            //     v => RawLogFile = v);
            // options.Add<string>("v|variable=",
            //     "[Optional] Values for any prompted variables in the format Label:Value. For JSON values, embedded quotation marks should be escaped with a backslash.",
            //     v => ParseVariable);
            // options.Add<DateTimeOffset>("runAt",
            //     "[Optional] Time at which deployment should start (scheduled deployment), specified as any valid DateTimeOffset format, and assuming the time zone is the current local time zone.",
            //     v => RunAt = v);
            // options.Add<DateTimeOffset>("noRunAfter=",
            //     "[Optional] Time at which scheduled deployment should expire, specified as any valid DateTimeOffset format, and assuming the time zone is the current local time zone.",
            //     v => NoRunAfter = v);
            // options.Add<string>("tentant=",
            //     "[Optional] Create a deployment for the tenant with this name or ID; specify this argument multiple times to add multiple tenants or use `*` wildcard to deploy to all tenants who are ready for this release (according to lifecycle).",
            //     v => Tenants.Add(v));
            // options.Add<string>("tenantTag=",
            //     "[Optional] Create a deployment for tenants matching this tag; specify this argument multiple times to build a query/filter with multiple tags, just like you can in the user interface.",
            //     v => TenantTags.Add(v));
        }

        public async Task Request()
        {
        
            // await ValidateParameters();
            var runbookResource = await Repository.Runbooks.FindByNameOrIdOrFail(RunbookNameOrId).ConfigureAwait(false);
            
            //
            var projectResource = await Repository.Projects.FindByNameOrIdOrFail(ProjectNameOrId).ConfigureAwait(false);
            
            
            var environmentResource = await Repository.Environments.FindByNameOrIdOrFail(EnvironmentNameOrId)
                .ConfigureAwait(false);
             
            // Optional Params
            var snapshotId = await RetrieveSnapshotOrFail(runbookResource.PublishedRunbookSnapshotId).ConfigureAwait(false);
            
            
            if (IsTenantedRunbookRun)
            {
                Console.WriteLine("Command issued for Tenanted Runbook :thumbs:");
            }
            else // run on single environment / 
            {
                //TODO check RunAt time is after current time.
                await IssueRunbookRun(runbookResource, environmentResource, projectResource, snapshotId);
            }
        }

        private async Task IssueRunbookRun(
            RunbookResource runbookResource,
            EnvironmentResource environmentResource,
            ProjectResource project, 
            string snapshotId 
        )
        {
            var guidedFailure = GuidedFailure.GetValueOrDefault(environmentResource.UseGuidedFailure);
            var includedMachineIds = await GetIncludedMachineIds().ConfigureAwait(false);
            var excludedMachineIds = await GetExcludeMachineIds().ConfigureAwait(false);
            CheckForIntersection(includedMachineIds.ToList(), excludedMachineIds.ToList());
            
            //TODO get skipActions
            // var runbookPreview = Repository.Runbooks.GetPreview(promotionTarget)
            // var skipActions = GetSkipActions(Repository, preview, StepsToSkip);
            
            var runbookRunResource = new RunbookRunResource
            {
                ProjectId = project.Id,
                RunbookId = runbookResource.Id, //Name: Runbook2 -- ID: Runbooks-2, 
                EnvironmentId = environmentResource.Id, // Environments-1 
                RunbookSnapshotId = snapshotId, // Name: "Snapshot SJPVXN3" ---  Id: RunbookSnapshots-7 (published)
                ForcePackageDownload = ForcePackageDownload,
                UseGuidedFailure = guidedFailure,
                SpecificMachineIds = includedMachineIds,
                ExcludedMachineIds = excludedMachineIds
            };

            var printableIncluded = includedMachineIds.Any() ? includedMachineIds.ToList().ReadableJoin(", ") : "None";
            var printableExcluded = excludedMachineIds.Any() ? excludedMachineIds.ToList().ReadableJoin(", ") : "None";
            
            // Print useful tings -- TODO: Send to the log.
            commandOutputProvider.Information($"Force Package Download: {ForcePackageDownload}");
            commandOutputProvider.Information($"Use Guided Failure: {guidedFailure}");
            commandOutputProvider.Information($"Included machines: {printableIncluded}");
            commandOutputProvider.Information($"Excluded machines: {printableExcluded}");

            // make the actual call to run the runbook
            book = await Repository.Runbooks.Run(runbookResource, runbookRunResource).ConfigureAwait(false);
            Console.WriteLine($"Running {book.Name} at {book.Created}");
        }

        private static void CheckForIntersection(IEnumerable<string> included, IEnumerable<string> excluded)
        {
            var intersection = included.Intersect(excluded);
            if (intersection.Any())
            {
                throw new CommandException($"Cannot specify the same machine as both included and excluded: {intersection.ReadableJoin(", ")}");
            }
        }

        private async Task<ReferenceCollection> GetIncludedMachineIds()
        {
            var specificMachineIds = new ReferenceCollection();
            
            if (!IncludedMachineIds.Any()) return specificMachineIds;
            
            var machines = await Repository.Machines.FindByNames(IncludedMachineIds).ConfigureAwait(false);
            var missing =
                IncludedMachineIds.Except(machines.Select(m => m.Name), StringComparer.OrdinalIgnoreCase).ToList();
            if (missing.Any())
            {
                throw new CouldNotFindException("machine", missing);
            }

            specificMachineIds.AddRange(machines.Select(m => m.Id));
            return specificMachineIds;
        }

        private async Task<ReferenceCollection> GetExcludeMachineIds()
        {
            var excludedMachineIds = new ReferenceCollection();
            if (!ExcludeMachineIds.Any()) return excludedMachineIds;
            var machines = await Repository.Machines.FindByNames(ExcludeMachineIds).ConfigureAwait(false);
            var missing = ExcludeMachineIds.Except(machines.Select(m => m.Name), StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (missing.Any())
            {
                commandOutputProvider.Debug(
                    $"The following machines to excluded could not be found: {missing.ReadableJoin(junction: ", ")}");
            }
        
            excludedMachineIds.AddRange(machines.Select(m => m.Id));
            return excludedMachineIds;
        }

        private async Task<string> RetrieveSnapshotOrFail(string defaultId)
        {
  
            if (Snapshot != null)
            {
                var snapshot = await Repository.RunbookSnapshots.FindByNameOrIdOrFail(Snapshot);
                return snapshot.Id;
            }
            
            if ( defaultId == null)
            {
                throw new CommandException("Could not find a published runbook snapshot to use.");
            }

            commandOutputProvider.Information($"Using Default Snapshot Id: {defaultId}");
            return defaultId;

        }

        private static ReferenceCollection GetSkipActions(IOctopusAsyncRepository repository, DeploymentPreviewBaseResource preview, IEnumerable<string> stepsToSkip)
        {
            // Skip actions - this returns only valid skip actions
            var skippedSteps = new ReferenceCollection();
            foreach (var step in stepsToSkip)
            {
                var stepToExecute = preview.StepsToExecute.SingleOrDefault(s =>
                    string.Equals(s.ActionName, step, StringComparison.CurrentCultureIgnoreCase));
                if (stepToExecute == null)
                {
                    throw new CommandException( // This seems like it needs to be an exception
                        $"The following step: {step} could not be found in the list of runbook steps for this runbook.");
                }
                else
                {
                    skippedSteps.Add(stepToExecute.ActionId);
                }
            }

            return skippedSteps;

        }

        protected override async Task ValidateParameters()
        {
            /*
            * All params are validated up front to prevent a runbook deploy (e.g. to tenants) from partially deploying a running
            */

            // Required params
            if (string.IsNullOrWhiteSpace(RunbookNameOrId))
                throw new CommandException("Please specify a runbook name or ID using the parameter: --runbook=XYZ");
            if (string.IsNullOrWhiteSpace(EnvironmentNameOrId))
                throw new CommandException(
                    "Please specify an environment name or ID using the parameter: --environment=XYZ");
            if (string.IsNullOrWhiteSpace(ProjectNameOrId))
                throw new CommandException("Please specify a project name or ID using the parameter: --project=XYZ");
            
            // Optional params
            if (Tenants.Contains("*") && (Tenants.Count > 1 || TenantTags.Count > 0))
                throw new CommandException(
                    "When running a runbook on all tenants using the wildcard option (e.g. --tenant=*), no other tenant filters can be provided");
            if (IsTenantedRunbookRun && !await Repository.SupportsTenants().ConfigureAwait(false))
                throw new CommandException(
                    "Your Octopus Server does not support tenants, which was introduced in Octopus 3.4. Please upgrade your Octopus Server, enable the multi-tenancy feature or remove the --tenant and --tenantTag arguments.");
            if ((RunAt ?? DateTimeOffset.Now) > NoRunAfter)
                throw new CommandException(
                    "The deployment will expire before it has a chance to execute.  Please select an expiry time that occurs after the deployment is scheduled to begin");

            var tagSetResources = new Dictionary<string, TagSetResource>();

            foreach (var tenantTagParts in TenantTags.Select(tenantTag => tenantTag.Split(Separator)))
            {
                CheckTenantTagFormat(tenantTagParts);
                CollectTagSetResources(tenantTagParts, tagSetResources, Repository);
            }

            var tenantNamesOrIds = Tenants.Where(tn => tn != "*").ToArray();
            if (tenantNamesOrIds.Any())
            {
                await Repository.Tenants.FindByNamesOrIdsOrFail(tenantNamesOrIds).ConfigureAwait(false);
            }

            // confirm included machines are valid - ignore the excluded machines property
            await GetIncludedMachineIds().ConfigureAwait(false);
            await GetExcludeMachineIds().ConfigureAwait(false);
            
            await base.ValidateParameters();
        }

        private void LogScheduledDeployment()
        {
            if (RunAt == null) return;
            var now = DateTimeOffset.UtcNow;
            commandOutputProvider.Information("Deployment will be scheduled to start in: {Duration:l}", (RunAt.Value - now).FriendlyDuration());
        }

        void ParseVariable(string variable)
        {
            var index = new[] {':', '='}.Select(s => variable.IndexOf(s)).Where(i => i > 0).OrderBy(i => i)
                .FirstOrDefault();
            if (index <= 0)
                return;

            var key = variable.Substring(0, index);
            var value = (index >= variable.Length - 1) ? string.Empty : variable.Substring(index + 1);

            variables.Set(key, value);
        }

        public void PrintDefaultOutput()
        {
            //TODO Implement
            // commandOutputProvider.Information("Runbook was run: {Id:l}", book.Name);
        }

        public void PrintJsonOutput()
        {
            commandOutputProvider.Json(new
            {
                // TODO Implement this
                // book.Name,
            });
        }

        private static async void CollectTagSetResources(IReadOnlyList<string> tenantTagParts,
            IDictionary<string, TagSetResource> tagSetResources, IOctopusAsyncRepository repository)
        {
            // Query the api if the results were not previously found 
            if (!tagSetResources.ContainsKey(tenantTagParts[0]))
            {
                tagSetResources.Add(tenantTagParts[0],
                    await repository.TagSets.FindByName(tenantTagParts[0]).ConfigureAwait(false));
            }

            // Verify the presence of the tag
            if (tagSetResources[tenantTagParts[0]]?.Tags?.All(tag => tenantTagParts[1] != tag.Name) ?? true)
            {
                throw new CommandException(
                    $"Unable to find matching tag from canonical tag name '{tenantTagParts.ReadableJoin("/")}'.");
            }
        }


        private static bool CheckTenantTagFormat(string[] tenantTagParts)
        {
            if (tenantTagParts.Length != 2 || tenantTagParts.Any(string.IsNullOrEmpty))
            {
                throw new CommandException(
                    $"Canonical Tag Name expected in the format of `TagSetName{Separator}TagName`");
            }

            return true;
        }

        private async Task<List<TenantResource>> RetrieveTenants(ProjectResource project, string envName, string runbookNameOrId)
        {
            var availableTenants = new List<TenantResource>();
            // if (!Tenants.Any() && !TenantTags.Any()) return availableTenants; // this should never  happen...
                
            if (Tenants.Contains("*")) // get all available tenants for runbook run
            {
                var tenants = await Repository.Tenants.FindAll();
                availableTenants.AddRange(tenants); 
                // TODO Filter tenants by project and environment
                return availableTenants;
            }
            else
            {
                //handle if tenants are provided
                if (Tenants.Any())
                {
                    var tenantsByNameOrId = await Repository.Tenants.FindByNamesOrIdsOrFail(Tenants);
                    availableTenants.AddRange(tenantsByNameOrId);
                    
                    // ensure all tenants are associated with this project - 
                    var unDeployableTenants =
                        availableTenants.Where(dt => !dt.ProjectEnvironments.ContainsKey(project.Id))
                            .Select(dt => $"'{dt.Name}'")
                            .ToList();
                    
                    if (unDeployableTenants.Any())
                        throw new CommandException(
                            $"Runbook '{runbookNameOrId}' in project '{project.Name}' cannot be run on '{(unDeployableTenants.Count == 1 ? "tenant" : "the following tenants")}: {string.Join(" or ", unDeployableTenants)}. This may be because either a) {(unDeployableTenants.Count == 1 ? "it is" : "they are")} not connected to this project, or b) you do not have permission to use {(unDeployableTenants.Count == 1 ? "it" : "them")} for this project."
                        );
                    
                    // TODO correct this block of code to return actual available tenants
                    return availableTenants;

                }
                
                // handle if tenant tags are provided
                if (TenantTags.Any())
                {
                    //TODO get all the tenants based on the tags -- be sure to check if both tenants and tenant tags are supplied
                    return availableTenants;
                }
            }
            return availableTenants;
        }
    }
}