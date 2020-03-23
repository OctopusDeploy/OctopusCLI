﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Octopus.Cli.Infrastructure;
using Octopus.Cli.Repositories;
using Octopus.Cli.Util;
using Octopus.Client;
using Octopus.Client.Model;
using Octopus.Client.Model.Forms;
using Octostache;

namespace Octopus.Cli.Commands.Deployment
{
    public abstract class DeploymentCommandBase : ApiCommand
    {
        private const char Separator = '/'; 
        readonly VariableDictionary variables = new VariableDictionary();
        protected IReadOnlyList<DeploymentResource> deployments;
        protected List<DeploymentPromotionTarget> promotionTargets;
        protected List<TenantResource> deploymentTenants;

        protected DeploymentCommandBase(IOctopusAsyncRepositoryFactory repositoryFactory, IOctopusFileSystem fileSystem, IOctopusClientFactory clientFactory, ICommandOutputProvider commandOutputProvider)
            : base(clientFactory, repositoryFactory, fileSystem, commandOutputProvider)
        {
            SpecificMachineNames = new List<string>();
            ExcludedMachineNames = new List<string>();
            SkipStepNames = new List<string>();
            DeployToEnvironmentNamesOrIds = new List<string>();
            TenantTags = new List<string>();
            Tenants = new List<string>();
            promotionTargets = new List<DeploymentPromotionTarget>();

            var options = Options.For("Deployment");
            options.Add("progress", "[Optional] Show progress of the deployment", v => { showProgress = true; WaitForDeployment = true; noRawLog = true; });
            options.Add("forcePackageDownload", "[Optional] Whether to force downloading of already installed packages (flag, default false).", v => ForcePackageDownload = true);
            options.Add("waitForDeployment", "[Optional] Whether to wait synchronously for deployment to finish.", v => WaitForDeployment = true);
            options.Add("deploymentTimeout=", "[Optional] Specifies maximum time (timespan format) that the console session will wait for the deployment to finish(default 00:10:00). This will not stop the deployment. Requires --waitForDeployment parameter set.", v => DeploymentTimeout = TimeSpan.Parse(v));
            options.Add("cancelOnTimeout", "[Optional] Whether to cancel the deployment if the deployment timeout is reached (flag, default false).", v => CancelOnTimeout = true);
            options.Add("deploymentCheckSleepCycle=", "[Optional] Specifies how much time (timespan format) should elapse between deployment status checks (default 00:00:10)", v => DeploymentStatusCheckSleepCycle = TimeSpan.Parse(v));
            options.Add("guidedFailure=", "[Optional] Whether to use guided failure mode. (True or False. If not specified, will use default setting from environment)", v => UseGuidedFailure = bool.Parse(v));
            options.Add("specificMachines=", "[Optional] A comma-separated list of machine names to target in the deployed environment. If not specified all machines in the environment will be considered.", v => SpecificMachineNames.AddRange(v.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(m => m.Trim())));
            options.Add("excludeMachines=", "[Optional] A comma-separated list of machine names to exclude in the deployed environment. If not specified all machines in the environment will be considered.", v => ExcludedMachineNames.AddRange(v.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(m => m.Trim())));
            options.Add("force", "[Optional] If a project is configured to skip packages with already-installed versions, override this setting to force re-deployment (flag, default false).", v => ForcePackageRedeployment = true);
            options.Add("skip=", "[Optional] Skip a step by name", v => SkipStepNames.Add(v));
            options.Add("noRawLog", "[Optional] Don't print the raw log of failed tasks", v => noRawLog = true);
            options.Add("rawLogFile=", "[Optional] Redirect the raw log of failed tasks to a file", v => rawLogFile = v);
            options.Add("v|variable=", "[Optional] Values for any prompted variables in the format Label:Value. For JSON values, embedded quotation marks should be escaped with a backslash.", ParseVariable);
            options.Add("deployAt=", "[Optional] Time at which deployment should start (scheduled deployment), specified as any valid DateTimeOffset format, and assuming the time zone is the current local time zone.", v => DeployAt = ParseDateTimeOffset(v));
            options.Add("noDeployAfter=", "[Optional] Time at which scheduled deployment should expire, specified as any valid DateTimeOffset format, and assuming the time zone is the current local time zone.", v => NoDeployAfter = ParseDateTimeOffset(v));
            options.Add("tenant=", "Create a deployment for the tenant with this name or ID; specify this argument multiple times to add multiple tenants or use `*` wildcard to deploy to all tenants who are ready for this release (according to lifecycle).", t => Tenants.Add(t));
            options.Add("tenantTag=", "Create a deployment for tenants matching this tag; specify this argument multiple times to build a query/filter with multiple tags, just like you can in the user interface.", tt => TenantTags.Add(tt));
        }

