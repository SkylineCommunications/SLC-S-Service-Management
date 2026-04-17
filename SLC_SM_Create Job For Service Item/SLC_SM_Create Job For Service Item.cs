/*
****************************************************************************
*  Copyright (c) 2025,  Skyline Communications NV  All Rights Reserved.    *
****************************************************************************

By using this script, you expressly agree with the usage terms and
conditions set out below.
This script and all related materials are protected by copyrights and
other intellectual property rights that exclusively belong
to Skyline Communications.

A user license granted for this script is strictly for personal use only.
This script may not be used in any way by anyone without the prior
written consent of Skyline Communications. Any sublicensing of this
script is forbidden.

Any modifications to this script by the user are only allowed for
personal use and within the intended purpose of the script,
and will remain the sole responsibility of the user.
Skyline Communications will not be responsible for any damages or
malfunctions whatsoever of the script resulting from a modification
or adaptation by the user.

The content of this script is confidential information.
The user hereby agrees to keep this confidential information strictly
secret and confidential and not to disclose or reveal it, in whole
or in part, directly or indirectly to any person, entity, organization
or administration without the prior written consent of
Skyline Communications.

Any inquiries can be addressed to:

    Skyline Communications NV
    Ambachtenstraat 33
    B-8870 Izegem
    Belgium
    Tel.    : +32 51 31 35 69
    Fax.    : +32 51 31 01 29
    E-mail    : info@skyline.be
    Web        : www.skyline.be
    Contact    : Ben Vandenberghe

****************************************************************************
Revision History:

DATE        VERSION        AUTHOR            COMMENTS

13/03/2025    1.0.0.1        RME, Skyline    Initial version
****************************************************************************
*/

