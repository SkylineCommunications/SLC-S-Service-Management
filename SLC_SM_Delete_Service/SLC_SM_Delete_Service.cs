/*
****************************************************************************
*  Copyright (c),  Skyline Communications NV  All Rights Reserved.    *
****************************************************************************

Revision History:

DATE        VERSION        AUTHOR            COMMENTS

13/03/2025    1.0.0.1	   XXX, Skyline    Initial version
****************************************************************************
*/
namespace SLC_SM_Delete_Service
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Core.DataMinerSystem.Automation;
	using Skyline.DataMiner.Core.DataMinerSystem.Common;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ApiHelpers;
	using Skyline.DataMiner.Utils.ServiceManagement.Common.Extensions;
	using Skyline.DataMiner.Utils.ServiceManagement.Common.IAS;
	using SLC_SM_Common.Extensions;
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

		private void RunSafe()
		{
			var domIdList = _engine.ReadScriptParamsFromApp<Guid>("DOM ID");

			// confirmation if the user wants to delete the services
			if (!_engine.ShowConfirmDialog($"Are you sure to you want to delete the selected {domIdList.Count} service(s) from the Inventory?{Environment.NewLine}Note: this will try to remove the linked item(s) (Jobs, Bookings, ...)"))
			{
				return;
			}

			var serviceManagementHelper = new ServiceManagementApiHelper(_engine.GetUserConnection(), "Service Inventory");
			var dms = _engine.GetDms();
			var selectedIds = new HashSet<string>(domIdList.Select(id => id.ToString()), StringComparer.OrdinalIgnoreCase);

			var services = serviceManagementHelper.ServiceInventory.Services
				.Read(new TRUEFilterElement<Models.Service>())
				.Where(service => !String.IsNullOrEmpty(service.Identifier) && selectedIds.Contains(service.Identifier))
				.ToList();

			foreach (var service in services)
			{
				RemoveService(dms, serviceManagementHelper, service);
			}
		}

		private void RemoveService(IDms dms, ServiceManagementApiHelper serviceManagementHelper, Models.Service service)
		{
			if (service.GenerateMonitoringService == true && dms.ServiceExistsSafe(service.Name, out IDmsService dmsService))
			{
				dmsService.Delete();
			}

			_engine.GenerateInformation($"Service that will be removed: {service.Identifier}/{service.Name}");

			foreach (var serviceItem in service.ServiceItems)
			{
				if (HasActiveLinkedReference(serviceManagementHelper, serviceItem))
				{
					return;
				}
			}

			serviceManagementHelper.ServiceInventory.Services.Delete(service);
		}

		private static bool HasActiveLinkedReference(ServiceManagementApiHelper serviceManagementHelper, Models.ServiceItem serviceItem)
		{
			if (!Guid.TryParse(serviceItem.ImplementationReference, out Guid refId) || refId == Guid.Empty)
			{
				return false;
			}

			var itemType = serviceItem.Type?.ToString();
			if (!String.Equals(itemType, "Service", StringComparison.OrdinalIgnoreCase))
			{
				return false;
			}

			return serviceManagementHelper.ServiceInventory.Services
				.Read(Models.ServiceExposers.Identifier.Equal(refId.ToString()))
				.Any();
		}
	}
}