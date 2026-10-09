/*
****************************************************************************
*  Copyright (c),  Skyline Communications NV  All Rights Reserved.    *
****************************************************************************

Revision History:

DATE        VERSION        AUTHOR            COMMENTS

dd/mm/2025    1.0.0.1        XXX, Skyline    Initial version
09/10/2026    1.0.0.2        SKA, Skyline    DataMinerApp support
****************************************************************************
*/
namespace SLC_SM_IAS_Add_Service_Order_Item_1
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
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
				if (!String.IsNullOrWhiteSpace(_engine.ReadScriptParamFromApp("Payload")))
				{
					RunApiSafe();
					return;
				}

				RunSafe();
			}
			catch (ScriptAbortException)
			{
				// Catch normal abort exceptions (engine.ExitFail or engine.ExitSuccess)
				throw;
			}
			catch (ScriptForceAbortException)
			{
				// Catch forced abort exceptions, caused via external maintenance messages.
				throw;
			}
			catch (ScriptTimeoutException)
			{
				// Catch timeout exceptions for when a script has been running for too long.
				throw;
			}
			catch (InteractiveUserDetachedException)
			{
				// Catch a user detaching from the interactive script by closing the window.
				// Only applicable for interactive scripts, can be removed for non-interactive scripts.
				throw;
			}
			catch (Exception e)
			{
				engine.HandleUnexpectedException(e);
			}
		}

		private void RunApiSafe()
		{
			string actionRaw = _engine.ReadScriptParamFromApp("Action");
			if (!Enum.TryParse(actionRaw, true, out Action action))
			{
				throw new InvalidOperationException("No Action provided to the service-order-item API mode.");
			}

			var payload = _engine.ReadJsonPayload<ApiPayload>("Payload", "service-order-item");
			if (String.IsNullOrWhiteSpace(payload.Name))
			{
				throw new InvalidOperationException("A service-order-item name is required.");
			}

			var api = _engine.GetUserConnection().GetServiceManagementApiHelper("Service Ordering");
			Guid domId = _engine.ReadScriptParamFromApp<Guid>("DOM ID");
			var order = api.ServiceOrder.ServiceOrders.Read(ServiceOrderExposers.Identifier.Equal(domId.ToString())).FirstOrDefault()
				?? throw new InvalidOperationException($"No service order with ID '{domId}' was found.");

			Models.ServiceOrderItem serviceOrderItem;
			if (action == Action.Add)
			{
				serviceOrderItem = new Models.ServiceOrderItem
				{
					Identifier = Guid.NewGuid().ToString(),
					ServiceInfo = new Models.ServiceOrderItemServiceInfo
					{
						Configurations = new List<Skyline.DataMiner.SDM.SdmObjectReference<Models.ServiceOrderItemConfigurationValue>>(),
					},
				};
			}
			else
			{
				Guid itemId = _engine.ReadScriptParamFromApp<Guid>("Service Order Item ID");
				serviceOrderItem = api.ServiceOrder.ServiceOrderItems.Read(ServiceOrderItemExposers.Identifier.Equal(itemId.ToString())).FirstOrDefault()
					?? throw new InvalidOperationException($"No service order item with ID '{itemId}' was found.");
				if (!(order.OrderItems ?? new List<Models.ServiceOrderEntry>())
					.Any(entry => entry != null && entry.ServiceOrderItemId != null && String.Equals(entry.ServiceOrderItemId.Identifier, itemId.ToString(), StringComparison.OrdinalIgnoreCase)))
				{
					throw new InvalidOperationException($"Service order item with ID '{itemId}' is not linked to order '{domId}'.");
				}
			}

			serviceOrderItem.Name = payload.Name.Trim();
			serviceOrderItem.Description = payload.Description ?? String.Empty;
			serviceOrderItem.Action = String.IsNullOrWhiteSpace(payload.ItemAction) ? "NoChange" : payload.ItemAction;
			serviceOrderItem.StartTime = ParseLocalDate(payload.StartTime);
			serviceOrderItem.EndTime = payload.IndefiniteRuntime ? default(DateTime?) : ParseLocalDate(payload.EndTime);
			serviceOrderItem.IndefiniteRuntime = payload.IndefiniteRuntime;
			serviceOrderItem.ServiceInfo = serviceOrderItem.ServiceInfo ?? new Models.ServiceOrderItemServiceInfo();
			serviceOrderItem.ServiceInfo.ServiceCategoryId = ToCategoryReference(payload.CategoryId);
			serviceOrderItem.ServiceInfo.SpecificationId = ToSpecificationReference(payload.SpecificationId);
			serviceOrderItem.ServiceInfo.ServiceId = ToServiceReference(payload.ServiceId);
			serviceOrderItem.ServiceInfo.Configurations = serviceOrderItem.ServiceInfo.Configurations
				?? new List<Skyline.DataMiner.SDM.SdmObjectReference<Models.ServiceOrderItemConfigurationValue>>();

			if (action == Action.Add)
			{
				api.ServiceOrder.ServiceOrderItems.Create(serviceOrderItem);
				order.OrderItems = order.OrderItems ?? new List<Models.ServiceOrderEntry>();
				order.OrderItems.Add(new Models.ServiceOrderEntry { ServiceOrderItemId = serviceOrderItem });
				api.ServiceOrder.ServiceOrders.Update(order);
			}
			else
			{
				api.ServiceOrder.ServiceOrderItems.Update(serviceOrderItem);
			}
		}

		private static DateTime? ParseLocalDate(string value)
		{
			if (String.IsNullOrWhiteSpace(value))
			{
				return null;
			}

			if (!DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out DateTime parsed))
			{
				throw new InvalidOperationException($"Invalid service-order-item date '{value}'.");
			}

			return parsed.ToUniversalTime();
		}

		private static Skyline.DataMiner.SDM.SdmObjectReference<Models.ServiceCategory> ToCategoryReference(string identifier)
		{
			return String.IsNullOrWhiteSpace(identifier)
				? null
				: new Skyline.DataMiner.SDM.SdmObjectReference<Models.ServiceCategory>(identifier);
		}

		private static Skyline.DataMiner.SDM.SdmObjectReference<Models.ServiceSpecification> ToSpecificationReference(string identifier)
		{
			return String.IsNullOrWhiteSpace(identifier)
				? null
				: new Skyline.DataMiner.SDM.SdmObjectReference<Models.ServiceSpecification>(identifier);
		}

		private static Skyline.DataMiner.SDM.SdmObjectReference<Models.Service> ToServiceReference(string identifier)
		{
			return String.IsNullOrWhiteSpace(identifier)
				? null
				: new Skyline.DataMiner.SDM.SdmObjectReference<Models.Service>(identifier);
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
