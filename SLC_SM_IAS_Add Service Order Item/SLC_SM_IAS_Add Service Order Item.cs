/*
****************************************************************************
*  Copyright (c),  Skyline Communications NV  All Rights Reserved.    *
****************************************************************************

Revision History:

DATE        VERSION        AUTHOR            COMMENTS

dd/mm/2025    1.0.0.1        XXX, Skyline    Initial version
****************************************************************************
*/
namespace SLC_SM_IAS_Add_Service_Order_Item_1
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ApiHelpers;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;
	using Skyline.DataMiner.SDM;
	using Skyline.DataMiner.Utils.InteractiveAutomationScript;
	using Skyline.DataMiner.Utils.ServiceManagement.Common.Extensions;
	using Skyline.DataMiner.Utils.ServiceManagement.Common.IAS;
	using SLC_SM_IAS_Add_Service_Order_Item_1.Presenters;
	using SLC_SM_IAS_Add_Service_Order_Item_1.Views;
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

		private static void AddOrUpdateServiceItemToInstance(IServiceManagementApiHelper helper, Models.ServiceOrder instance, Models.ServiceOrderEntry updatedData, Models.ServiceOrderItem serviceOrderItem)
		{
			var existingItem = instance.OrderItems.FirstOrDefault(x => x?.ServiceOrderItemId.Identifier == serviceOrderItem.Identifier);
			if (existingItem != null)
			{
				helper.ServiceOrder.ServiceOrderItems.Update(serviceOrderItem);
				return;
			}

			helper.ServiceOrder.ServiceOrderItems.Create(serviceOrderItem);
			instance.OrderItems.Add(updatedData);
			helper.ServiceOrder.ServiceOrders.Update(instance);
		}

		private static string[] GetServiceItemLabels(IServiceManagementApiHelper helper, Models.ServiceOrder serviceOrdersInstance, string oldLbl)
		{
			var itemIds = serviceOrdersInstance.OrderItems.Where(x => x?.ServiceOrderItemId != null).Select(x => x.ServiceOrderItemId.Identifier).ToHashSet();
			var items = helper.ServiceOrder.ServiceOrderItems.Read(new TRUEFilterElement<Models.ServiceOrderItem>())
				.Where(x => itemIds.Contains(x.Identifier))
				.Select(x => x.Name)
				.ToList();

			items.Remove(oldLbl);
			return items.ToArray();
		}

		private void RunSafe()
		{
			Guid domId = _engine.ReadScriptParamFromApp<Guid>("DOM ID");

			string actionRaw = _engine.ReadScriptParamFromApp("Action");
			if (!Enum.TryParse(actionRaw, true, out Action action))
			{
				throw new InvalidOperationException("No Action provided as input to the script");
			}

			var repo = _engine.GetUserConnection().GetServiceManagementApiHelper("Service Ordering");
			var order = repo.ServiceOrder.ServiceOrders.Read(ServiceOrderExposers.Identifier.Equal(domId.ToString())).FirstOrDefault()
				?? throw new InvalidOperationException($"No DOM Instance with ID '{domId}' found on the system.");

			Guid.TryParse(_engine.ReadScriptParamFromApp("Service Order Item ID"), out Guid orderItemid);

			var orderItemEntry = order.OrderItems.FirstOrDefault(x => x.ServiceOrderItemId.Identifier == orderItemid.ToString());
			var orderItem = orderItemEntry == null
				? null
				: repo.ServiceOrder.ServiceOrderItems.Read(ServiceOrderItemExposers.Identifier.Equal(orderItemEntry.ServiceOrderItemId.Identifier)).FirstOrDefault();

			// Init views
			var view = new ServiceOrderItemView(_engine);
			var presenter = new ServiceOrderItemPresenter(view, repo, GetServiceItemLabels(repo, order, orderItem?.Name));

			// Events
			view.BtnCancel.Pressed += (sender, args) => throw new ScriptAbortException("OK");
			view.BtnAdd.Pressed += (sender, args) =>
			{
				if (presenter.Validate())
				{
					AddOrUpdateServiceItemToInstance(repo, order, presenter.GetOrderEntry, presenter.GetData);
					throw new ScriptAbortException("OK");
				}
			};

			if (action == Action.Add)
			{
				presenter.LoadFromModel(order.OrderItems.Count(x => x.ServiceOrderItemId != null));
			}
			else
			{
				presenter.LoadFromModel(orderItem);
			}

			// Run interactive
			_controller.ShowDialog(view);
		}
	}
}