namespace SLCSMCreateJobForServiceItem
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	using DomHelpers.SlcWorkflow;

	using Newtonsoft.Json;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Net.Apps.DataMinerObjectModel;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.ServiceManagement;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM;
	using Skyline.DataMiner.Utils.MediaOps.Common;
	using Skyline.DataMiner.Utils.MediaOps.Common.DOM.Applications.Workflow;
	using Skyline.DataMiner.Utils.MediaOps.Common.IOData.Scheduling.Scripts.JobHandler;
	using Skyline.DataMiner.Utils.MediaOps.Helpers.Relationships;
	using Skyline.DataMiner.Utils.MediaOps.Helpers.ResourceStudio;
	using Skyline.DataMiner.Utils.MediaOps.Helpers.Workflows;
	using Skyline.DataMiner.Utils.ServiceManagement.Common.Extensions;
	using Skyline.DataMiner.Utils.ServiceManagement.Common.IAS;

	using static DomHelpers.SlcServicemanagement.SlcServicemanagementIds.Behaviors.Service_Behavior;

	using DomApplications = Skyline.DataMiner.Utils.MediaOps.Common.DOM.Applications;
	using ServiceModels = Skyline.DataMiner.ProjectApi.ServiceManagement.API.ServiceManagement.Models;

	/// <summary>
	///     Represents a DataMiner Automation script.
	/// </summary>
	public class Script
	{
		private const string ReferenceUnknown = "Reference Unknown";

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
				// Only applicable for interactive scripts, can be removed for non-interactive scripts.
			}
			catch (Exception e)
			{
				engine.ShowErrorDialog(e);
				engine.Log(e.ToString());
			}
		}

		private static void UpdateState(DataHelperService srvHelper, ServiceModels.Service service)
		{
			// If all items are in progress -> move to In Progress
			if (!service.ServiceItems.All(x => !String.IsNullOrEmpty(x.ImplementationReference) && x.ImplementationReference != ReferenceUnknown))
			{
				return;
			}

			if (service.Status == StatusesEnum.New)
			{
				service = srvHelper.UpdateState(service, TransitionsEnum.New_To_Designed);
			}

			if (service.Status == StatusesEnum.Designed)
			{
				service = srvHelper.UpdateState(service, TransitionsEnum.Designed_To_Reserved);
			}

			if (service.Status == StatusesEnum.Reserved)
			{
				service = srvHelper.UpdateState(service, TransitionsEnum.Reserved_To_Active);
			}
		}

		private void AddOrUpdateServiceItemToInstance(DataHelperService helper, ServiceModels.Service instance, ServiceModels.ServiceItem newSection, string oldLabel)
		{
			var oldItem = instance.ServiceItems.FirstOrDefault(x => x.Label == oldLabel);
			if (oldItem != null)
			{
				instance.ServiceItems.Remove(oldItem);
			}
			else
			{
				long[] ids = instance.ServiceItems.Select(x => x.ID).OrderBy(x => x).ToArray();
				newSection.ID = ids.Any() ? ids.Max() + 1 : 0;
			}

			instance.ServiceItems.Add(newSection);
			helper.CreateOrUpdate(instance);

			UpdateState(helper, instance);
		}

		private CreateJobAction CreateJobConfiguration(ServiceModels.Service instance, ServiceModels.ServiceItem serviceItemsSection, Skyline.DataMiner.Utils.MediaOps.Helpers.Workflows.Workflow workflow)
		{
			return new CreateJobAction
			{
				Name = $"{instance.Name} | {serviceItemsSection.Label}",
				Description = $"{instance.ID} | {serviceItemsSection.Label}",
				DomWorkflowId = workflow.Id,
				Source = "Scheduling",
				DesiredJobStatus = DesiredJobStatus.Tentative,
				Start = instance.StartTime ?? throw new InvalidOperationException("No Start Time configured to create the job from"),
				End = instance.EndTime ?? instance.StartTime.Value + TimeSpan.FromDays(365 * 5), ////ReservationInstance.PermanentEnd,
			};
		}

		private void CreateLink(IEngine engine, ServiceModels.Service instance, JobsInstance job)
		{
			var relationshipHelper = new RelationshipsHelper(engine);

			var serviceObjectType = GetOrCreateObjectType(relationshipHelper, "Service");
			var jobObjectType = GetOrCreateObjectType(relationshipHelper, "Job");

			var linkDetailsConfiguration = CreateLinkDetailsConfiguration(instance, job, serviceObjectType, jobObjectType);
			relationshipHelper.CreateLink(linkDetailsConfiguration);
		}

		private LinkConfiguration CreateLinkDetailsConfiguration(ServiceModels.Service instance, JobsInstance job, Guid serviceObjectType, Guid jobObjectType)
		{
			var linkConfiguration = new LinkConfiguration
			{
				Child = new LinkDetailsConfiguration
				{
					DomObjectTypeId = serviceObjectType,
					ObjectId = instance.ID.ToString(),
					ObjectName = instance.Name,
					URL = "Link to open the service panel on service inventory app",
				},
				Parent = new LinkDetailsConfiguration
				{
					DomObjectTypeId = jobObjectType,
					ObjectId = job.ID.Id.ToString(),
					ObjectName = job.Name,
				},
			};

			return linkConfiguration;
		}

		private JobsInstance FindJob(DomHelper domWorkflowHelper, Guid jobId)
		{
			var filter = DomInstanceExposers.Id.Equal(jobId);
			var instance = domWorkflowHelper.DomInstances.Read(filter).FirstOrDefault();
			if (instance != null)
			{
				return new JobsInstance(instance);
			}

			return default;
		}

		private Guid GetOrCreateObjectType(RelationshipsHelper relationshipHelper, string name)
		{
			var objectType = relationshipHelper.GetObjectType(name);
			if (objectType == null)
			{
				return relationshipHelper.CreateObjectType(
					new ObjectTypeConfiguration
					{
						Name = name,
					});
			}

			return objectType.Id;
		}

		private void RunSafe(IEngine engine)
		{
			Guid domId = engine.ReadScriptParamFromApp<Guid>("DOM ID");
			if (domId == Guid.Empty)
			{
				throw new InvalidOperationException("No DOM ID provided as input to the script");
			}

			string label = engine.ReadScriptParamFromApp("Service Item Label");

			var dataHelperService = new DataHelperService(engine.GetUserConnection());
			var instance = dataHelperService.Read(ServiceExposers.Guid.Equal(domId)).FirstOrDefault()
						   ?? throw new InvalidOperationException($"No Service exists with ID '{domId}'");
			var serviceItemsSection = instance.ServiceItems.SingleOrDefault(s => s.Label == label)
									  ?? throw new InvalidOperationException($"Could not find the service item section with label '{label}'");

			if (!engine.DomModelExists(SlcWorkflowIds.ModuleId, new[] { SlcWorkflowIds.Sections.WorkflowInfo.Id.Id }))
			{
				throw new InvalidOperationException("The Media Ops solution needs to be installed to use this feature. The '(slc)workflow' DOM model is required but not found on the system.");
			}

			var workflowHelper = new WorkflowHelper(engine);
			var workflow = workflowHelper.GetAllWorkflows().FirstOrDefault(x => x.Name == serviceItemsSection.DefinitionReference)
						   ?? throw new InvalidOperationException($"No Workflow found on the system with name '{serviceItemsSection.DefinitionReference}'");

			if (instance.EndTime.HasValue && instance.EndTime.Value < DateTime.UtcNow)
			{
				throw new InvalidOperationException($"End time lies in the past ({instance.EndTime}), not possible to create a job for a past event");
			}

			engine.Log("Gonna create job configuration");

			CreateJobAction jobConfiguration = CreateJobConfiguration(instance, serviceItemsSection, workflow);

			engine.Log("Gonna send to job handler");
			OutputData sendToJobHandler = jobConfiguration.SendToJobHandler(engine, true);

			engine.Log("Returned from job handler");

			if (sendToJobHandler == null)
			{
				engine.Log("Failed to create the job");
				engine.Log($"This is the exception: {sendToJobHandler.ExceptionInfo.SourceException}");
				throw new InvalidOperationException("Failure on creating the job from the workflow");
			}

			if (sendToJobHandler.HasException)
			{
				engine.ExitFail(sendToJobHandler.ExceptionInfo.SourceException.Message);
				return;
			}

			var outputData = (CreateJobActionOutput)sendToJobHandler.ActionOutput;
			if (outputData == null)
			{
				throw new InvalidOperationException("Failure on creating the job from the workflow");
			}

			var jobId = outputData.DomJobId;

			var domWorkflowHelper = new DomHelper(engine.SendSLNetMessages, SlcWorkflowIds.ModuleId);
			var job = FindJob(domWorkflowHelper, jobId);

			UpdateJobWithParameterConfigurations(engine, jobId, instance);

			if (job.Status == SlcWorkflowIds.Behaviors.Job_Behavior.StatusesEnum.Draft)
			{
				var transitionJobToTentativeInputData = new ExecuteJobAction
				{
					DomJobId = jobId,
					JobAction = JobAction.SaveAsTentative,
				};
				transitionJobToTentativeInputData.SendToJobHandler(engine, true);

				job = FindJob(domWorkflowHelper, jobId);
			}

			CreateLink(engine, instance, job);
			TrySetMonitoringSettingsForJob(job);

			job.Save(domWorkflowHelper);

			serviceItemsSection.ImplementationReference = jobId.ToString();
			AddOrUpdateServiceItemToInstance(dataHelperService, instance, serviceItemsSection, label);
		}

		private void UpdateJobWithParameterConfigurations(IEngine engine, Guid jobId, ServiceModels.Service instance)
		{
			WorkflowHandler worflowHandler = new DomApplications.Workflow.WorkflowHandler(engine);
			ResourceStudioHelper resourceStudioHelper = new ResourceStudioHelper(engine);
			Guid domConfigurationId = GetDomConfigurationIdFromJob(engine, worflowHandler, jobId);

			if (domConfigurationId == Guid.Empty)
			{
				return;
			}

			var domConfiguration = worflowHandler.GetConfigurationByDomInstanceId(domConfigurationId);

			if (domConfiguration == null || domConfiguration.ProfileParameterValues.Count == 0)
			{
				return;
			}

			var parameterIds = domConfiguration.ProfileParameterValues.Select(x => x.ProfileParameterId).ToList();
			if (parameterIds.Count == 0)
			{
				return;
			}

			var parameters = resourceStudioHelper.GetParameters(parameterIds);
			var jobParameters = domConfiguration.ProfileParameterValues.Select(
				o
				=> new
				{
					ParameterValue = new Skyline.DataMiner.Utils.MediaOps.Common.IOData.Workflows.Scripts.ConfigurationHandler.ProfileParameterValue
					{
						ProfileParameterId = o.ProfileParameterId,
						StringValue = o.StringValue,
						DoubleValue = o.DoubleValue,
					},
					parameters.Configurations.FirstOrDefault(p => p.Id == o.ProfileParameterId)?.Name,
				});

			engine.Log($"Job Param:\n{JsonConvert.SerializeObject(jobParameters)}");
			var serviceParams = instance.ServiceConfiguration.Parameters.Select(p => p.ConfigurationParameter).ToList();
			foreach (var profile in instance.ServiceConfiguration.Profiles)
			{
				serviceParams.AddRange(profile.Profile.ConfigurationParameterValues);
			}

			var configurationParameters = GetFilteredConfigurationParameters(engine, serviceParams);

			var serviceParamsWithConfigParam = serviceParams.Select(sp => new
			{
				ConfigurationParameterValue = sp,
				ConfigurationParameter = configurationParameters.FirstOrDefault(cp => cp.ID == sp.ConfigurationParameterId),
			}).ToList();

			List<Skyline.DataMiner.Utils.MediaOps.Common.IOData.Workflows.Scripts.ConfigurationHandler.ProfileParameterValue> overriddenValues = new List<Skyline.DataMiner.Utils.MediaOps.Common.IOData.Workflows.Scripts.ConfigurationHandler.ProfileParameterValue>();

			engine.Log($"Service params:\n{JsonConvert.SerializeObject(serviceParamsWithConfigParam.Select(p => new { p.ConfigurationParameter.Name }))}");

			foreach (var param in jobParameters)
			{
				var serviceParam = serviceParamsWithConfigParam.FirstOrDefault(p =>
				{
					if (p.ConfigurationParameter == null)
					{
						return false;
					}

					string paramName = p.ConfigurationParameter.Name;

					if (paramName == "Frequency")
					{
						paramName = "IRD input frequency";
					}

					if (paramName == "Symbol Rate")
					{
						paramName = "IRD symbol rate";
					}

					return param.Name == paramName;
				});

				if (serviceParam == null)
				{
					overriddenValues.Add(param.ParameterValue);
					continue;
				}

				engine.Log($"Found matching service parameter for job parameter '{param.Name}':\n{JsonConvert.SerializeObject(serviceParam)}");

				if (serviceParam.ConfigurationParameterValue.Type == DomHelpers.SlcConfigurations.SlcConfigurationsIds.Enums.Type.Number)
				{
					engine.Log($"Overriding parameter '{param.Name}' with value '{serviceParam.ConfigurationParameterValue.DoubleValue}'");
					overriddenValues.Add(new Skyline.DataMiner.Utils.MediaOps.Common.IOData.Workflows.Scripts.ConfigurationHandler.ProfileParameterValue
					{
						ProfileParameterId = param.ParameterValue.ProfileParameterId,
						DoubleValue = serviceParam.ConfigurationParameterValue.DoubleValue,
					});
				}
				else
				{
					engine.Log($"Overriding parameter '{param.Name}' with value '{serviceParam.ConfigurationParameterValue.StringValue}'");
					overriddenValues.Add(new Skyline.DataMiner.Utils.MediaOps.Common.IOData.Workflows.Scripts.ConfigurationHandler.ProfileParameterValue
					{
						ProfileParameterId = param.ParameterValue.ProfileParameterId,
						StringValue = serviceParam.ConfigurationParameterValue.StringValue,
					});
				}
			}

			engine.Log($"Overridden values:\n{JsonConvert.SerializeObject(overriddenValues)}");

			if (overriddenValues.Count > 0)
			{
				var editConfigurationAction = new Skyline.DataMiner.Utils.MediaOps.Common.IOData.Workflows.Scripts.ConfigurationHandler.EditConfigurationAction
				{
					Context = new Skyline.DataMiner.Utils.MediaOps.Common.IOData.Workflows.Scripts.ConfigurationHandler.ConfigurationContext
					{
						DomInstanceId = jobId,
						Target = Skyline.DataMiner.Utils.MediaOps.Common.IOData.Workflows.Scripts.ConfigurationHandler.ConfigurationTarget.Job,
					},
					DomConfigurationId = domConfiguration.InstanceId,
					OverriddenValues = overriddenValues,
				};
				editConfigurationAction.SendToConfigurationHandler(engine);
			}
		}

		private void TrySetMonitoringSettingsForJob(JobsInstance job)
		{
			job.MonitoringSettings.AtJobStart = SlcWorkflowIds.Enums.Atjobstart.CreateServiceAtWorkflowStart;
			job.MonitoringSettings.AtJobEnd = SlcWorkflowIds.Enums.Atjobend.DeleteServiceIfOneExists;
		}

		private Guid GetDomConfigurationIdFromJob(IEngine engine, WorkflowHandler workflowHandler, Guid domInstanceId)
		{
			var domJob = workflowHandler.GetJobByDomInstanceId(domInstanceId);
			if (domJob == null)
			{
				engine.Log($"No job available with ID '{domInstanceId}'.");
				return Guid.Empty;
			}

			return domJob.JobExecution?.ConfigurationId ?? Guid.Empty;
		}

		private List<Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.ConfigurationParameter> GetFilteredConfigurationParameters(IEngine engine, List<Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.ConfigurationParameterValue> serviceParams)
		{
			FilterElement<Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.ConfigurationParameter> filterConfigParams =
				new ORFilterElement<Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.ConfigurationParameter>();
			var usedConfigurationParameterIds = serviceParams
				.Where(x => x?.ConfigurationParameterId != null && x.ConfigurationParameterId != Guid.Empty)
				.Select(x => x.ConfigurationParameterId)
				.ToList()
												?? new List<Guid>();
			foreach (Guid guid in usedConfigurationParameterIds)
			{
				filterConfigParams = filterConfigParams.OR(ConfigurationParameterExposers.Guid.Equal(guid));
			}

			var configurationParameters = !filterConfigParams.isEmpty()
				? new DataHelperConfigurationParameter(engine.GetUserConnection()).Read(filterConfigParams)
				: new List<Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.ConfigurationParameter>();
			return configurationParameters;
		}
	}
}