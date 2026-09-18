/*
****************************************************************************
*  Copyright (c),  Skyline Communications NV  All Rights Reserved.    *
****************************************************************************

Revision History:

DATE        VERSION        AUTHOR            COMMENTS

dd/mm/2025    1.0.0.1        XXX, Skyline    Initial version
****************************************************************************
*/
namespace SLC_SM_IAS_Add_Service_Item
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	using DomHelpers.SlcRelationships;
	using DomHelpers.SlcServicemanagement;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.Relationship;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ApiHelpers;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;
	using Skyline.DataMiner.Utils.InteractiveAutomationScript;
	using Skyline.DataMiner.Utils.ServiceManagement.Common.Extensions;
	using Skyline.DataMiner.Utils.ServiceManagement.Common.IAS;
	using SLC_SM_IAS_Add_Service_Item.Presenters;
	using SLC_SM_IAS_Add_Service_Item.ScriptModels;
	using SLC_SM_IAS_Add_Service_Item.Views;
	using Models = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;

	/// <summary>
	///     Represents a DataMiner Automation script.
	/// </summary>
	public class Script
	{
		private InteractiveController _controller;
		private IEngine _engine;

		private enum Action
		{
			Add,
			Edit,
		}

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
			if (engine.IsInteractive)
			{
				engine.FindInteractiveClient("Failed to run script in interactive mode", 1);
			}

			try
			{
				_engine = engine;
				_controller = new InteractiveController(engine) { ScriptAbortPopupBehavior = ScriptAbortPopupBehavior.HideAlways };
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
			}
		}

		private static IScriptModel GetScriptModel(Models.Service serviceInstance, Models.ServiceSpecification specInstance)
		{
			var scriptModel = new ScriptScriptModel();
			if (serviceInstance != null)
			{
				scriptModel.ID = Guid.Parse(serviceInstance.Identifier);
				scriptModel.Start = serviceInstance.StartTime;
				scriptModel.End = serviceInstance.EndTime;
				return scriptModel;
			}

			if (specInstance != null)
			{
				scriptModel.ID = Guid.Parse(specInstance.Identifier);
				return scriptModel;
			}

			return scriptModel;
		}

		private static string[] GetServiceItemLabels(List<Models.ServiceItem> serviceItems, string oldLbl)
		{
			if (serviceItems == null)
			{
				return Array.Empty<string>();
			}

			var items = serviceItems.Select(x => x.Label).ToList();
			items.Remove(oldLbl);

			return items.ToArray();
		}

		private static Models.ServiceItem GetServiceItemSection(List<Models.ServiceItem> serviceItems, string label)
		{
			return serviceItems?.FirstOrDefault(x => x.Label == label)
			       ?? throw new InvalidOperationException($"No Service Item with label '{label}' exists.");
		}

		private static void NormalizeOptionalSpecificationCollections(Models.ServiceSpecification specification)
		{
			if (specification == null)
			{
				return;
			}

			if (specification.ConfigurationProfiles != null)
			{
				specification.ConfigurationProfiles = specification.ConfigurationProfiles
					.Where(profile =>
						profile != null &&
						!String.IsNullOrWhiteSpace(profile.Identifier))
					.ToList();

				if (specification.ConfigurationProfiles.Count == 0)
				{
					specification.ConfigurationProfiles = null;
				}
			}

			if (specification.ConfigurationParameters != null)
			{
				specification.ConfigurationParameters = specification.ConfigurationParameters
					.Where(parameter =>
						parameter != null &&
						!String.IsNullOrWhiteSpace(parameter.Identifier))
					.ToList();

				if (specification.ConfigurationParameters.Count == 0)
				{
					specification.ConfigurationParameters = null;
				}
			}
		}

		private void AddOrUpdateServiceItemToInstance(IServiceManagementApiHelper helper, Models.Service instance, Models.ServiceItem newSection, string oldLabel)
		{
			if (instance == null)
			{
				return;
			}

			// Remove old instance first in case of edit
			var oldItem = instance.ServiceItems.FirstOrDefault(x => x.Label == oldLabel);
			if (oldItem != null)
			{
				newSection.ServiceItemID = oldItem.ServiceItemID;
				instance.ServiceItems.Remove(oldItem);
			}

			if (!newSection.ServiceItemID.HasValue)
			{
				// Auto assign new ID
				long[] ids = instance.ServiceItems.Where(x => x.ServiceItemID.HasValue).Select(x => x.ServiceItemID.Value).OrderBy(x => x).ToArray();
				newSection.ServiceItemID = ids.Any() ? ids.Max() + 1 : 0;
			}

			newSection.Icon = instance.Icon; // inherit icon from service.

			AddServiceLink(Guid.Parse(instance.Identifier), instance.Name, newSection);
			newSection.Type = null;

			instance.ServiceItems.Add(newSection);
			helper.ServiceInventory.Services.Update(instance);
		}

		private void AddOrUpdateServiceItemToInstance(IServiceManagementApiHelper helper, Models.ServiceSpecification instance, Models.ServiceItem newSection, string oldLabel)
		{
			if (instance == null)
			{
				return;
			}

			// Remove old instance first in case of edit
			var oldItem = instance.ServiceItems.FirstOrDefault(x => x.Label == oldLabel);
			if (oldItem != null)
			{
				newSection.ServiceItemID = oldItem.ServiceItemID;
				instance.ServiceItems.Remove(oldItem);
			}

			if (!newSection.ServiceItemID.HasValue)
			{
				// Auto assign new ID
				long[] ids = instance.ServiceItems.Where(x => x.ServiceItemID.HasValue).Select(x => x.ServiceItemID.Value).OrderBy(x => x).ToArray();
				newSection.ServiceItemID = ids.Any() ? ids.Max() + 1 : 0;
			}

			newSection.Type = null;
			instance.ServiceItems.Add(newSection);
			NormalizeOptionalSpecificationCollections(instance);
			helper.ServiceCatalog.ServiceSpecifications.Update(instance);
		}

		private void AddServiceLink(Guid serviceInstanceId, string serviceInstanceName, Models.ServiceItem newSection)
		{
			if (newSection.Type != SlcServicemanagementIds.Enums.ServiceitemtypesEnum.Service
				|| !_engine.DomModelExists(SlcRelationshipsIds.ModuleId, new[] { SlcRelationshipsIds.Sections.LinkInfo.Id.Id }))
			{
				return;
			}

			var dataHelper = new DataHelperLink(_engine.GetUserConnection());
			var link = dataHelper.Read(Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.LinkExposers.ParentID.Equal(serviceInstanceId.ToString()).AND(Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.LinkExposers.ChildID.Equal(newSection.ImplementationReference))).FirstOrDefault();
			if (link != null)
			{
				// Already linked OK
				return;
			}

			dataHelper.CreateOrUpdate(
				new Skyline.DataMiner.ProjectApi.ServiceManagement.API.Relationship.Models.Link
				{
					ParentID = serviceInstanceId.ToString(),
					ParentName = serviceInstanceName,
					ChildID = newSection.ImplementationReference,
					ChildName = newSection.DefinitionReference,
				});
		}

		private void RunSafe()
		{
			Guid domId = _engine.ReadScriptParamFromApp<Guid>("DOM ID");

			string actionRaw = _engine.ReadScriptParamFromApp("Action");
			if (!Enum.TryParse(actionRaw, true, out Action action))
			{
				throw new InvalidOperationException("No Action provided as input to the script");
			}

			var api = _engine.GetUserConnection().GetServiceManagementApiHelper("Service Inventory");
			var serviceInstance = api.ServiceInventory.Services.Read(ServiceExposers.Identifier.Equal(domId.ToString())).FirstOrDefault();
			var specInstance = api.ServiceCatalog.ServiceSpecifications.Read(ServiceSpecificationExposers.Identifier.Equal(domId.ToString())).FirstOrDefault();
			if (serviceInstance == null && specInstance == null)
			{
				throw new InvalidOperationException($"No DOM Instance with ID '{domId}' found on the system!");
			}

			string label = _engine.ReadScriptParamFromApp("Service Item Label");

			// Init views
			var view = new ServiceItemView(_engine) { IsEnabled = false };
			view.Show(false);
			view.IsEnabled = true;
			var presenter = new ServiceItemPresenter(_engine, view, GetServiceItemLabels(serviceInstance?.ServiceItems ?? specInstance?.ServiceItems, label), GetScriptModel(serviceInstance, specInstance));

			// Events
			view.BtnCancel.Pressed += (sender, args) => throw new ScriptAbortException("OK");
			view.BtnAdd.Pressed += (sender, args) =>
			{
				if (presenter.Validate())
				{
					var section = presenter.Section;
					string jobId = presenter.UpdateJobForWorkFlow(label);
					if (!String.IsNullOrEmpty(jobId))
					{
						section.ImplementationReference = jobId;
					}

					AddOrUpdateServiceItemToInstance(api, serviceInstance, section, label);
					AddOrUpdateServiceItemToInstance(api, specInstance, section, label);
					throw new ScriptAbortException("OK");
				}
			};

			if (action == Action.Add)
			{
				presenter.LoadFromModel();
			}
			else
			{
				presenter.LoadFromModel(GetServiceItemSection(serviceInstance?.ServiceItems ?? specInstance?.ServiceItems, label));
			}

			// Run interactive
			_controller.ShowDialog(view);
		}
	}
}