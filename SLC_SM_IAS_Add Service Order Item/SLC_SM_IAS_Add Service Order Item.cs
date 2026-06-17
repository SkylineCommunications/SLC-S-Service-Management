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
	using System.Linq;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.ServiceManagement;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM;
	using Skyline.DataMiner.Utils.InteractiveAutomationScript;
	using Skyline.DataMiner.Utils.ServiceManagement.Common.Extensions;
	using Skyline.DataMiner.Utils.ServiceManagement.Common.IAS;
	using SLC_SM_IAS_Add_Service_Order_Item_1.Presenters;
	using SLC_SM_IAS_Add_Service_Order_Item_1.Views;

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

		private static void AddOrUpdateServiceItemToInstance(DataHelpersServiceManagement helper, Models.ServiceOrder instance, Models.ServiceOrderItems updatedData)
		{
			helper.ServiceOrderItems.CreateOrUpdate(updatedData.ServiceOrderItem);

			var existingItem = instance.OrderItems.FirstOrDefault(x => x?.ServiceOrderItem?.ID == updatedData.ServiceOrderItem.ID);
			if (existingItem != null)
			{
				// Already linked - nothing to do
				return;
			}

			instance.OrderItems.Add(updatedData);
			helper.ServiceOrders.CreateOrUpdate(instance);
		}

		private static string[] GetServiceItemLabels(Models.ServiceOrder serviceOrdersInstance, string oldLbl)
		{
			var items = serviceOrdersInstance.OrderItems.Where(x => x?.ServiceOrderItem != null).Select(x => x.ServiceOrderItem.Name).ToList();

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

			var repo = new DataHelpersServiceManagement(_engine.GetUserConnection());
			var order = repo.ServiceOrders.Read(ServiceOrderExposers.Guid.Equal(domId)).FirstOrDefault()
				?? throw new InvalidOperationException($"No DOM Instance with ID '{domId}' found on the system.");

			Guid.TryParse(_engine.ReadScriptParamFromApp("Service Order Item ID"), out Guid orderItemid);

			var orderItem = order.OrderItems.FirstOrDefault(x => x.ServiceOrderItem?.ID == orderItemid);

			// Init views
			var view = new ServiceOrderItemView(_engine);
			var presenter = new ServiceOrderItemPresenter(view, repo, GetServiceItemLabels(order, orderItem?.ServiceOrderItem.Name));

			// Events
			view.BtnCancel.Pressed += (sender, args) => throw new ScriptAbortException("OK");
			view.BtnAdd.Pressed += (sender, args) =>
			{
				if (presenter.Validate())
				{
					AddOrUpdateServiceItemToInstance(repo, order, presenter.GetData);
					throw new ScriptAbortException("OK");
				}
			};

			if (action == Action.Add)
			{
				presenter.LoadFromModel(order.OrderItems.Count(x => x.ServiceOrderItem != null));
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