        protected bool ForcePackageRedeployment { get; set; }
        protected bool ForcePackageDownload { get; set; }
        protected bool? UseGuidedFailure { get; set; }
        protected bool WaitForDeployment { get; set; }
        protected TimeSpan DeploymentTimeout { get; set; } = TimeSpan.FromMinutes(10);
        protected bool CancelOnTimeout { get; set; }
        protected TimeSpan DeploymentStatusCheckSleepCycle { get; set; } = TimeSpan.FromSeconds(10);
        protected List<string> SpecificMachineNames { get; set; }
        protected List<string> ExcludedMachineNames { get; set; }
        protected List<string> SkipStepNames { get; set; }
        protected DateTimeOffset? DeployAt { get; set; }
        protected DateTimeOffset? NoDeployAfter { get; set; }
        public string ProjectNameOrId { get; set; }
        public List<string> DeployToEnvironmentNamesOrIds { get; set; }
        public List<string> Tenants { get; set; }
        public List<string> TenantTags { get; set; }

        private bool IsTenantedDeployment => (Tenants.Any() || TenantTags.Any());

        bool noRawLog;
        bool showProgress;
        string rawLogFile;
        TaskOutputProgressPrinter printer = new TaskOutputProgressPrinter();

        protected override async Task ValidateParameters()
        {
            if (string.IsNullOrWhiteSpace(ProjectNameOrId)) throw new CommandException("Please specify a project name or ID using the parameter: --project=XYZ");
            if (IsTenantedDeployment && DeployToEnvironmentNamesOrIds.Count > 1) throw new CommandException("Please specify only one environment at a time when deploying to tenants.");
            if (Tenants.Contains("*") && (Tenants.Count > 1 || TenantTags.Count > 0)) throw new CommandException("When deploying to all tenants using --tenant=* wildcard no other tenant filters can be provided");
            if (IsTenantedDeployment && !await Repository.SupportsTenants().ConfigureAwait(false))
                throw new CommandException("Your Octopus Server does not support tenants, which was introduced in Octopus 3.4. Please upgrade your Octopus Server, enable the multi-tenancy feature or remove the --tenant and --tenantTag arguments.");
            if ((DeployAt ?? DateTimeOffset.Now) > NoDeployAfter)
                throw new CommandException("The deployment will expire before it has a chance to execute.  Please select an expiry time that occurs after the deployment is scheduled to begin");

            /*
             * A create release operation can also optionally deploy the release, however any invalid options that
             * are specific only to the deployment will fail after the release has been created. This can leave
             * a deployment in a half finished state, so this validation ensures that the input relating to the
             * deployment is valid so missing or incorrect input doesn't stop stop the deployment after a release is
             * created.
             *
             * Note that certain validations still need to be done on the server. Permissions and lifecycle progression
             * still rely on server side validation.
             */
            
            // We might query the same tagset repeatedly, so store old queries here
            var tagSetResources = new Dictionary<string, TagSetResource>();
            // Make sure the tags are valid
            foreach (var tenantTag in TenantTags)
            {
                // Verify the format of the tag
                var parts = tenantTag.Split(Separator);
                if (parts.Length != 2 || string.IsNullOrEmpty(parts[0]) || string.IsNullOrEmpty(parts[1]))
                {
                    throw new CommandException(
                        $"Canonical Tag Name expected in the format of `TagSetName{Separator}TagName`");
                }
                
                // Query the api if the results were not previously found 
                if (!tagSetResources.ContainsKey(parts[0]))
                {
                    tagSetResources.Add(parts[0], await Repository.TagSets.FindByName(parts[0]).ConfigureAwait(false));
                } 

                // Verify the presence of the tag
                if (tagSetResources[parts[0]]?.Tags?.All(tag => parts[1] != tag.Name) ?? true)
                {
                    throw new CommandException(
                        $"Unable to find matching tag from canonical tag name '{tenantTag}'.");
                }
            }

            // Make sure the tenants are valid
            var tenantNamesOrIds = Tenants.Where(tn => tn != "*").ToArray();
            if (tenantNamesOrIds.Any())
            {
                await Repository.Tenants.FindByNamesOrIdsOrFail(tenantNamesOrIds).ConfigureAwait(false);
            }

            // Make sure environment is valid
            await Repository.Environments.FindByNamesOrIdsOrFail(DeployToEnvironmentNamesOrIds).ConfigureAwait(false);

            // Make sure the machines are valid
            await GetSpecificMachines();

            await base.ValidateParameters();
        }

