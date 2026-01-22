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

dd/mm/2025    1.0.0.1        XXX, Skyline    Initial version
****************************************************************************
*/
namespace SLC_SM_IAS_Add_Service_Item
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using DomHelpers.SlcServicemanagement;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.Relationship;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.ServiceManagement;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM;
	using Skyline.DataMiner.Utils.InteractiveAutomationScript;
	using Skyline.DataMiner.Utils.ServiceManagement.Common.Extensions;
	using Skyline.DataMiner.Utils.ServiceManagement.Common.IAS;
	using SLC_SM_IAS_Add_Service_Item.Presenters;
	using SLC_SM_IAS_Add_Service_Item.ScriptModels;
	using SLC_SM_IAS_Add_Service_Item.Views;
	using Models = Skyline.DataMiner.ProjectApi.ServiceManagement.API.ServiceManagement.Models;

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
				scriptModel.ID = serviceInstance.ID;
				scriptModel.Start = serviceInstance.StartTime;
				scriptModel.End = serviceInstance.EndTime;
				return scriptModel;
			}

			if (specInstance != null)
			{
				scriptModel.ID = specInstance.ID;
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

		private void AddOrUpdateServiceItemToInstance(DataHelperService helper, Models.Service instance, Models.ServiceItem newSection, string oldLabel)
		{
			if (instance == null)
			{
				return;
			}

			// Remove old instance first in case of edit
			var oldItem = instance.ServiceItems.FirstOrDefault(x => x.Label == oldLabel);
			if (oldItem != null)
			{
				newSection.ID = oldItem.ID;
				instance.ServiceItems.Remove(oldItem);
			}

			if (newSection.ID < -1)
			{
				// Auto assign new ID
				long[] ids = instance.ServiceItems.Select(x => x.ID).OrderBy(x => x).ToArray();
				newSection.ID = ids.Any() ? ids.Max() + 1 : 0;
			}

			newSection.Icon = instance.Icon; // inherit icon from service.

			AddServiceLink(instance.ID, instance.Name, newSection);

			instance.ServiceItems.Add(newSection);
			helper.CreateOrUpdate(instance);
		}

		private void AddOrUpdateServiceItemToInstance(DataHelperServiceSpecification helper, Models.ServiceSpecification instance, Models.ServiceItem newSection, string oldLabel)
		{
			if (instance == null)
			{
				return;
			}

			// Remove old instance first in case of edit
			var oldItem = instance.ServiceItems.FirstOrDefault(x => x.Label == oldLabel);
			if (oldItem != null)
			{
				newSection.ID = oldItem.ID;
				instance.ServiceItems.Remove(oldItem);
			}

			if (newSection.ID < -1)
			{
				// Auto assign new ID
				long[] ids = instance.ServiceItems.Select(x => x.ID).OrderBy(x => x).ToArray();
				newSection.ID = ids.Any() ? ids.Max() + 1 : 0;
			}

			instance.ServiceItems.Add(newSection);
			helper.CreateOrUpdate(instance);
		}

		private void AddServiceLink(Guid serviceInstanceId, string serviceInstanceName, Models.ServiceItem newSection)
		{
			if (newSection.Type != SlcServicemanagementIds.Enums.ServiceitemtypesEnum.Service)
			{
				return;
			}

			var dataHelper = new DataHelperLink(_engine.GetUserConnection());
			var link = dataHelper.Read(LinkExposers.ParentID.Equal(serviceInstanceId.ToString()).AND(LinkExposers.ChildID.Equal(newSection.ImplementationReference))).FirstOrDefault();
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

			var dataHelperService = new DataHelperService(_engine.GetUserConnection());
			var serviceInstance = dataHelperService.Read(ServiceExposers.Guid.Equal(domId)).FirstOrDefault();
			var dataHelperServiceSpecification = new DataHelperServiceSpecification(_engine.GetUserConnection());
			var specInstance = dataHelperServiceSpecification.Read(ServiceSpecificationExposers.Guid.Equal(domId)).FirstOrDefault();
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

					AddOrUpdateServiceItemToInstance(dataHelperService, serviceInstance, section, label);
					AddOrUpdateServiceItemToInstance(dataHelperServiceSpecification, specInstance, section, label);
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