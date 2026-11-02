/*
****************************************************************************
*  Copyright (c),  Skyline Communications NV  All Rights Reserved.    *
****************************************************************************
Revision History:

DATE        VERSION        AUTHOR            COMMENTS

dd/mm/2025    1.0.0.1        XXX, Skyline    Initial version
****************************************************************************
*/
namespace SLC_SM_Delete_Service_Order_Item_1
{
	using System;
	using System.Linq;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ApiHelpers;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;
	using Skyline.DataMiner.Utils.ServiceManagement.Common.Extensions;
	using Skyline.DataMiner.Utils.ServiceManagement.Common.IAS;

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

		private static void DeleteServiceItemFromInstance(IServiceManagementApiHelper api, ServiceOrder order, Guid serviceOrderItemId)
		{
			var itemToRemove = order.OrderItems.FirstOrDefault(x => String.Equals(x.ServiceOrderItemId.Identifier, serviceOrderItemId.ToString(), StringComparison.OrdinalIgnoreCase));
			if (itemToRemove == null)
			{
				throw new InvalidOperationException($"No Service order item exists with ID '{serviceOrderItemId}' to remove");
			}

			// Remove the reference first so the Service Order Item is no longer linked when it is deleted.
			order.OrderItems.Remove(itemToRemove);
			api.ServiceOrder.ServiceOrders.Update(order);

			var serviceOrderItem = api.ServiceOrder.ServiceOrderItems.Read(ServiceOrderItemExposers.Identifier.Equal(serviceOrderItemId.ToString())).FirstOrDefault();
			if (serviceOrderItem != null)
			{
				api.ServiceOrder.ServiceOrderItems.Delete(serviceOrderItem);
			}
		}

		private void RunSafe()
		{
			Guid domId = _engine.ReadScriptParamFromApp<Guid>("DOM ID");

			Guid serviceOrderItemId = _engine.ReadScriptParamFromApp<Guid>("Service Order Item ID");

			// confirmation if the user wants to delete the services
			if (!_engine.ShowConfirmDialog($"Are you sure to you want to delete the selected service order item(s)?"))
			{
				return;
			}

			var api = _engine.GetUserConnection().GetServiceManagementApiHelper("Service Ordering");
			var order = api.ServiceOrder.ServiceOrders.Read(ServiceOrderExposers.Identifier.Equal(domId.ToString())).FirstOrDefault();
			if (order == null)
			{
				return;
			}

			DeleteServiceItemFromInstance(api, order, serviceOrderItemId);
		}
	}
}