        private DateTimeOffset ParseDateTimeOffset(string v)
        {
            try
            {
                return DateTimeOffset.Parse(v, CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal);
            }
            catch (FormatException fex)
            {
                throw new CommandException($"Could not convert '{v}' to a DateTimeOffset: {fex.Message}");
            }
        }

        private async Task<IReadOnlyList<DeploymentResource>> DeployTenantedRelease(ProjectResource project, ReleaseResource release)
        {
            if (DeployToEnvironmentNamesOrIds.Count != 1)
                return new List<DeploymentResource>();

            var environment = await Repository.Environments.FindByNameOrIdOrFail(DeployToEnvironmentNamesOrIds[0]).ConfigureAwait(false);
            var releaseTemplate = await Repository.Releases.GetTemplate(release).ConfigureAwait(false);
            
            deploymentTenants = await GetTenants(project, environment.Name, release, releaseTemplate).ConfigureAwait(false);
            var specificMachineIds = await GetSpecificMachines().ConfigureAwait(false);
            var excludedMachineIds = await GetExcludedMachines().ConfigureAwait(false);

            LogScheduledDeployment();
            
            var createTasks = deploymentTenants.Select(tenant =>
            {
                var promotion =
                    releaseTemplate.TenantPromotions
                        .First(t => t.Id == tenant.Id)
                        .PromoteTo
                        .First(tt => tt.Name.Equals(environment.Name, StringComparison.OrdinalIgnoreCase));
                promotionTargets.Add(promotion);
                return CreateDeploymentTask(project, release, promotion, specificMachineIds, excludedMachineIds, tenant);
            });

            return await Task.WhenAll(createTasks).ConfigureAwait(false);
        }

        private void LogScheduledDeployment()
        {
            if (DeployAt != null)
            {
                var now = DateTimeOffset.UtcNow;
                commandOutputProvider.Information("Deployment will be scheduled to start in: {Duration:l}", (DeployAt.Value - now).FriendlyDuration());
            }
        }

        private async Task<ReferenceCollection> GetSpecificMachines()
        {
            var specificMachineIds = new ReferenceCollection();
            if (SpecificMachineNames.Any())
            {
                var machines = await Repository.Machines.FindByNames(SpecificMachineNames).ConfigureAwait(false);
                var missing =
                    SpecificMachineNames.Except(machines.Select(m => m.Name), StringComparer.OrdinalIgnoreCase).ToList();
                if (missing.Any())
                {
                    throw new CouldNotFindException("machine", missing);
                }

                specificMachineIds.AddRange(machines.Select(m => m.Id));
            }
            return specificMachineIds;
        }

