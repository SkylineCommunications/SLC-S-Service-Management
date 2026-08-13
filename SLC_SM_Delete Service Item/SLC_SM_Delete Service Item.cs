/*
****************************************************************************
*  Copyright (c),  Skyline Communications NV  All Rights Reserved.    *
****************************************************************************

Revision History:

DATE        VERSION        AUTHOR            COMMENTS

dd/mm/2025    1.0.0.1        XXX, Skyline    Initial version
****************************************************************************
*/
namespace SLC_SM_Delete_Service_Item
{
	using System;
	using System.Linq;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ApiHelpers;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;
	using Skyline.DataMiner.Utils.ServiceManagement.Common.Extensions;
	using Skyline.DataMiner.Utils.ServiceManagement.Common.IAS;
	using Models = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;

	/// <summary>
	///     Represents a DataMiner Automation script.
	/// </summary>
	public class Script
	{
		private IEngine _engine;

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
				_engine = engine;
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

		private void DeleteServiceItemFromInstance(IServiceManagementApiHelper helper, Models.Service service, string label)
		{
			var serviceItemToRemove = service.ServiceItems.FirstOrDefault(x => x.Label == label);
			if (serviceItemToRemove == null)
			{
				return;
			}

			if (HasActiveLinkedReference(helper, serviceItemToRemove))
			{
				return;
			}

			service.ServiceItems.Remove(serviceItemToRemove);

			var id = serviceItemToRemove.ServiceItemID.ToString();
			var relationships = service.ServiceItemsRelationships.Where(r => r.ParentServiceItem == id || r.ChildServiceItem == id).ToList();
			foreach (var r in relationships)
			{
				service.ServiceItemsRelationships.Remove(r);
			}

			helper.ServiceInventory.Services.Update(service);
		}

		private void DeleteServiceItemFromInstance(IServiceManagementApiHelper helper, Models.ServiceSpecification spec, string label)
		{
			var serviceItemToRemove = spec.ServiceItems.FirstOrDefault(x => x.Label == label);
			if (serviceItemToRemove == null)
			{
				return;
			}

			if (HasActiveLinkedReference(helper, serviceItemToRemove))
			{
				return;
			}

			spec.ServiceItems.Remove(serviceItemToRemove);

			var id = serviceItemToRemove.ServiceItemID.ToString();
			var relationships = spec.ServiceItemsRelationships.Where(r => r.ParentServiceItem == id || r.ChildServiceItem == id).ToList();
			foreach (var r in relationships)
			{
				spec.ServiceItemsRelationships.Remove(r);
			}

			helper.ServiceCatalog.ServiceSpecifications.Update(spec);
		}

		private static bool HasActiveLinkedReference(IServiceManagementApiHelper helper, Models.ServiceItem serviceItem)
		{
			if (!Guid.TryParse(serviceItem.ImplementationReference, out Guid referenceId) || referenceId == Guid.Empty)
			{
				return false;
			}

			var itemType = serviceItem.Type?.ToString();
			if (!String.Equals(itemType, "Service", StringComparison.OrdinalIgnoreCase))
			{
				return false;
			}

			return helper.ServiceInventory.Services
				.Read(Models.ServiceExposers.Identifier.Equal(referenceId.ToString()))
				.Any();
		}

		private void RunSafe()
		{
			Guid domId = _engine.ReadScriptParamFromApp<Guid>("DOM ID");

			// confirmation if the user wants to delete the services
			if (!_engine.ShowConfirmDialog($"Are you sure to you want to delete the selected service item(s)?{Environment.NewLine}Note: this will try to remove the linked item(s) (Jobs, Bookings, ...)"))
			{
				return;
			}

			string serviceItemLabel = _engine.ReadScriptParamFromApp("Service Item Label");

			var api = _engine.GetUserConnection().GetServiceManagementApiHelper("Service Inventory");
			var service = api.ServiceInventory.Services.Read(ServiceExposers.Identifier.Equal(domId.ToString())).FirstOrDefault();
			if (service != null)
			{
				DeleteServiceItemFromInstance(api, service, serviceItemLabel);
				return;
			}

			var spec = api.ServiceCatalog.ServiceSpecifications.Read(ServiceSpecificationExposers.Identifier.Equal(domId.ToString())).FirstOrDefault();
			if (spec != null)
			{
				DeleteServiceItemFromInstance(api, spec, serviceItemLabel);
				return;
			}

			throw new InvalidOperationException($"No item with ID '{domId}' found on the system!");
		}
	}
}