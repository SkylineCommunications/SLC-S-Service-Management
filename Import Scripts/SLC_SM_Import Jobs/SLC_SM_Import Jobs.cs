/*
****************************************************************************
*  Copyright (c),  Skyline Communications NV  All Rights Reserved.    *
****************************************************************************

Revision History:

DATE        VERSION        AUTHOR            COMMENTS

02/09/2026  1.0.0.1        SKA           Initial version
04/09/2026  1.0.0.2        SKA           Removed Plan API dependency. Resource references now resolve through ResourceStudioHelper; Service links resolve through ServiceManagement repository IDs.

Import order for the Service Management demo dataset:
	1. Resource Studio  (resource pools, resources, capabilities, capacities)
	2. Workflows        (workflow templates that jobs are instantiated from)
	3. Jobs             (this script)

Jobs are created as DOM instances in the '(slc)workflow' module using the
public MediaOps Scheduling helper (Skyline.DataMiner.Utils.MediaOps.Helpers.
Scheduling.SchedulingHelper), the same proven DOM-based creation path as the
'ResourceScheduling_General_Demo Setup_Jobs' demo. Each job is instantiated
from its referenced Workflow (resolved by name to its DOM instance id), with
one node per node reservation (resource pool nodes, or resource nodes when a
specific resource is booked). Resource pools and resources are resolved by
name to their DOM instance ids through the Resource Studio helper API. After creation, the job
state is advanced (tentative/confirmed/running/completed) through the job
action pipeline, which is what triggers the actual resource booking on a real
Agent. Jobs are additionally linked to their Service (from the Service
Inventory import) through the MediaOps relationship helper.
****************************************************************************
*/
namespace SLC_SM_Import_Jobs
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.IO;
	using System.Linq;
	using System.Text;

	using Newtonsoft.Json;
	using Newtonsoft.Json.Linq;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.ServiceManagement;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM;
	using Skyline.DataMiner.Utils.MediaOps.Helpers.Relationships;

	using ResourceStudio = Skyline.DataMiner.Utils.MediaOps.Helpers.ResourceStudio;
	using Scheduling = Skyline.DataMiner.Utils.MediaOps.Helpers.Scheduling;
	using ServiceModels = Skyline.DataMiner.ProjectApi.ServiceManagement.API.ServiceManagement.Models;
	using Workflows = Skyline.DataMiner.Utils.MediaOps.Helpers.Workflows;

	/// <summary>
	/// Represents a DataMiner Automation script.
	/// </summary>
	public class Script
	{
		private const string DefaultJobsJsonPath = @"C:\Skyline DataMiner\Documents\DMA_COMMON_DOCUMENTS\jobs.json";
		private const string DefaultWorkflowsJsonPath = @"C:\Skyline DataMiner\Documents\DMA_COMMON_DOCUMENTS\workflows.json";
		private const string DefaultResourceStudioJsonPath = @"C:\Skyline DataMiner\Documents\DMA_COMMON_DOCUMENTS\resource-studio.json";
		private const string DefaultServiceInventoryJsonPath = @"C:\Skyline DataMiner\Documents\DMA_COMMON_DOCUMENTS\service-inventory.json";

		/// <summary>
		/// The script entry point.
		/// </summary>
		/// <param name="engine">Link with SLAutomation process.</param>
		public void Run(IEngine engine)
		{
			try
			{
				RunSafe(engine);
			}
			catch (ScriptAbortException)
			{
				// Catch normal abort exceptions (engine.ExitFail or engine.ExitSuccess)
			}
			catch (ScriptForceAbortException)
			{
				// Catch forced abort exceptions, caused via external maintenance messages.
			}
			catch (ScriptTimeoutException)
			{
				// Catch timeout exceptions for when a script has been running for too long.
			}
			catch (InteractiveUserDetachedException)
			{
				// Catch a user detaching from the interactive script by closing the window.
			}
			catch (Exception e)
			{
				engine.ExitFail($"Run|{e.Message}");
			}
		}

		private static void RunSafe(IEngine engine)
		{
			string jobsJsonPath = ResolvePath(engine, "JSON File Path", DefaultJobsJsonPath);
			string workflowsJsonPath = ResolvePath(engine, "Workflows JSON File Path", DefaultWorkflowsJsonPath);
			string resourceStudioJsonPath = ResolvePath(engine, "Resource Studio JSON File Path", DefaultResourceStudioJsonPath);
			string serviceInventoryJsonPath = ResolvePath(engine, "Service Inventory JSON File Path", DefaultServiceInventoryJsonPath);

			if (!File.Exists(jobsJsonPath))
			{
				throw new FileNotFoundException($"JSON import file was not found: '{jobsJsonPath}'");
			}

			var root = JsonConvert.DeserializeObject<JobsRoot>(File.ReadAllText(jobsJsonPath));
			if (root?.Jobs == null || root.Jobs.Count == 0)
			{
				throw new InvalidOperationException($"No jobs were found in '{jobsJsonPath}'.");
			}

			var warnings = new List<string>();

			Dictionary<string, string> workflowNameByTemplateId = ReadWorkflowNames(workflowsJsonPath, warnings);
			ResourceStudioMaps resourceMaps = ReadResourceStudioMaps(resourceStudioJsonPath, warnings);
			Dictionary<string, ServiceInventoryEntry> serviceInventoryById = ReadServiceInventory(serviceInventoryJsonPath, warnings);

			var schedulingHelper = new Scheduling.SchedulingHelper(engine);

			var workflowIdByName = new Dictionary<string, Guid>(StringComparer.InvariantCultureIgnoreCase);
			foreach (var workflow in new Workflows.WorkflowHelper(engine).GetAllWorkflows())
			{
				if (workflow != null && !String.IsNullOrWhiteSpace(workflow.Name) && !workflowIdByName.ContainsKey(workflow.Name))
				{
					workflowIdByName[workflow.Name] = workflow.Id;
				}
			}

			var resourcePoolIdByName = new Dictionary<string, Guid>(StringComparer.InvariantCultureIgnoreCase);
			var resourceIdByName = new Dictionary<string, Guid>(StringComparer.InvariantCultureIgnoreCase);
			try
			{
				var resourceStudioHelper = new ResourceStudio.ResourceStudioHelper(engine);
				foreach (var pool in resourceStudioHelper.GetAllResourcePools())
				{
					if (pool != null && !String.IsNullOrWhiteSpace(pool.Name) && !resourcePoolIdByName.ContainsKey(pool.Name))
					{
						resourcePoolIdByName[pool.Name] = pool.Id;
					}
				}

				foreach (var resource in resourceStudioHelper.GetAllResources())
				{
					if (resource != null && !String.IsNullOrWhiteSpace(resource.Name) && !resourceIdByName.ContainsKey(resource.Name))
					{
						resourceIdByName[resource.Name] = resource.Id;
					}
				}
			}
			catch (Exception e)
			{
				warnings.Add($"Could not read resources/pools from Resource Studio: {e.Message}. Job nodes may be skipped.");
			}

			List<ServiceModels.Service> services;
			try
			{
				services = new DataHelperService(engine.GetUserConnection()).Read().ToList();
			}
			catch (Exception e)
			{
				warnings.Add($"Could not read the service inventory from DataMiner: {e.Message}. Service linking is disabled.");
				services = new List<ServiceModels.Service>();
			}

			var serviceRepo = new DataHelpersServiceManagement(engine.GetUserConnection());
			var relationshipHelper = new RelationshipsHelper(engine);

			var context = new ImportContext
			{
				Engine = engine,
				SchedulingHelper = schedulingHelper,
				WorkflowNameByTemplateId = workflowNameByTemplateId,
				WorkflowIdByName = workflowIdByName,
				ResourcePoolIdByName = resourcePoolIdByName,
				ResourceIdByName = resourceIdByName,
				ResourceMaps = resourceMaps,
				ServiceInventoryById = serviceInventoryById,
				Services = services,
				ServiceRepo = serviceRepo,
				RelationshipHelper = relationshipHelper,
				Warnings = warnings,
			};

			int created = 0;
			int skipped = 0;
			int failed = 0;
			int linked = 0;
			int serviceItemLinked = 0;

			foreach (var source in root.Jobs)
			{
				if (source == null || String.IsNullOrWhiteSpace(source.Id))
				{
					warnings.Add("Encountered a job without an id; skipped.");
					failed++;
					continue;
				}

				try
				{
					JobImportResult result = ImportJob(context, source);
					if (result.WasCreated)
					{
						created++;
						if (result.Linked)
						{
							linked++;
						}

						if (result.ServiceItemLinked)
						{
							serviceItemLinked++;
						}
					}
					else
					{
						skipped++;
					}
				}
				catch (Exception e)
				{
					failed++;
					warnings.Add($"Job '{source.Id}' was not imported: {e.Message}");
				}
			}

			var summary = new StringBuilder();
			summary.Append("[Import Jobs] Completed. ");
			summary.Append($"Jobs Created/Skipped/Failed: {created}/{skipped}/{failed}. ");
			summary.Append($"Service links created: {linked}. ");
			summary.Append($"Service item links created: {serviceItemLinked}. ");
			summary.Append($"Source file: {jobsJsonPath}.");
			if (warnings.Count > 0)
			{
				summary.Append($" Warnings ({warnings.Count}): ");
				summary.Append(String.Join(" | ", warnings.Take(25)));
			}

			engine.GenerateInformation(summary.ToString());
		}

		private static JobImportResult ImportJob(ImportContext context, JobImport source)
		{
			Guid workflowId = ResolveWorkflow(context, source);
			if (workflowId == Guid.Empty)
			{
				context.Warnings.Add($"Job '{source.Id}': no workflow could be resolved; job skipped.");
				return new JobImportResult { WasCreated = false };
			}

			DateTime jobStart = ParseDateUtc(source.ScheduledStart) ?? DateTime.UtcNow;
			DateTime jobEnd = ParseDateUtc(source.ScheduledEnd) ?? jobStart.AddYears(5);

			var configuration = new Scheduling.JobConfiguration
			{
				Name = String.IsNullOrWhiteSpace(source.Name) ? source.Id : source.Name,
				Description = BuildJobDescription(source),
				Start = new DateTimeOffset(jobStart, TimeSpan.Zero),
				End = new DateTimeOffset(jobEnd, TimeSpan.Zero),
				DomWorkflowId = workflowId,
			};

			foreach (var reservation in source.NodeReservations ?? new List<NodeReservationImport>())
			{
				if (reservation == null)
				{
					continue;
				}

				Scheduling.JobNodeConfiguration nodeConfiguration = BuildNodeConfiguration(context, source, reservation);
				if (nodeConfiguration != null)
				{
					configuration.Nodes.Add(nodeConfiguration);
				}
			}

			Guid jobId = context.SchedulingHelper.CreateJob(configuration);

			ApplyJobState(context, source, jobId);

			bool serviceItemLinked = TryLinkJobToServiceItem(context, source, jobId);
			bool linked = TryCreateServiceLink(context, source, jobId, configuration.Name);

			return new JobImportResult { WasCreated = true, Linked = linked, ServiceItemLinked = serviceItemLinked };
		}

		private static Scheduling.JobNodeConfiguration BuildNodeConfiguration(ImportContext context, JobImport source, NodeReservationImport reservation)
		{
			Guid poolId = ResolvePoolDomId(context, reservation.ResourcePoolId);
			if (poolId == Guid.Empty)
			{
				context.Warnings.Add($"Job '{source.Id}': resource pool '{reservation.ResourcePoolId}' could not be resolved; node '{reservation.NodeName}' skipped.");
				return null;
			}

			Guid resourceId = ResolveResourceDomId(context, reservation.ResourceId);
			if (resourceId != Guid.Empty)
			{
				return new Scheduling.JobResourceNodeConfiguration(resourceId, poolId);
			}

			if (!String.IsNullOrWhiteSpace(reservation.ResourceId))
			{
				context.Warnings.Add($"Job '{source.Id}': resource '{reservation.ResourceId}' could not be resolved; node '{reservation.NodeName}' falls back to a resource-pool node.");
			}

			return new Scheduling.JobResourcePoolNodeConfiguration(poolId);
		}

		private static void ApplyJobState(ImportContext context, JobImport source, Guid jobId)
		{
			string state = (source.State ?? String.Empty).Trim().ToLowerInvariant();
			if (state.Length == 0 || state == "draft")
			{
				return;
			}

			Scheduling.Job job;
			try
			{
				job = context.SchedulingHelper.GetJob(jobId);
			}
			catch (Exception e)
			{
				context.Warnings.Add($"Job '{source.Id}': could not reload job to apply state '{source.State}': {e.Message}");
				return;
			}

			if (job == null)
			{
				return;
			}

			try
			{
				switch (state)
				{
					case "tentative":
						job.ExecuteJobAction(Scheduling.JobAction.SaveAsTentative);
						break;
					case "confirmed":
						job.ExecuteJobAction(Scheduling.JobAction.ConfirmJob);
						break;
					case "running":
						job.ExecuteJobAction(Scheduling.JobAction.ConfirmJob);
						job.ManualStart(true);
						break;
					case "completed":
						job.ExecuteJobAction(Scheduling.JobAction.ConfirmJob);
						job.ExecuteJobAction(Scheduling.JobAction.CompletePastJob);
						break;
					default:
						break;
				}
			}
			catch (Exception e)
			{
				context.Warnings.Add($"Job '{source.Id}': could not fully advance to state '{source.State}': {e.Message}");
			}
		}

		private static Guid ResolvePoolDomId(ImportContext context, string resourcePoolId)
		{
			if (String.IsNullOrWhiteSpace(resourcePoolId))
			{
				return Guid.Empty;
			}

			if (context.ResourceMaps.PoolNameById.TryGetValue(resourcePoolId, out string poolName)
				&& context.ResourcePoolIdByName.TryGetValue(poolName, out Guid poolId))
			{
				return poolId;
			}

			return Guid.Empty;
		}

		private static Guid ResolveResourceDomId(ImportContext context, string resourceId)
		{
			if (String.IsNullOrWhiteSpace(resourceId))
			{
				return Guid.Empty;
			}

			if (context.ResourceMaps.ResourceNameById.TryGetValue(resourceId, out string resourceName)
				&& context.ResourceIdByName.TryGetValue(resourceName, out Guid domId))
			{
				return domId;
			}

			return Guid.Empty;
		}

		private static string BuildJobDescription(JobImport source)
		{
			var parts = new List<string>
			{
				$"Service: {source.ServiceId ?? "<none>"}",
				$"ServiceItem: {source.ServiceItemId ?? "<none>"}",
				$"Label: {source.Label ?? "<none>"}",
			};

			if (source.Paused.HasValue && source.Paused.Value)
			{
				string pausedDate = ParseDateUtc(source.PausedDate)?.ToString("u", CultureInfo.InvariantCulture);
				parts.Add(String.IsNullOrEmpty(pausedDate) ? "Paused: true" : $"Paused: true ({pausedDate})");
			}

			return String.Join(" | ", parts);
		}

		private static Guid ResolveWorkflow(ImportContext context, JobImport source)
		{
			if (String.IsNullOrWhiteSpace(source.WorkflowTemplateId))
			{
				return Guid.Empty;
			}

			if (!context.WorkflowNameByTemplateId.TryGetValue(source.WorkflowTemplateId, out string workflowName))
			{
				context.Warnings.Add($"Job '{source.Id}': workflow template '{source.WorkflowTemplateId}' not found in workflows.json.");
				return Guid.Empty;
			}

			if (context.WorkflowIdByName.TryGetValue(workflowName, out Guid workflowId))
			{
				return workflowId;
			}

			context.Warnings.Add($"Job '{source.Id}': no DataMiner workflow found with name '{workflowName}'.");
			return Guid.Empty;
		}

		private static bool TryCreateServiceLink(ImportContext context, JobImport source, Guid jobId, string jobName)
		{
			try
			{
				ServiceModels.Service service = ResolveService(context, source);
				if (service == null)
				{
					context.Warnings.Add($"Job '{source.Id}': service '{source.ServiceId}' not found; link skipped.");
					return false;
				}

				Guid serviceObjectType = GetOrCreateObjectType(context.RelationshipHelper, "Service");
				Guid jobObjectType = GetOrCreateObjectType(context.RelationshipHelper, "Job");

				var linkConfiguration = new LinkConfiguration
				{
					Child = new LinkDetailsConfiguration
					{
						DomObjectTypeId = serviceObjectType,
						ObjectId = service.ID.ToString(),
						ObjectName = service.Name,
						URL = "Link to open the service panel on service inventory app",
					},
					Parent = new LinkDetailsConfiguration
					{
						DomObjectTypeId = jobObjectType,
						ObjectId = jobId.ToString(),
						ObjectName = jobName,
					},
				};

				context.RelationshipHelper.CreateLink(linkConfiguration);
				return true;
			}
			catch (Exception e)
			{
				context.Warnings.Add($"Job '{source.Id}': service link failed: {e.Message}");
				return false;
			}
		}

		private static bool TryLinkJobToServiceItem(ImportContext context, JobImport source, Guid jobId)
		{
			if (String.IsNullOrWhiteSpace(source.ServiceItemId))
			{
				return false;
			}

			try
			{
				ServiceModels.Service service = ResolveService(context, source);
				if (service == null)
				{
					context.Warnings.Add($"Job '{source.Id}': service item link skipped because service '{source.ServiceId}' was not resolved.");
					return false;
				}

				var fullService = context.ServiceRepo.Services.Read(ServiceExposers.Guid.Equal(service.ID)).FirstOrDefault();
				if (fullService == null)
				{
					context.Warnings.Add($"Job '{source.Id}': service item link skipped because service details for '{service.Name}' could not be loaded.");
					return false;
				}

				var serviceItems = fullService.ServiceItems ?? new List<ServiceModels.ServiceItem>();
				var targetItem = ResolveTargetServiceItem(context, source, serviceItems);
				if (targetItem == null)
				{
					context.Warnings.Add($"Job '{source.Id}': serviceItemId '{source.ServiceItemId}' could not be matched to an imported service item.");
					return false;
				}

				string jobIdString = jobId.ToString();
				if (String.Equals(targetItem.ImplementationReference, jobIdString, StringComparison.InvariantCultureIgnoreCase))
				{
					return true;
				}

				targetItem.ImplementationReference = jobIdString;
				context.ServiceRepo.Services.CreateOrUpdate(fullService);
				return true;
			}
			catch (Exception e)
			{
				context.Warnings.Add($"Job '{source.Id}': service item link failed: {e.Message}");
				return false;
			}
		}

		private static ServiceModels.ServiceItem ResolveTargetServiceItem(
			ImportContext context,
			JobImport source,
			List<ServiceModels.ServiceItem> serviceItems)
		{
			if (serviceItems == null || serviceItems.Count == 0)
			{
				return null;
			}

			string workflowName = String.Empty;
			if (!String.IsNullOrWhiteSpace(source.WorkflowTemplateId))
			{
				context.WorkflowNameByTemplateId.TryGetValue(source.WorkflowTemplateId, out workflowName);
			}

			if (context.ServiceInventoryById.TryGetValue(source.ServiceId ?? String.Empty, out ServiceInventoryEntry serviceEntry)
				&& serviceEntry.ServiceItemsById.TryGetValue(source.ServiceItemId, out ServiceItemInventoryEntry inventoryItem))
			{
				string targetLabel = FirstNonEmpty(inventoryItem.Label, inventoryItem.Name);
				ServiceModels.ServiceItem exact = FindServiceItem(serviceItems, targetLabel, workflowName, preferUnlinked: true);
				if (exact != null)
				{
					return exact;
				}
			}

			string sourceLabel = FirstNonEmpty(source.Label);
			ServiceModels.ServiceItem bySourceLabel = FindServiceItem(serviceItems, sourceLabel, workflowName, preferUnlinked: true);
			if (bySourceLabel != null)
			{
				return bySourceLabel;
			}

			return FindServiceItem(serviceItems, String.Empty, workflowName, preferUnlinked: true);
		}

		private static ServiceModels.ServiceItem FindServiceItem(
			List<ServiceModels.ServiceItem> serviceItems,
			string label,
			string workflowName,
			bool preferUnlinked)
		{
			IEnumerable<ServiceModels.ServiceItem> candidates = serviceItems.Where(item => item != null);

			if (!String.IsNullOrWhiteSpace(workflowName))
			{
				candidates = candidates.Where(item => String.Equals(item.DefinitionReference, workflowName, StringComparison.InvariantCultureIgnoreCase));
			}

			if (!String.IsNullOrWhiteSpace(label))
			{
				candidates = candidates.Where(item => String.Equals(item.Label, label, StringComparison.InvariantCultureIgnoreCase));
			}

			var materialized = candidates.ToList();
			if (preferUnlinked)
			{
				var unlinked = materialized.FirstOrDefault(item => String.IsNullOrWhiteSpace(item.ImplementationReference));
				if (unlinked != null)
				{
					return unlinked;
				}
			}

			return materialized.FirstOrDefault();
		}

		private static string FirstNonEmpty(params string[] values)
		{
			foreach (var value in values)
			{
				if (!String.IsNullOrWhiteSpace(value))
				{
					return value;
				}
			}

			return String.Empty;
		}

		private static ServiceModels.Service ResolveService(ImportContext context, JobImport source)
		{
			if (String.IsNullOrWhiteSpace(source.ServiceId))
			{
				return null;
			}

			string systemServiceId = null;
			string serviceName = null;
			if (context.ServiceInventoryById.TryGetValue(source.ServiceId, out ServiceInventoryEntry entry))
			{
				systemServiceId = entry.SystemServiceId;
				serviceName = entry.Name;
			}

			if (!String.IsNullOrWhiteSpace(systemServiceId))
			{
				var byServiceId = context.Services.FirstOrDefault(s => String.Equals(s.ServiceID, systemServiceId, StringComparison.InvariantCultureIgnoreCase));
				if (byServiceId != null)
				{
					return byServiceId;
				}
			}

			if (!String.IsNullOrWhiteSpace(serviceName))
			{
				var byName = context.Services.FirstOrDefault(s => String.Equals(s.Name, serviceName, StringComparison.InvariantCultureIgnoreCase));
				if (byName != null)
				{
					return byName;
				}
			}

			return context.Services.FirstOrDefault(s => String.Equals(s.ServiceID, source.ServiceId, StringComparison.InvariantCultureIgnoreCase));
		}

		private static Guid GetOrCreateObjectType(RelationshipsHelper relationshipHelper, string name)
		{
			var objectType = relationshipHelper.GetObjectType(name);
			if (objectType == null)
			{
				return relationshipHelper.CreateObjectType(new ObjectTypeConfiguration { Name = name });
			}

			return objectType.Id;
		}

		private static Dictionary<string, string> ReadWorkflowNames(string path, List<string> warnings)
		{
			var map = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
			if (!File.Exists(path))
			{
				warnings.Add($"Workflows JSON not found ('{path}'); workflow references will be reduced.");
				return map;
			}

			try
			{
				var root = JsonConvert.DeserializeObject<WorkflowsRoot>(File.ReadAllText(path));
				foreach (var template in root?.WorkflowTemplates ?? new List<WorkflowTemplateImport>())
				{
					if (template != null && !String.IsNullOrWhiteSpace(template.Id) && !map.ContainsKey(template.Id))
					{
						map[template.Id] = template.Name;
					}
				}
			}
			catch (Exception e)
			{
				warnings.Add($"Could not read workflows JSON '{path}': {e.Message}");
			}

			return map;
		}

		private static ResourceStudioMaps ReadResourceStudioMaps(string path, List<string> warnings)
		{
			var maps = new ResourceStudioMaps
			{
				PoolNameById = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase),
				ResourceNameById = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase),
			};

			if (!File.Exists(path))
			{
				warnings.Add($"Resource Studio JSON not found ('{path}'); job nodes cannot be resolved.");
				return maps;
			}

			try
			{
				var root = JsonConvert.DeserializeObject<ResourceStudioRoot>(File.ReadAllText(path));
				foreach (var pool in root?.ResourcePools ?? new List<ResourcePoolImport>())
				{
					if (pool == null || String.IsNullOrWhiteSpace(pool.Id))
					{
						continue;
					}

					maps.PoolNameById[pool.Id] = pool.Name;
					foreach (var resource in pool.Resources ?? new List<ResourceImport>())
					{
						if (resource != null && !String.IsNullOrWhiteSpace(resource.Id) && !maps.ResourceNameById.ContainsKey(resource.Id))
						{
							maps.ResourceNameById[resource.Id] = resource.Name;
						}
					}
				}
			}
			catch (Exception e)
			{
				warnings.Add($"Could not read resource studio JSON '{path}': {e.Message}");
			}

			return maps;
		}

		private static Dictionary<string, ServiceInventoryEntry> ReadServiceInventory(string path, List<string> warnings)
		{
			var map = new Dictionary<string, ServiceInventoryEntry>(StringComparer.InvariantCultureIgnoreCase);
			if (!File.Exists(path))
			{
				warnings.Add($"Service Inventory JSON not found ('{path}'); service linking will be reduced.");
				return map;
			}

			try
			{
				var root = JsonConvert.DeserializeObject<ServiceInventoryRoot>(File.ReadAllText(path));
				foreach (var service in root?.Services ?? new List<ServiceInventoryImport>())
				{
					if (service != null && !String.IsNullOrWhiteSpace(service.Id) && !map.ContainsKey(service.Id))
					{
						var entry = new ServiceInventoryEntry
						{
							SystemServiceId = service.SystemServiceId,
							Name = service.Name,
						};

						foreach (var item in service.ServiceItems ?? new List<ServiceInventoryItemImport>())
						{
							if (item == null || String.IsNullOrWhiteSpace(item.Id) || entry.ServiceItemsById.ContainsKey(item.Id))
							{
								continue;
							}

							entry.ServiceItemsById[item.Id] = new ServiceItemInventoryEntry
							{
								Name = item.Name,
								Label = item.Label,
							};
						}

						map[service.Id] = entry;
					}
				}
			}
			catch (Exception e)
			{
				warnings.Add($"Could not read service inventory JSON '{path}': {e.Message}");
			}

			return map;
		}

		private static string ResolvePath(IEngine engine, string parameterName, string defaultPath)
		{
			string value = engine.GetScriptParam(parameterName)?.Value;
			return String.IsNullOrWhiteSpace(value) ? defaultPath : value;
		}

		private static DateTime? ParseDateUtc(string value)
		{
			if (String.IsNullOrWhiteSpace(value))
			{
				return null;
			}

			if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime parsed))
			{
				return DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
			}

			return null;
		}

		private sealed class ImportContext
		{
			public IEngine Engine { get; set; }

			public Scheduling.SchedulingHelper SchedulingHelper { get; set; }

			public Dictionary<string, string> WorkflowNameByTemplateId { get; set; }

			public Dictionary<string, Guid> WorkflowIdByName { get; set; }

			public Dictionary<string, Guid> ResourcePoolIdByName { get; set; }

			public Dictionary<string, Guid> ResourceIdByName { get; set; }

			public ResourceStudioMaps ResourceMaps { get; set; }

			public Dictionary<string, ServiceInventoryEntry> ServiceInventoryById { get; set; }

			public List<ServiceModels.Service> Services { get; set; }

			public DataHelpersServiceManagement ServiceRepo { get; set; }

			public RelationshipsHelper RelationshipHelper { get; set; }

			public List<string> Warnings { get; set; }
		}

		private sealed class JobImportResult
		{
			public bool WasCreated { get; set; }

			public bool Linked { get; set; }

			public bool ServiceItemLinked { get; set; }
		}

		private sealed class ResourceStudioMaps
		{
			public Dictionary<string, string> PoolNameById { get; set; }

			public Dictionary<string, string> ResourceNameById { get; set; }
		}

		private sealed class ServiceInventoryEntry
		{
			public string SystemServiceId { get; set; }

			public string Name { get; set; }

			public Dictionary<string, ServiceItemInventoryEntry> ServiceItemsById { get; set; }
				= new Dictionary<string, ServiceItemInventoryEntry>(StringComparer.InvariantCultureIgnoreCase);
		}

		private sealed class ServiceItemInventoryEntry
		{
			public string Name { get; set; }

			public string Label { get; set; }
		}

		private sealed class JobsRoot
		{
			[JsonProperty("jobs")]
			public List<JobImport> Jobs { get; set; }
		}

		private sealed class JobImport
		{
			[JsonProperty("id")]
			public string Id { get; set; }

			[JsonProperty("name")]
			public string Name { get; set; }

			[JsonProperty("workflowTemplateId")]
			public string WorkflowTemplateId { get; set; }

			[JsonProperty("serviceId")]
			public string ServiceId { get; set; }

			[JsonProperty("serviceItemId")]
			public string ServiceItemId { get; set; }

			[JsonProperty("label")]
			public string Label { get; set; }

			[JsonProperty("state")]
			public string State { get; set; }

			[JsonProperty("createdDate")]
			public string CreatedDate { get; set; }

			[JsonProperty("scheduledStart")]
			public string ScheduledStart { get; set; }

			[JsonProperty("scheduledEnd")]
			public string ScheduledEnd { get; set; }

			[JsonProperty("paused")]
			public bool? Paused { get; set; }

			[JsonProperty("pausedDate")]
			public string PausedDate { get; set; }

			[JsonProperty("nodeReservations")]
			public List<NodeReservationImport> NodeReservations { get; set; }
		}

		private sealed class NodeReservationImport
		{
			[JsonProperty("nodeId")]
			public string NodeId { get; set; }

			[JsonProperty("nodeName")]
			public string NodeName { get; set; }

			[JsonProperty("resourcePoolId")]
			public string ResourcePoolId { get; set; }

			[JsonProperty("resourceId")]
			public string ResourceId { get; set; }

			[JsonProperty("candidateResourceIds")]
			public List<string> CandidateResourceIds { get; set; }

			[JsonProperty("capabilityMatch")]
			public List<CapabilityMatchImport> CapabilityMatch { get; set; }

			[JsonProperty("capacityBooking")]
			public List<CapacityImport> CapacityBooking { get; set; }

			[JsonProperty("capacityRequested")]
			public List<CapacityImport> CapacityRequested { get; set; }

			[JsonProperty("released")]
			public bool? Released { get; set; }
		}

		private sealed class CapabilityMatchImport
		{
			[JsonProperty("capabilityId")]
			public string CapabilityId { get; set; }

			[JsonProperty("value")]
			public JToken Value { get; set; }
		}

		private sealed class CapacityImport
		{
			[JsonProperty("capacityId")]
			public string CapacityId { get; set; }

			[JsonProperty("amount")]
			public double Amount { get; set; }
		}

		private sealed class WorkflowsRoot
		{
			[JsonProperty("workflowTemplates")]
			public List<WorkflowTemplateImport> WorkflowTemplates { get; set; }
		}

		private sealed class WorkflowTemplateImport
		{
			[JsonProperty("id")]
			public string Id { get; set; }

			[JsonProperty("name")]
			public string Name { get; set; }
		}

		private sealed class ResourceStudioRoot
		{
			[JsonProperty("resourcePools")]
			public List<ResourcePoolImport> ResourcePools { get; set; }
		}

		private sealed class ResourcePoolImport
		{
			[JsonProperty("id")]
			public string Id { get; set; }

			[JsonProperty("name")]
			public string Name { get; set; }

			[JsonProperty("resources")]
			public List<ResourceImport> Resources { get; set; }
		}

		private sealed class ResourceImport
		{
			[JsonProperty("id")]
			public string Id { get; set; }

			[JsonProperty("name")]
			public string Name { get; set; }
		}

		private sealed class ServiceInventoryRoot
		{
			[JsonProperty("services")]
			public List<ServiceInventoryImport> Services { get; set; }
		}

		private sealed class ServiceInventoryImport
		{
			[JsonProperty("id")]
			public string Id { get; set; }

			[JsonProperty("systemServiceId")]
			public string SystemServiceId { get; set; }

			[JsonProperty("name")]
			public string Name { get; set; }

			[JsonProperty("serviceItems")]
			public List<ServiceInventoryItemImport> ServiceItems { get; set; }
		}

		private sealed class ServiceInventoryItemImport
		{
			[JsonProperty("id")]
			public string Id { get; set; }

			[JsonProperty("name")]
			public string Name { get; set; }

			[JsonProperty("label")]
			public string Label { get; set; }
		}
	}
}