        private async Task<ReferenceCollection> GetExcludedMachines()
        {
            var excludedMachineIds = new ReferenceCollection();
            if (ExcludedMachineNames.Any())
            {
                var machines = await Repository.Machines.FindByNames(ExcludedMachineNames).ConfigureAwait(false);
                var missing = ExcludedMachineNames
                    .Except(machines.Select(m => m.Name), StringComparer.OrdinalIgnoreCase).ToList();
                if (missing.Any())
                {
                    commandOutputProvider.Debug($"The following excluded machines could not be found: {missing.ReadableJoin()}");
                }

                excludedMachineIds.AddRange(machines.Select(m => m.Id));
            }
            return excludedMachineIds;
        }

        protected async Task DeployRelease(ProjectResource project, ReleaseResource release)
        {
            var deploymentsTask = IsTenantedDeployment ?
                DeployTenantedRelease(project, release) : 
                DeployToEnvironments(project, release);
            
            deployments = await deploymentsTask.ConfigureAwait(false);
            if (deployments.Any() && WaitForDeployment)
            {
                await WaitForDeploymentToComplete(deployments, project, release).ConfigureAwait(false);
            }
        }

        private async Task<IReadOnlyList<DeploymentResource>> DeployToEnvironments(ProjectResource project, ReleaseResource release)
        {
            if (DeployToEnvironmentNamesOrIds.Count == 0)
                return new List<DeploymentResource>();

            var releaseTemplate = await Repository.Releases.GetTemplate(release).ConfigureAwait(false);

            var deployToEnvironments = await Repository.Environments
                .FindByNamesOrIdsOrFail(DeployToEnvironmentNamesOrIds.Distinct(StringComparer.OrdinalIgnoreCase))
                .ConfigureAwait(false);
            var promotingEnvironments = deployToEnvironments.Select(environment => new
            {
                environment.Name,
                Promotion = releaseTemplate.PromoteTo
                    .FirstOrDefault(p => string.Equals(p.Name, environment.Name, StringComparison.OrdinalIgnoreCase))
            }).ToList();

            var unknownEnvironments = promotingEnvironments.Where(p => p.Promotion == null).ToList();
            if (unknownEnvironments.Count > 0)
            {
                throw new CommandException(
                    string.Format(
                        "Release '{0}' of project '{1}' cannot be deployed to {2} not in the list of environments that this release can be deployed to. This may be because a) the environment does not exist or is misspelled, b) The lifecycle has not reached this phase, possibly due to previous deployment failure, c) you don't have permission to deploy to this environment, or d) the environment is not in the list of environments defined by the lifecycle.",
                        release.Version,
                        project.Name,
                        unknownEnvironments.Count == 1
                            ? "environment '" + unknownEnvironments[0].Name + "' because the environment is"
                            : "environments " + string.Join(", ", unknownEnvironments.Select(e => "'" + e.Name + "'")) +
                              " because the environments are"
                        ));
            }

            LogScheduledDeployment();
            var specificMachineIds = await GetSpecificMachines().ConfigureAwait(false);
            var excludedMachineIds = await GetExcludedMachines().ConfigureAwait(false);

            var createTasks = promotingEnvironments.Select(promotion => CreateDeploymentTask(project, release, promotion.Promotion, specificMachineIds, excludedMachineIds));
            return await Task.WhenAll(createTasks).ConfigureAwait(false);
        }

