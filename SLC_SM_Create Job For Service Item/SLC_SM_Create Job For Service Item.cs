namespace SLCSMCreateJobForServiceItem
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using DomHelpers.SlcWorkflow;
	using Library.Dom;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Net.Apps.DataMinerObjectModel;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.ServiceManagement;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.Relationship;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ApiHelpers;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;
	using Skyline.DataMiner.Utils.ServiceManagement.Common.Extensions;
	using Skyline.DataMiner.Utils.ServiceManagement.Common.IAS;
	using static DomHelpers.SlcServicemanagement.SlcServicemanagementIds.Behaviors.Service_Behavior;
	using Models = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;

	/// <summary>
	///     Represents a DataMiner Automation script.
	/// </summary>
	public class Script
	{
		private IEngine engine;

		/// <summary>
		///     The script entry point.
		/// </summary>
		/// <param name="engine">Link with SLAutomation process.</param>
		public void Run(IEngine engine)
		{
			/*
            * Note:
            * Do not remove the commented methods below!
            * The lines are needed to execute an interactive automation script from the non-interactive automation script or from Visio!
            *
            * engine.ShowUI();
            */

			try
			{
				this.engine = engine;
				RunSafe();
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
				// Only applicable for interactive scripts, can be removed for non-interactive scripts.
			}
			catch (Exception e)
			{
				engine.ShowErrorDialog(e);
				engine.Log(e.ToString());
			}
		}

		private void AddOrUpdateServiceItemToInstance(IServiceManagementApiHelper helper, Models.Service instance, Models.ServiceItem newSection, string oldLabel)
		{
			var oldItem = instance.ServiceItems.FirstOrDefault(x => x.Label == oldLabel);
			if (oldItem != null)
			{
				instance.ServiceItems.Remove(oldItem);
				newSection.ServiceItemID = oldItem.ServiceItemID;
			}
			else
			{
				long[] ids = instance.ServiceItems
					.Where(x => x.ServiceItemID.HasValue)
					.Select(x => x.ServiceItemID.Value)
					.OrderBy(x => x)
					.ToArray();
				newSection.ServiceItemID = ids.Any() ? ids.Max() + 1 : 0;
			}

			instance.ServiceItems.Add(newSection);
			helper.ServiceInventory.Services.Update(instance);

			UpdateServiceStatusOnServiceItem(instance);
		}

		private static ConnectionsSection CloneConnection(ConnectionsSection connection)
		{
			if (connection == null)
			{
				return null;
			}

			return new ConnectionsSection
			{
				ConnectionID = connection.ConnectionID,
				SourceNodeID = connection.SourceNodeID,
				DestinationNodeID = connection.DestinationNodeID,
				ConnectionAlias = connection.ConnectionAlias,
				ConnectionExecutionOrder = connection.ConnectionExecutionOrder,
				ConnectionType = connection.ConnectionType,
				ConnectionSubtype = connection.ConnectionSubtype,
				PredefinedSubset = connection.PredefinedSubset,
				ConnectionDetails = connection.ConnectionDetails,
				ConnectionExecutionScript = connection.ConnectionExecutionScript,
			};
		}

		private JobsInstance CreateJobConfiguration(Models.Service instance, Models.ServiceItem serviceItemsSection, WorkflowsInstance workflow)
		{
			DateTime start = instance.StartTime ?? throw new InvalidOperationException("No Start Time configured to create the job from");
			DateTime end = instance.EndTime ?? start + TimeSpan.FromDays(365 * 5);

			var job = new JobsInstance
			{
				JobInfo = new JobInfoSection
				{
					JobName = $"{instance.Name} | {serviceItemsSection.Label}",
					JobDescription = $"{instance.ServiceID} | {serviceItemsSection.Label}",
					Workflow = workflow.ID.Id,
					JobStart = start,
					JobEnd = end,
					JobSource = "Scheduling",
					JobPriority = SlcWorkflowIds.Enums.Jobpriority.Normal,
				},
			};
			foreach (var node in workflow.Nodeses?.Select(CloneNode).Where(clone => clone != null) ?? Enumerable.Empty<NodesSection>())
			{
				job.Nodeses.Add(node);
			}

			foreach (var connection in workflow.Connectionses?.Select(CloneConnection).Where(clone => clone != null) ?? Enumerable.Empty<ConnectionsSection>())
			{
				job.Connectionses.Add(connection);
			}

			return job;
		}

		private void CreateLink(IEngine engine, Models.Service instance, JobsInstance job)
		{
			var linkHelper = new DataHelperLink(engine.GetUserConnection());
			string jobId = job.ID.Id.ToString();

			var existingLink = linkHelper.Read(Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.LinkExposers.ParentID.Equal(jobId).AND(Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.LinkExposers.ChildID.Equal(instance.Identifier))).FirstOrDefault();
			if (existingLink != null)
			{
				return;
			}

			linkHelper.CreateOrUpdate(
				new Skyline.DataMiner.ProjectApi.ServiceManagement.API.Relationship.Models.Link
				{
					ParentID = jobId,
					ParentName = job.Name,
					ChildID = instance.Identifier,
					ChildName = instance.Name,
				});
		}

		private static NodesSection CloneNode(NodesSection node)
		{
			if (node == null)
			{
				return null;
			}

			return new NodesSection
			{
				NodeID = node.NodeID,
				NodeAlias = node.NodeAlias,
				NodeType = node.NodeType,
				NodeReferenceID = node.NodeReferenceID,
				NodeParentReferenceID = node.NodeParentReferenceID,
				NodeIcon = node.NodeIcon,
				AutomaticConfiguration = node.AutomaticConfiguration,
				ConfigurationParameters = node.ConfigurationParameters,
				AdHocControlScript = node.AdHocControlScript,
				NodeConfigurationExecutionOrder = node.NodeConfigurationExecutionOrder,
				ReserveNode = node.ReserveNode,
				Hidden = node.Hidden,
				NodeStartTime = node.NodeStartTime,
				NodeEndTime = node.NodeEndTime,
				LinkedBookingIds = node.LinkedBookingIds,
				ResourceSelectMode = node.ResourceSelectMode,
				ResourceSelectState = node.ResourceSelectState,
				Billable = node.Billable,
				NodeConfiguration = node.NodeConfiguration,
				NodeConfigurationStatus = node.NodeConfigurationStatus,
			};
		}

		private void RunSafe()
		{
			Guid domId = engine.ReadScriptParamFromApp<Guid>("DOM ID");
			if (domId == Guid.Empty)
			{
				throw new InvalidOperationException("No DOM ID provided as input to the script");
			}

			string label = engine.ReadScriptParamFromApp("Service Item Label");

			var serviceHelper = engine.GetUserConnection().GetServiceManagementApiHelper("Service Inventory");
			var instance = serviceHelper.ServiceInventory.Services
				.Read(ServiceExposers.Identifier.Equal(domId.ToString()))
				.FirstOrDefault() ?? throw new InvalidOperationException($"No Service exists with ID '{domId}'");

			var serviceItemsSection = instance.ServiceItems.SingleOrDefault(s => s.Label == label)
			                          ?? throw new InvalidOperationException($"Could not find the service item section with label '{label}'");

			if (!engine.DomModelExists(SlcWorkflowIds.ModuleId, new[] {SlcWorkflowIds.Sections.WorkflowInfo.Id.Id}))
			{
				throw new InvalidOperationException("The Media Ops solution needs to be installed to use this feature. The '(slc)workflow' DOM model is required but not found on the system.");
			}

			var workflow = WorkflowExtensions.GetWorkflows(engine.SendSLNetMessages).FirstOrDefault(x => x.Name == serviceItemsSection.DefinitionReference)
			               ?? throw new InvalidOperationException($"No Workflow found on the system with name '{serviceItemsSection.DefinitionReference}'");

			if (instance.EndTime.HasValue && instance.EndTime.Value < DateTime.UtcNow)
			{
				throw new InvalidOperationException($"End time lies in the past ({instance.EndTime}), not possible to create a job for a past event");
			}

			engine.Log("Gonna create job configuration");
			var job = CreateJobConfiguration(instance, serviceItemsSection, workflow);

			var domWorkflowHelper = new DomHelper(engine.SendSLNetMessages, SlcWorkflowIds.ModuleId);
			job.Save(domWorkflowHelper);

			CreateLink(engine, instance, job);
			TrySetMonitoringSettingsForJob(job);

			job.Save(domWorkflowHelper);

			serviceItemsSection.ImplementationReference = job.ID.Id.ToString();
			AddOrUpdateServiceItemToInstance(serviceHelper, instance, serviceItemsSection, label);
		}

		private void UpdateServiceStatusOnServiceItem(Models.Service instance)
		{
			if (instance?.ServiceItems == null
				|| !instance.ServiceItems.All(x => !String.IsNullOrEmpty(x.ImplementationReference) && Guid.TryParse(x.ImplementationReference, out Guid _))
				|| !Guid.TryParse(instance.Identifier, out var serviceGuid))
			{
				return;
			}

			var legacyServiceHelper = new DataHelperService(engine.GetUserConnection());
			var legacyService = legacyServiceHelper.Read(Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceExposers.Guid.Equal(serviceGuid)).FirstOrDefault();
			if (legacyService == null)
			{
				return;
			}

			if (legacyService.Status == StatusesEnum.New)
			{
				legacyService = legacyServiceHelper.UpdateState(legacyService, TransitionsEnum.New_To_Designed);
			}

			if (legacyService.Status == StatusesEnum.Designed)
			{
				legacyServiceHelper.UpdateState(legacyService, TransitionsEnum.Designed_To_Reserved);
			}
		}

		private void TrySetMonitoringSettingsForJob(JobsInstance job)
		{
			job.MonitoringSettings.AtJobStart = SlcWorkflowIds.Enums.Atjobstart.CreateServiceAtWorkflowStart;
			job.MonitoringSettings.AtJobEnd = SlcWorkflowIds.Enums.Atjobend.DeleteServiceIfOneExists;
		}
	}
}