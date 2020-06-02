using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CSharp.RuntimeBinder;
using Octopus.Cli.Commands.Deployment;
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
        private RunbookRunResource book = null;
        readonly VariableDictionary variables = new VariableDictionary();

        // Required arguments
        private string RunbookNameOrId { get; set; }
        private string ProjectNameOrId { get; set; }
        private string EnvironmentNameOrId { get; set; }

        //Optional arguments
        private string Snapshot { get; set; }
        private bool Progress { get; set; }
        private bool ForcePackageDownload { get; set; } = false;
        private bool WaitForRun { get; set; }
        private TimeSpan RunTimeout { get; set; } = TimeSpan.FromMinutes(10);
        private bool CancelOnTimeout { get; set; }
        private TimeSpan RunCheckSleepCycle { get; set; } = TimeSpan.FromSeconds(10);
        private bool? GuidedFailure { get; set; }

        private List<string> SpecificMachines { get; set; }

        private List<string> ExcludeMachines { get; set; }
        private List<string> Skip { get; set; }
        private bool NoRawLog { get; set; }
        private string RawLogFile { get; set; }
        private string Variable { get; set; }
        private DateTimeOffset? RunAt { get; set; }
        private DateTimeOffset? NoRunAfter { get; set; }
        private List<string> Tenants { get; set; }
        private List<string> TenantTags { get; set; }

        public bool IsTenantedRunbookRun { get; set; }
        private const char Separator = '/';

        public RunRunbookCommand(IOctopusAsyncRepositoryFactory repositoryFactory, IOctopusFileSystem fileSystem,
            IOctopusClientFactory clientFactory, ICommandOutputProvider commandOutputProvider) : base(clientFactory,
            repositoryFactory, fileSystem, commandOutputProvider)
        {
            ExcludeMachines = new List<string>();
            SpecificMachines = new List<string>();
            TenantTags = new List<string>();
            Tenants = new List<string>();
            // Skip = new List<string>();


            Console.WriteLine("THIS IS THE NEW COMMAND> TRY IT OUT!");

            var options = Options.For("Run Runbook");
            // required 
            options.Add<string>("runbook=", // TODO can't we use the 
                "Name or ID of the runbook. If the name is supplied, the project parameter must also be specified.",
                v => RunbookNameOrId = v);
            options.Add<string>("project=",
                "Name or ID of the project. This is optional if the runbook argument is an ID",
                v => ProjectNameOrId = v);
            options.Add<string>("environment=",
                "Name or ID of the environment to deploy to, e.g ., 'Production' or 'Environments-1'; specify this argument multiple times to deploy to multiple environments.",
                v => EnvironmentNameOrId = v);
            // optional
            options.Add<string>("snapshot=", // TODO need to get this working correctly
                "[Optional] Name or ID of the snapshot to run. If not supplied, the published snapshot should be used.",
                v => Snapshot = v);
            // options.Add<bool>("progress", "[Optional] Show progress of the deployment", v => Progress = true);
            options.Add<bool>("forcePackageDownload",
                "[Optional] Whether to force downloading of already installed packages (flag, default false).",
                v => ForcePackageDownload = true);
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
            options.Add<bool>("guidedFailure=",
                "[Optional] Whether to use Guided Failure mode. (True or False. If not specified, will use default setting from environment)",
                v => GuidedFailure = v);
            options.Add<string>("specificMachines=",
                "[Optional] A comma-separated list of machine names to target in the deployed environment. If not specified all machines in the environment will be considered.",
                v => SpecificMachines.AddRange(v.Split(new[] {','}, StringSplitOptions.RemoveEmptyEntries)
                    .Select(m => m.Trim())));
            options.Add<string>("excludeMachines",
                "[Optional] A comma-separated list of machine names to exclude in the deployed environment. If not specified all machines in the environment will be considered.",
                v => ExcludeMachines.AddRange(v.Split(new[] {','}, StringSplitOptions.RemoveEmptyEntries)
                    .Select(m => m.Trim())));
            // options.Add<string>("skip=", "[Optional] Skip a step by name", v => Skip.Add(v));
            options.Add<bool>("noRawLog", "[Optional] Don't print the raw log of failed tasks", v => NoRawLog = true);
            // options.Add<string>("rawLogFile=", "[Optional] Redirect the raw log of failed tasks to a file",
            //     v => RawLogFile = v);
            // options.Add<string>("v|variable=",
            //     "[Optional] Values for any prompted variables in the format Label:Value. For JSON values, embedded quotation marks should be escaped with a backslash.",
            //     v => ParseVariable);
            options.Add<DateTimeOffset>("runAt",
                "[Optional] Time at which deployment should start (scheduled deployment), specified as any valid DateTimeOffset format, and assuming the time zone is the current local time zone.",
                v => RunAt = v);
            options.Add<DateTimeOffset>("noRunAfter=",
                "[Optional] Time at which scheduled deployment should expire, specified as any valid DateTimeOffset format, and assuming the time zone is the current local time zone.",
                v => NoRunAfter = v);
            options.Add<string>("tentant=",
                "[Optional] Create a deployment for the tenant with this name or ID; specify this argument multiple times to add multiple tenants or use `*` wildcard to deploy to all tenants who are ready for this release (according to lifecycle).",
                v => Tenants.Add(v));
            options.Add<string>("tenantTag=",
                "[Optional] Create a deployment for tenants matching this tag; specify this argument multiple times to build a query/filter with multiple tags, just like you can in the user interface.",
                v => TenantTags.Add(v));

            // set useful object properties based on args
            this.IsTenantedRunbookRun = (Tenants.Any() || TenantTags.Any());
        }

        private async Task<RunbookResource> FindRunbookByIdOrFail(IOctopusAsyncRepository repository)
        {
            // check by name
            var runbookResource = await repository.Runbooks.FindByName(RunbookNameOrId).ConfigureAwait(false);
            if (runbookResource != null) return runbookResource;

            // check by Id
            var runbookResourceList = await repository.Runbooks.FindAll();
            var filteredResourceArray =
                runbookResourceList.Where(resource => resource.Id == RunbookNameOrId).ToArray();
            if (filteredResourceArray.Count() != 1)
            {
                throw new CommandException(
                    "Runbook name/Id was invalid. Please provide a valid runbook Name or Id");
            }

            return filteredResourceArray[0];
        }

        public async Task Request()
        {
            // TODO: The main function for running the run book task.
            await ValidateParameters(); // TODO This is causing too many 'Found project' or 'Found Environment' messages to display in console

            // REQUIRED PARAMS //
            //checks if project is valid
            var project = await Repository.Projects.FindByNameOrIdOrFail(ProjectNameOrId).ConfigureAwait(false);
            var runbookResource = await FindRunbookByIdOrFail(Repository);

            var envResource = await Repository.Environments.FindByNameOrIdOrFail(EnvironmentNameOrId)
                .ConfigureAwait(false);
            if (envResource == null)
            {
                throw new CommandException(
                    "Environment name/Id was invalid. Please provide a valid environment name or Id.");
            }

            // OPTIONAL PARAMS //
            var includedMachines = await GetIncludedMachines();
            var excludedMachines = await GetExcludedMachines();
            var forcePackageDownload = ForcePackageDownload ? true : false; // TODO does this override snapshot props?
            var guidedFailure = GuidedFailure.GetValueOrDefault(envResource.UseGuidedFailure);

            var snapshotId = // TODO the default snapshot id works, but others doesn't seem to be working with this getTask call.
                await RetrieveSnapshotOrFail(runbookResource
                    .PublishedRunbookSnapshotId); // TODO - we can potentially just return the resource and rerun this snapshot -- check on this (do we need to be abl to modify machines, etc?)


            var deployableTenants = GetTenants(project, )
            
            
                
            // Assemble the run TODO: This will be handled in a function (this is just a reference atm)
            var runbookRunResource = new RunbookRunResource
            {
                ProjectId = project.Id,
                RunbookId = RunbookNameOrId, //"TestRunbook", 
                RunbookSnapshotId = snapshotId,
                EnvironmentId = envResource.Id, //"Environments-1"     
                ExcludedMachineIds = excludedMachines,
                SpecificMachineIds = includedMachines,
                ForcePackageDownload = forcePackageDownload,
                UseGuidedFailure = guidedFailure,
            };
            // TODO Make compatible with multitenancy 
            // // make the actual call to run the runbook
            book = await Repository.Runbooks.Run(runbookResource, runbookRunResource);
        }

        private async Task<string> RetrieveSnapshotOrFail(string defaultId)
        {
            // snapshots -- if not supplied, use the published snapshot -- if no published snapshot -- fail
            var snapshotId =
                (Snapshot != null && await ValidateSpecifiedSnapshot()) ? Snapshot : defaultId; // default could be null
            if (string.IsNullOrWhiteSpace(snapshotId))
            {
                throw new CommandException("No valid runbook snapshot or published snapshot could be found.");
            }

            return snapshotId;
        }

        private async Task<bool> ValidateSpecifiedSnapshot()
        {
            var runbookRunTaskResource =
                await Repository.RunbookRuns.GetTask(new RunbookRunResource() {RunbookSnapshotId = Snapshot});

            try
            {
                //TODO Check that this actually will throw -- I think it actually just returns a 500
                // var runbookRunTaskResource = await Repository.RunbookRuns.GetTask(runbookRunResource);
                if (!runbookRunTaskResource.CanRerun)
                {
                    throw new CommandException("Could not rerun the provided snapshot " + Snapshot);
                }

                return true;
            }
            catch
            {
                throw new CommandException("Could not find the provided snapshot " + Snapshot);
            }
        }

        private async Task<ReferenceCollection> GetIncludedMachines()
        {
            var includedMachineIds = new ReferenceCollection();
            if (!SpecificMachines.Any()) return includedMachineIds;
            var machines = await Repository.Machines.FindByNames(SpecificMachines).ConfigureAwait(false);
            var missing = SpecificMachines.Except(machines.Select(m => m.Name), StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (missing.Any())
            {
                commandOutputProvider.Debug(
                    $"The following specified machines could not be found: {missing.ReadableJoin(junction: ", ")}");
            }

            includedMachineIds.AddRange(machines.Select(m => m.Id));

            return includedMachineIds;
        }

        private async Task<ReferenceCollection> GetExcludedMachines()
        {
            var excludedMachineIds = new ReferenceCollection();
            if (!ExcludeMachines.Any()) return excludedMachineIds;
            var machines = await Repository.Machines.FindByNames(ExcludeMachines).ConfigureAwait(false);
            var missing = ExcludeMachines.Except(machines.Select(m => m.Name), StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (missing.Any())
            {
                commandOutputProvider.Debug(
                    $"The following excluded machines could not be found: {missing.ReadableJoin(junction: ", ")}");
            }

            excludedMachineIds.AddRange(machines.Select(m => m.Id));
            return excludedMachineIds;
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
            commandOutputProvider.Information("Runbook was run: {Id:l}", book.Name);
        }

        public void PrintJsonOutput()
        {
            commandOutputProvider.Json(new
            {
                // TODO Implement this
                book.Name,
            });
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

            await Repository.Projects.FindByNameOrIdOrFail(ProjectNameOrId).ConfigureAwait(false);
            await Repository.Environments.FindByNameOrIdOrFail(EnvironmentNameOrId).ConfigureAwait(false);
            if (await Repository.Runbooks.FindByName(RunbookNameOrId).ConfigureAwait(false) == null)
            {
                throw new CommandException("Runbook name/Id was invalid. Please provide a valid runbook Name or Id");
            }

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
            await GetIncludedMachines();

            await base.ValidateParameters();
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
            if (!Tenants.Any() && !TenantTags.Any()) return deployableTenants; // early return if no tenants or tags
                
            if (Tenants.Contains("*")) // get all available tenants
            {
                var tenants = await Repository.Tenants.FindAll();
                availableTenants.AddRange(tenants); // TODO Filter tenants by project and environment
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
                            $"Runbook '{runbookNameOrId}' in project '{project.Name}' cannot be run on'{(unDeployableTenants.Count == 1 ? "" : " the following")} tenant{(unDeployableTenants.Count == 1 ? "" : "s")} {string.Join(" or ", unDeployableTenants)}. This may be because either a) {(unDeployableTenants.Count == 1 ? "it is" : "they are")} not connected to this project, or b) you do not have permission to deploy {(unDeployableTenants.Count == 1 ? "it" : "them")} to this project."
                        );
                    
                }
                
                // handle if tenant tags are provided
                if (TenantTags.Any())
                {
                    //TODO get all the tenants based on the tags -- be sure to check if both tenants and tenant tags are supplied
                }
            }
        }
    }
}