        private async Task<List<TenantResource>> GetTenants(ProjectResource project, string environmentName, ReleaseResource release,
            DeploymentTemplateResource releaseTemplate)
        {
            if (!Tenants.Any() && !TenantTags.Any())
            {
                return new List<TenantResource>();
            }

            var deployableTenants = new List<TenantResource>();

            if (Tenants.Contains("*"))
            {
                var tenantPromotions = releaseTemplate.TenantPromotions.Where(
                    tp => tp.PromoteTo.Any(
                        promo => promo.Name.Equals(environmentName, StringComparison.OrdinalIgnoreCase))).Select(tp => tp.Id).ToArray();

                var tentats = await Repository.Tenants.Get(tenantPromotions).ConfigureAwait(false);
                deployableTenants.AddRange(tentats);

                commandOutputProvider.Information("Found {NumberOfTenants} Tenants who can deploy {Project:l} {Version:l} to {Environment:l}", deployableTenants.Count, project.Name,release.Version, environmentName);
            }
            else
            {
                if (Tenants.Any())
                {
                    var tenantsByNameOrId = await Repository.Tenants.FindByNamesOrIdsOrFail(Tenants);
                    deployableTenants.AddRange(tenantsByNameOrId);

                    var unDeployableTenants =
                        deployableTenants.Where(dt => !dt.ProjectEnvironments.ContainsKey(project.Id))
                            .Select(dt => $"'{dt.Name}'")
                            .ToList();
                    if (unDeployableTenants.Any())
                        throw new CommandException(
                            string.Format(
                                "Release '{0}' of project '{1}' cannot be deployed for tenant{2} {3}. This may be because either a) {4} not connected to this project, or b) you do not have permission to deploy {5} to this project.",
                                release.Version,
                                project.Name,
                                unDeployableTenants.Count == 1 ? "" : "s",
                                string.Join(" or ", unDeployableTenants),
                                unDeployableTenants.Count == 1 ? "it is" : "they are",
                                unDeployableTenants.Count == 1 ? "it" : "them"));

                    unDeployableTenants = deployableTenants.Where(dt =>
                    {
                        var tenantPromo = releaseTemplate.TenantPromotions.FirstOrDefault(tp => tp.Id == dt.Id);
                        return tenantPromo == null ||
                               !tenantPromo.PromoteTo.Any(
                                   tdt => tdt.Name.Equals(environmentName, StringComparison.OrdinalIgnoreCase));
                    }).Select(dt => $"'{dt.Name}'").ToList();
                    if (unDeployableTenants.Any())
                    {
                        throw new CommandException(
                            string.Format(
                                "Release '{0}' of project '{1}' cannot be deployed for tenant{2} {3} to environment '{4}'. This may be because a) the tenant{2} {5} not connected to this environment, a) the environment does not exist or is misspelled, b) The lifecycle has not reached this phase, possibly due to previous deployment failure,  c) you don't have permission to deploy to this environment, d) the environment is not in the list of environments defined by the lifecycle, or e) {6} unable to deploy to this channel.",
                                release.Version,
                                project.Name,
                                unDeployableTenants.Count == 1 ? "" : "s",
                                string.Join(" or ", unDeployableTenants),
                                environmentName,
                                unDeployableTenants.Count == 1 ? "is" : "are",
                                unDeployableTenants.Count == 1 ? "it is" : "they are"));
                    }
                }

                if (TenantTags.Any())
                {

                    var tenantsByTag = await Repository.Tenants.FindAll(null, TenantTags.ToArray()).ConfigureAwait(false);
                    var deployableByTag = tenantsByTag.Where(dt =>
                    {
                        var tenantPromo = releaseTemplate.TenantPromotions.FirstOrDefault(tp => tp.Id == dt.Id);
                        return tenantPromo != null && tenantPromo.PromoteTo.Any(tdt => tdt.Name.Equals(environmentName, StringComparison.OrdinalIgnoreCase));
                    }).Where(tenant => !deployableTenants.Any(deployable => deployable.Id == tenant.Id));
                    deployableTenants.AddRange(deployableByTag);
                }
            }

            if (!deployableTenants.Any())
                throw new CommandException(
                    string.Format(
                        "No tenants are available to be deployed for release '{0}' of project '{1}' to environment '{2}'.  This may be because a) No tenants matched the tags provided b) The tenants that do match are not connected to this project or environment, c) The tenants that do match are not yet able to release to this lifecycle phase, or d) you do not have the appropriate deployment permissions.",
                        release.Version, project.Name, environmentName));


            return deployableTenants;
        }

