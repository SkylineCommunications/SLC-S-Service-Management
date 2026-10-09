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
namespace SLC_SM_IAS_Add_Service_Order_1
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.Linq;
	using DomHelpers.SlcServicemanagement;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Net;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.PeopleAndOrganization;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ApiHelpers;
	using Skyline.DataMiner.Utils.InteractiveAutomationScript;
	using Skyline.DataMiner.Utils.ServiceManagement.Common.Extensions;
	using Skyline.DataMiner.Utils.ServiceManagement.Common.IAS;
	using SLC_SM_IAS_Add_Service_Order_1.Presenters;
	using SLC_SM_IAS_Add_Service_Order_1.Views;
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

		private void RunSafe()
		{
			if (!String.IsNullOrWhiteSpace(_engine.ReadScriptParamFromApp("Payload")))
			{
				RunApiSafe();
				return;
			}

			string actionRaw = _engine.ReadScriptParamFromApp("Action");
			if (!Enum.TryParse(actionRaw, true, out Action action))
			{
				throw new InvalidOperationException("No Action provided as input to the script");
			}

			var api = _engine.GetUserConnection().GetServiceManagementApiHelper("Service Ordering");
			List<Models.ServiceOrder> serviceOrders = api.ServiceOrder.ServiceOrders.Read(new TRUEFilterElement<Models.ServiceOrder>()).ToList();

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
					orderToUpdate.Identifier = Guid.NewGuid().ToString();
					api.ServiceOrder.ServiceOrders.Create(new[] { orderToUpdate });
				}
				else
				{
					api.ServiceOrder.ServiceOrders.Update(orderToUpdate);
				}

				throw new ScriptAbortException("OK");
			};

			if (action == Action.Add)
			{
				presenter.LoadFromModel();
			}
			else
			{
				Guid domId = _engine.ReadScriptParamFromApp<Guid>("DOM ID");
				var orderToEdit = serviceOrders.FirstOrDefault(x => String.Equals(x.Identifier, domId.ToString(), StringComparison.OrdinalIgnoreCase))
								  ?? throw new InvalidOperationException($"No Service Order with ID '{domId}' found on the system!");
				presenter.LoadFromModel(orderToEdit);
			}

			// Run interactive
			_controller.ShowDialog(view);
		}

		private void RunApiSafe()
		{
			string actionRaw = _engine.ReadScriptParamFromApp("Action");
			if (!Enum.TryParse(actionRaw, true, out Action action))
			{
				throw new InvalidOperationException("No Action provided to the service-order API mode.");
			}
			var payload = _engine.ReadJsonPayload<ApiPayload>("Payload", "service-order");
			if (action == Action.Add && String.IsNullOrWhiteSpace(payload.Name))
			{
				throw new InvalidOperationException("A service-order name is required.");
			}

			var connection = _engine.GetUserConnection();
			var api = connection.GetServiceManagementApiHelper("Service Ordering");
			var serviceOrders = api.ServiceOrder.ServiceOrders.Read(new TRUEFilterElement<Models.ServiceOrder>()).ToList();
			Models.ServiceOrder serviceOrder;

			if (action == Action.Add)
			{
				serviceOrder = new Models.ServiceOrder
				{
					Identifier = Guid.NewGuid().ToString(),
					ContactIds = new List<Guid>(),
					OrderItems = new List<Models.ServiceOrderEntry>(),
					OrderId = GetDefaultOrderId(serviceOrders.Select(order => order.OrderId)),
				};
			}
			else
			{
				Guid domId = _engine.ReadScriptParamFromApp<Guid>("DOM ID");
				serviceOrder = serviceOrders.FirstOrDefault(order => String.Equals(order.Identifier, domId.ToString(), StringComparison.OrdinalIgnoreCase))
					?? throw new InvalidOperationException($"No Service Order with ID '{domId}' found on the system.");
			}

			serviceOrder.Name = String.IsNullOrWhiteSpace(payload.Name) ? serviceOrder.Name : payload.Name;
			serviceOrder.Description = payload.Description ?? String.Empty;
			serviceOrder.ExternalID = payload.ExternalId ?? String.Empty;

			string priority = payload.Priority;
			if (!String.IsNullOrWhiteSpace(priority))
			{
				if (!Enum.TryParse(priority, true, out SlcServicemanagementIds.Enums.ServiceorderpriorityEnum parsedPriority))
				{
					throw new InvalidOperationException($"Unsupported service-order priority '{priority}'.");
				}

				serviceOrder.Priority = parsedPriority;
			}

			string organizationName = payload.Organization;
			if (String.IsNullOrWhiteSpace(organizationName))
			{
				serviceOrder.OrganizationId = null;
			}
			else
			{
				var organization = new DataHelperOrganization(connection)
					.Read()
					.FirstOrDefault(item => String.Equals(item.Name, organizationName, StringComparison.OrdinalIgnoreCase));
				if (organization == null)
				{
					throw new InvalidOperationException($"Organization '{organizationName}' was not found.");
				}

				serviceOrder.OrganizationId = organization.ID;
			}

			if (payload.CompletionSettingsEnabled)
			{
				DateTime? requestedStart = ParseOptionalDate(payload.RequestedStart, "requested start");
				DateTime? requestedEnd = ParseOptionalDate(payload.RequestedEnd, "requested completion");
				if (requestedStart.HasValue && requestedEnd.HasValue && requestedEnd < requestedStart)
				{
					throw new InvalidOperationException("Requested completion cannot be earlier than requested start.");
				}

				serviceOrder.CompletionInfo = new Models.ServiceOrderCompletionInfo
				{
					RequestedStartDate = requestedStart,
					RequestedCompletedDate = requestedEnd,
				};
			}
			else
			{
				serviceOrder.CompletionInfo = null;
			}

			if (action == Action.Add)
			{
				api.ServiceOrder.ServiceOrders.Create(new[] { serviceOrder });
			}
			else
			{
				api.ServiceOrder.ServiceOrders.Update(serviceOrder);
			}
		}

		private static DateTime? ParseOptionalDate(string raw, string fieldName)
		{
			if (String.IsNullOrWhiteSpace(raw))
			{
				return null;
			}

			if (!DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsed))
			{
				throw new InvalidOperationException($"Invalid {fieldName} date '{raw}'.");
			}

			return parsed.ToUniversalTime();
		}

		private static string GetDefaultOrderId(IEnumerable<string> usedOrderIds)
		{
			int highest = usedOrderIds
				.Select(orderId => Int32.TryParse(orderId?.Split('-').Last(), out int number) ? number : 0)
				.DefaultIfEmpty()
				.Max();
			return $"ORDER-{highest + 1:000000}";
		}
	}
}