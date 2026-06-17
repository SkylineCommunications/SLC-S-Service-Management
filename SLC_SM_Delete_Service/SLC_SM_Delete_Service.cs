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
	using Library.Dom;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Core.DataMinerSystem.Automation;
	using Skyline.DataMiner.Core.DataMinerSystem.Common;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.ServiceManagement;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM;
	using Skyline.DataMiner.Utils.ServiceManagement.Common.Extensions;
	using Skyline.DataMiner.Utils.ServiceManagement.Common.IAS;
	using SLC_SM_Common.Extensions;

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

			var serviceManagementHelper = new DataHelpersServiceManagement(_engine.GetUserConnection());
			var dms = _engine.GetDms();

			FilterElement<Models.Service> filter = new ORFilterElement<Models.Service>();
			foreach (Guid domId in domIdList)
			{
				filter = filter.OR(ServiceExposers.Guid.Equal(domId));
			}

			var services = !filter.isEmpty() ? serviceManagementHelper.Services.Read(filter) : new List<Models.Service>();
			foreach (var service in services)
			{
				RemoveService(dms, serviceManagementHelper, service);
			}
		}

		private void RemoveService(IDms dms, DataHelpersServiceManagement serviceManagementHelper, Models.Service service)
		{
			if (service.GenerateMonitoringService == true && dms.ServiceExistsSafe(service.Name, out IDmsService dmsService))
			{
				dmsService.Delete();
			}

			_engine.GenerateInformation($"Service that will be removed: {service.ID}/{service.Name}");

			foreach (Models.ServiceItem serviceItem in service.ServiceItems)
			{
				if (serviceItem.LinkedReferenceStillActive(_engine))
				{
					return;
				}
			}

			serviceManagementHelper.Services.TryDelete(service);
		}
	}
}