        private async Task<DeploymentResource> CreateDeploymentTask(ProjectResource project, ReleaseResource release, DeploymentPromotionTarget promotionTarget, ReferenceCollection specificMachineIds, ReferenceCollection excludedMachineIds, TenantResource tenant = null)
        {
            var preview = await Repository.Releases.GetPreview(promotionTarget).ConfigureAwait(false);

            // Validate skipped steps
            var skip = new ReferenceCollection();
            foreach (var step in SkipStepNames)
            {
                var stepToExecute =
                    preview.StepsToExecute.SingleOrDefault(s => string.Equals(s.ActionName, step, StringComparison.CurrentCultureIgnoreCase));
                if (stepToExecute == null)
                {
                    commandOutputProvider.Warning("No step/action named '{Step:l}' could be found when deploying to environment '{Environment:l}', so the step cannot be skipped.", step, promotionTarget.Name);
                }
                else
                {
                    commandOutputProvider.Debug("Skipping step: {Step:l}", stepToExecute.ActionName);
                    skip.Add(stepToExecute.ActionId);
                }
            }

            // Validate form values supplied
            if (preview.Form != null && preview.Form.Elements != null && preview.Form.Values != null)
            {
                foreach (var element in preview.Form.Elements)
                {
                    var variableInput = element.Control as VariableValue;
                    if (variableInput == null)
                    {
                        continue;
                    }

                    var value = variables.Get(variableInput.Label) ?? variables.Get(variableInput.Name);

                    if (string.IsNullOrWhiteSpace(value) && element.IsValueRequired)
                    {
                        throw new ArgumentException("Please provide a variable for the prompted value " + variableInput.Label);
                    }

                    preview.Form.Values[element.Name] = value;
                }
            }

            // Log step with no machines
            foreach (var previewStep in preview.StepsToExecute)
            {
                if (previewStep.HasNoApplicableMachines)
                {
                    commandOutputProvider.Warning("Warning: there are no applicable machines roles used by step {Step:l}", previewStep.ActionName);
                }
            }

            promotionTargets.Add(promotionTarget);
            var deployment = await Repository.Deployments.Create(new DeploymentResource
            {
                TenantId = tenant?.Id,
                EnvironmentId = promotionTarget.Id,
                SkipActions = skip,
                ReleaseId = release.Id,
                ForcePackageDownload = ForcePackageDownload,
                UseGuidedFailure = UseGuidedFailure.GetValueOrDefault(preview.UseGuidedFailureModeByDefault),
                SpecificMachineIds = specificMachineIds,
                ExcludedMachineIds = excludedMachineIds,
                ForcePackageRedeployment = ForcePackageRedeployment,
                FormValues = (preview.Form ?? new Form()).Values,
                QueueTime = DeployAt,
                QueueTimeExpiry = NoDeployAfter
            })
            .ConfigureAwait(false);

            commandOutputProvider.Information("Deploying {Project:l} {Release:} to: {PromotionTarget:l} {Tenant:l}(Guided Failure: {GuidedFailure:l})", project.Name, release.Version, promotionTarget.Name,
                tenant == null ? string.Empty : $"for {tenant.Name} ",
                deployment.UseGuidedFailure ? "Enabled" : "Not Enabled");

            return deployment;
        }

