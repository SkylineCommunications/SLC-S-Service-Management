/*
****************************************************************************
*  Copyright (c),  Skyline Communications NV  All Rights Reserved.    *
****************************************************************************

Revision History:

DATE        VERSION        AUTHOR            COMMENTS

dd/mm/2025    1.0.0.1        XXX, Skyline    Initial version
****************************************************************************
*/
namespace SLC_SM_IAS_Add_Service_Order_1
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using Library.Ownership;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.ServiceManagement;
	using Skyline.DataMiner.Utils.InteractiveAutomationScript;
	using Skyline.DataMiner.Utils.ServiceManagement.Common.Extensions;
	using Skyline.DataMiner.Utils.ServiceManagement.Common.IAS;
	using SLC_SM_IAS_Add_Service_Order_1.Presenters;
	using SLC_SM_IAS_Add_Service_Order_1.Views;

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

		private void RunSafe()
		{
			string actionRaw = _engine.ReadScriptParamFromApp("Action");
			if (!Enum.TryParse(actionRaw, true, out Action action))
			{
				throw new InvalidOperationException("No Action provided as input to the script");
			}

			var dataHelperOrders = new DataHelperServiceOrder(_engine.GetUserConnection());
			List<Models.ServiceOrder> serviceOrders = dataHelperOrders.Read();

			var usedOrderItemLabels = serviceOrders.Select(o => o.Name).ToList();
			var usedOrderIds = serviceOrders.Select(o => o.OrderId).ToList();

			// Init views
			var view = new ServiceOrderView(_engine);
			var presenter = new ServiceOrderPresenter(_engine, view, usedOrderItemLabels, usedOrderIds);

			// Events
			view.BtnAdd.Pressed += (sender, args) =>
			{
				if (!presenter.Validate())
				{
					return;
				}

				Models.ServiceOrder orderToUpdate = presenter.GetData;
				if (action == Action.Add)
				{
					// Take ownership if possible when adding new order
					orderToUpdate.TakeOwnershipForOrder(_engine);
				}

				dataHelperOrders.CreateOrUpdate(orderToUpdate);
				throw new ScriptAbortException("OK");
			};

			if (action == Action.Add)
			{
				presenter.LoadFromModel();
			}
			else
			{
				Guid domId = _engine.ReadScriptParamFromApp<Guid>("DOM ID");
				var ordersInstance = serviceOrders.Find(x => x.ID == domId)
				                     ?? throw new InvalidOperationException($"No Service Order with ID '{domId}' found on the system!");
				presenter.LoadFromModel(ordersInstance);
			}

			// Run interactive
			_controller.ShowDialog(view);
		}
	}
}