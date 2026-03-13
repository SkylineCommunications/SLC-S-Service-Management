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
	using Library.Dom;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.ServiceManagement;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM;
	using Skyline.DataMiner.Utils.ServiceManagement.Common.Extensions;
	using Skyline.DataMiner.Utils.ServiceManagement.Common.IAS;
	using Models = Skyline.DataMiner.ProjectApi.ServiceManagement.API.ServiceManagement.Models;

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

		private void DeleteServiceItemFromInstance(DataHelperService helper, Skyline.DataMiner.ProjectApi.ServiceManagement.API.ServiceManagement.Models.Service service, string label)
		{
			var serviceItemToRemove = service.ServiceItems.FirstOrDefault(x => x.Label == label);
			if (serviceItemToRemove == null)
			{
				return;
			}

			if (serviceItemToRemove.LinkedReferenceStillActive(_engine))
			{
				return;
			}

			service.ServiceItems.Remove(serviceItemToRemove);

			var id = serviceItemToRemove.ID.ToString();
			var relationships = service.ServiceItemsRelationships.Where(r => r.ParentServiceItem == id || r.ChildServiceItem == id).ToList();
			foreach (var r in relationships)
			{
				service.ServiceItemsRelationships.Remove(r);
			}

			helper.CreateOrUpdate(service);
		}

		private void DeleteServiceItemFromInstance(DataHelperServiceSpecification helper, Models.ServiceSpecification spec, string label)
		{
			var serviceItemToRemove = spec.ServiceItems.FirstOrDefault(x => x.Label == label);
			if (serviceItemToRemove == null)
			{
				return;
			}

			if (serviceItemToRemove.LinkedReferenceStillActive(_engine))
			{
				return;
			}

			spec.ServiceItems.Remove(serviceItemToRemove);

			var id = serviceItemToRemove.ID.ToString();
			var relationships = spec.ServiceItemsRelationships.Where(r => r.ParentServiceItem == id || r.ChildServiceItem == id).ToList();
			foreach (var r in relationships)
			{
				spec.ServiceItemsRelationships.Remove(r);
			}

			helper.CreateOrUpdate(spec);
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

			var dataHelperService = new DataHelperService(_engine.GetUserConnection());
			var service = dataHelperService.Read(ServiceExposers.Guid.Equal(domId)).FirstOrDefault();
			if (service != null)
			{
				DeleteServiceItemFromInstance(dataHelperService, service, serviceItemLabel);
				return;
			}

			var dataHelperServiceSpecification = new DataHelperServiceSpecification(_engine.GetUserConnection());
			var spec = dataHelperServiceSpecification.Read(ServiceSpecificationExposers.Guid.Equal(domId)).FirstOrDefault();
			if (spec != null)
			{
				DeleteServiceItemFromInstance(dataHelperServiceSpecification, spec, serviceItemLabel);
				return;
			}

			throw new InvalidOperationException($"No item with ID '{domId}' found on the system!");
		}
	}
}