        public async Task WaitForDeploymentToComplete(IReadOnlyList<DeploymentResource> deployments, ProjectResource project, ReleaseResource release)
        {
            var getTasks = deployments.Select(dep => Repository.Tasks.Get(dep.TaskId));
            var deploymentTasks = await Task.WhenAll(getTasks).ConfigureAwait(false);
            if (showProgress && deployments.Count > 1)
            {
                commandOutputProvider.Information("Only progress of the first task ({Task:l}) will be shown", deploymentTasks.First().Name);
            }

            try
            {
                commandOutputProvider.Information("Waiting for {NumberOfTasks} deployment(s) to complete....", deploymentTasks.Length);
                await Repository.Tasks.WaitForCompletion(deploymentTasks.ToArray(), DeploymentStatusCheckSleepCycle.Seconds, DeploymentTimeout, PrintTaskOutput).ConfigureAwait(false);
                var failed = false;
                foreach (var deploymentTask in deploymentTasks)
                {
                    var updated = await Repository.Tasks.Get(deploymentTask.Id).ConfigureAwait(false);
                    if (updated.FinishedSuccessfully)
                    {
                        commandOutputProvider.Information("{Task:l}: {State}", updated.Description, updated.State);
                    }
                    else
                    {
                        commandOutputProvider.Error("{Task:l}: {State}, {Error:l}", updated.Description, updated.State, updated.ErrorMessage);
                        failed = true;

                        if (noRawLog)
                        {
                            continue;
                        }

                        try
                        {
                            var raw = await Repository.Tasks.GetRawOutputLog(updated).ConfigureAwait(false);
                            if (!string.IsNullOrEmpty(rawLogFile))
                            {
                                File.WriteAllText(rawLogFile, raw);
                            }
                            else
                            {
                                commandOutputProvider.Error(raw);
                            }
                        }
                        catch (Exception ex)
                        {
                            commandOutputProvider.Error(ex, "Could not retrieve the raw task log for the failed task.");
                        }
                    }
                }
                if (failed)
                {
                    throw new CommandException("One or more deployment tasks failed.");
                }

                commandOutputProvider.Information("Done!");
            }
            catch (TimeoutException e)
            {
                commandOutputProvider.Error(e.Message);

                await CancelDeploymentOnTimeoutIfRequested(deploymentTasks).ConfigureAwait(false);

                var guidedFailureDeployments =
                    from d in deployments
                    where d.UseGuidedFailure
                    select d;
                if (guidedFailureDeployments.Any())
                {
                    commandOutputProvider.Warning("One or more deployments are using guided failure. Use the links below to check if intervention is required:");
                    foreach (var guidedFailureDeployment in guidedFailureDeployments)
                    {
                        var environment = await Repository.Environments.Get(guidedFailureDeployment.Link("Environment")).ConfigureAwait(false);
                        commandOutputProvider.Warning("  - {Environment:l}: {Url:l}", environment.Name, GetPortalUrl(string.Format("/app#/projects/{0}/releases/{1}/deployments/{2}", project.Slug, release.Version, guidedFailureDeployment.Id)));
                    }
                }
                throw new CommandException(e.Message);
            }
        }

        private Task CancelDeploymentOnTimeoutIfRequested(IReadOnlyList<TaskResource> deploymentTasks)
        {
            if (!CancelOnTimeout)
                return Task.WhenAll();

            var tasks = deploymentTasks.Select(async task => {
                commandOutputProvider.Warning("Cancelling deployment task '{Task:l}'", task.Description);
                try
                {
                    await Repository.Tasks.Cancel(task).ConfigureAwait(false);
                }
                catch(Exception ex)
                {
                    commandOutputProvider.Error("Failed to cancel deployment task '{Task:l}': {ExceptionMessage:l}", task.Description, ex.Message);
                }
            });
            return Task.WhenAll(tasks);
        }

        Task PrintTaskOutput(TaskResource[] taskResources)
        {
            var task = taskResources.First();
            return printer.Render(Repository, commandOutputProvider, task);
        }

        void ParseVariable(string variable)
        {
            var index = new[] { ':', '=' }.Select(s => variable.IndexOf(s)).Where(i => i > 0).OrderBy(i => i).FirstOrDefault();
            if (index <= 0)
                return;

            var key = variable.Substring(0, index);
            var value = (index >= variable.Length - 1) ? string.Empty : variable.Substring(index + 1);

            variables.Set(key, value);
        }
    }
}
