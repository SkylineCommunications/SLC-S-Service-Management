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
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.ServiceManagement;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM;
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

		private static void DeleteServiceItemFromInstance(DataHelpersServiceManagement repo, Models.ServiceOrder domInstance, Guid serviceOrderItemId)
		{
			var itemToRemove = domInstance.OrderItems.FirstOrDefault(x => x.ServiceOrderItem.ID == serviceOrderItemId);
			if (itemToRemove == null)
			{
				throw new InvalidOperationException($"No Service order item exists with ID '{serviceOrderItemId}' to remove");
			}

			if (!repo.ServiceOrderItems.TryDelete(itemToRemove.ServiceOrderItem))
			{
				throw new InvalidOperationException("Failed to remove the Service Order item");
			}

			domInstance.OrderItems.Remove(itemToRemove);
			repo.ServiceOrders.CreateOrUpdate(domInstance);
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

			var repo = new DataHelpersServiceManagement(_engine.GetUserConnection());
			var orderItemInstance = repo.ServiceOrders.Read(ServiceOrderExposers.Guid.Equal(domId)).FirstOrDefault();
			if (orderItemInstance == null)
			{
				return;
			}

			DeleteServiceItemFromInstance(repo, orderItemInstance, serviceOrderItemId);
		}
	}
}