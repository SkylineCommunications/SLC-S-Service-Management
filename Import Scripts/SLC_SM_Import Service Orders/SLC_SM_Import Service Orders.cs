/*
****************************************************************************
*  Copyright (c),  Skyline Communications NV  All Rights Reserved.    *
****************************************************************************

Revision History:

DATE        VERSION        AUTHOR            COMMENTS

31/08/2026  1.0.0.1        SKA           Initial version
****************************************************************************
*/
namespace SLC_SM_Import_Service_Orders
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.IO;
	using System.Linq;
	using System.Security.Cryptography;
	using System.Text;
	using DomHelpers.SlcConfigurations;
	using Newtonsoft.Json;
	using Newtonsoft.Json.Linq;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.PeopleAndOrganization;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.ServiceManagement;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM;
	using Skyline.DataMiner.Utils.ServiceManagement.Common.Extensions;
	using Skyline.DataMiner.Utils.ServiceManagement.Common.IAS;
	using ConfigModels = Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models;
	using OrgModels = Skyline.DataMiner.ProjectApi.ServiceManagement.API.PeopleAndOrganization.Models;
	using ServiceModels = Skyline.DataMiner.ProjectApi.ServiceManagement.API.ServiceManagement.Models;
	using static DomHelpers.SlcServicemanagement.SlcServicemanagementIds.Enums.ServiceorderpriorityEnum;

	/// <summary>
	/// Represents a DataMiner Automation script.
	/// </summary>
	public class Script
	{
		private const string DefaultJsonPath = @"C:\Skyline DataMiner\Documents\DMA_COMMON_DOCUMENTS\service-orders.json";
		private const string DefaultServiceInventoryJsonPath = @"C:\Skyline DataMiner\Documents\DMA_COMMON_DOCUMENTS\service-inventory.json";
		private const string DefaultCategoriesJsonPath = @"C:\Skyline DataMiner\Documents\DMA_COMMON_DOCUMENTS\categories.json";

		/// <summary>
		/// The script entry point.
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
				RunSafe(engine);
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
				engine.ExitFail($"Run|{e.Message}");
			}
		}

		private static void RunSafe(IEngine engine)
		{
			string jsonPath = engine.ReadScriptParamFromApp("JSON File Path");
			if (String.IsNullOrWhiteSpace(jsonPath))
			{
				jsonPath = DefaultJsonPath;
			}
			string serviceInventoryJsonPath = engine.GetScriptParam("Service Inventory JSON File Path")?.Value;
			if (String.IsNullOrWhiteSpace(serviceInventoryJsonPath))
			{
				serviceInventoryJsonPath = DefaultServiceInventoryJsonPath;
			}
			string categoriesJsonPath = engine.GetScriptParam("Categories JSON File Path")?.Value;
			if (String.IsNullOrWhiteSpace(categoriesJsonPath))
			{
				categoriesJsonPath = DefaultCategoriesJsonPath;
			}

			if (!File.Exists(jsonPath))
			{
				throw new FileNotFoundException($"JSON import file was not found: '{jsonPath}'");
			}

			ServiceOrdersRoot root = JsonConvert.DeserializeObject<ServiceOrdersRoot>(File.ReadAllText(jsonPath));
			if (root?.ServiceOrders == null || root.ServiceOrders.Count == 0)
			{
				throw new InvalidOperationException($"No service orders were found in '{jsonPath}'.");
			}

			var connection = engine.GetUserConnection();
			var serviceOrderHelper = new DataHelperServiceOrder(connection);
			var serviceOrderItemHelper = new DataHelperServiceOrderItem(connection);
			var configHelper = new DataHelpersConfigurations(connection);
			var serviceHelper = new DataHelperService(connection);
			var categoryHelper = new DataHelperServiceCategory(connection);
			var specificationHelper = new DataHelperServiceSpecification(connection);
			var organizationHelper = new DataHelperOrganization(connection);

			var existingOrders = serviceOrderHelper.ReadBasicDetails();
			var existingOrdersByOrderId = existingOrders
				.Where(x => !String.IsNullOrWhiteSpace(x.OrderId))
				.GroupBy(x => x.OrderId, StringComparer.InvariantCultureIgnoreCase)
				.ToDictionary(x => x.Key, x => x.First(), StringComparer.InvariantCultureIgnoreCase);

			var organizationsByName = organizationHelper.Read()
				.Where(x => !String.IsNullOrWhiteSpace(x.Name))
				.GroupBy(x => x.Name.Trim(), StringComparer.InvariantCultureIgnoreCase)
				.ToDictionary(x => x.Key, x => x.First(), StringComparer.InvariantCultureIgnoreCase);
			var organizationsByNormalizedName = organizationsByName.Values
				.Where(x => !String.IsNullOrWhiteSpace(x.Name))
				.GroupBy(x => NormalizeLookupKey(x.Name), StringComparer.InvariantCultureIgnoreCase)
				.ToDictionary(x => x.Key, x => x.First(), StringComparer.InvariantCultureIgnoreCase);
			var servicesByLookupKey = BuildServiceReferenceLookup(serviceHelper);
			ExtendServiceLookupWithInventoryAliases(servicesByLookupKey, ReadServiceInventorySources(serviceInventoryJsonPath));
			var categorySourceById = ReadCategorySources(categoriesJsonPath);
			var categories = categoryHelper.Read();
			var categoriesByNameAndType = categories
				.Where(x => !String.IsNullOrWhiteSpace(x.Name))
				.GroupBy(x => BuildCategoryKey(x.Type, x.Name), StringComparer.InvariantCultureIgnoreCase)
				.ToDictionary(x => x.Key, x => x.First(), StringComparer.InvariantCultureIgnoreCase);
			var categoriesById = categories
				.GroupBy(x => x.ID)
				.ToDictionary(x => x.Key, x => x.First());
			var specifications = specificationHelper.Read();
			var specificationsByName = specifications
				.Where(x => !String.IsNullOrWhiteSpace(x.Name))
				.GroupBy(x => x.Name, StringComparer.InvariantCultureIgnoreCase)
				.ToDictionary(x => x.Key, x => x.First(), StringComparer.InvariantCultureIgnoreCase);
			var specificationsById = specifications
				.GroupBy(x => x.ID)
				.ToDictionary(x => x.Key, x => x.First());
			var configurationParametersById = configHelper.ConfigurationParameters.Read()
				.ToDictionary(x => x.ID, x => x);

			int created = 0;
			int updated = 0;
			int skipped = 0;
			int orderItemCreated = 0;
			int orderItemUpdated = 0;

			foreach (ServiceOrderImport orderImport in root.ServiceOrders)
			{
				if (orderImport == null)
				{
					skipped++;
					continue;
				}

				string orderId = String.IsNullOrWhiteSpace(orderImport.OrderNumber) ? orderImport.Id : orderImport.OrderNumber;
				if (String.IsNullOrWhiteSpace(orderId))
				{
					skipped++;
					engine.GenerateInformation($"[Import Service Orders] Skipping entry without id/orderNumber.");
					continue;
				}

				bool exists = existingOrdersByOrderId.TryGetValue(orderId, out ServiceModels.ServiceOrder existingBasicOrder);
				ServiceModels.ServiceOrder orderToPersist = exists
					? serviceOrderHelper.Read(ServiceOrderExposers.Guid.Equal(existingBasicOrder.ID)).FirstOrDefault()
					: new ServiceModels.ServiceOrder
					{
						ContactIds = new List<Guid>(),
						OrderItems = new List<ServiceModels.ServiceOrderItems>(),
					};
				if (orderToPersist == null)
				{
					orderToPersist = new ServiceModels.ServiceOrder
					{
						ContactIds = new List<Guid>(),
						OrderItems = new List<ServiceModels.ServiceOrderItems>(),
					};
				}

				string desiredOrderName = FirstNonEmpty(
					orderImport.Label,
					orderImport.Name,
					orderImport.OrderItems?.FirstOrDefault(x => !String.IsNullOrWhiteSpace(x?.Label))?.Label,
					orderImport.OrderItems?.FirstOrDefault(x => !String.IsNullOrWhiteSpace(x?.Name))?.Name,
					orderImport.OrderNumber,
					orderImport.Id);
				orderToPersist.Name = desiredOrderName;
				orderToPersist.OrderId = orderId;
				orderToPersist.ExternalID = orderImport.Id;
				orderToPersist.Description = BuildDescription(orderImport);
				orderToPersist.Priority = DeterminePriority(orderImport);

				if (TryResolveOrganization(orderImport.RelatedParty, organizationsByName, organizationsByNormalizedName, out var organization))
				{
					orderToPersist.OrganizationId = organization.ID;
				}
				else
				{
					orderToPersist.OrganizationId = null;
				}

				if (orderToPersist.OrderItems == null)
				{
					orderToPersist.OrderItems = new List<ServiceModels.ServiceOrderItems>();
				}

				var importedOrderItemStates = UpsertOrderItems(
					orderImport,
					orderToPersist,
					orderId,
					serviceOrderItemHelper,
					configurationParametersById,
					categorySourceById,
					categoriesByNameAndType,
					categoriesById,
					specificationsByName,
					specificationsById,
					servicesByLookupKey,
					ref orderItemCreated,
					ref orderItemUpdated);

				if (orderToPersist.CompletionInfo == null)
				{
					orderToPersist.CompletionInfo = new ServiceModels.ServiceOrderCompletionInfo();
				}

				orderToPersist.CompletionInfo.RequestedStartDate = orderImport.OrderDate?.ToUniversalTime();
				orderToPersist.CompletionInfo.RequestedCompletedDate = orderImport.CompletionDate?.ToUniversalTime();

				if (orderToPersist.CancellationInfo == null)
				{
					orderToPersist.CancellationInfo = new ServiceModels.ServiceOrderCancellationInfo();
				}

				orderToPersist.CancellationInfo.CancellationDate = orderImport.CancellationDate?.ToUniversalTime();
				orderToPersist.CancellationInfo.Reason = orderImport.CancellationReason;

				Guid orderDomId = serviceOrderHelper.CreateOrUpdate(orderToPersist);

				var persistedOrder = serviceOrderHelper.Read(ServiceOrderExposers.Guid.Equal(orderDomId)).FirstOrDefault();
				ApplyOrderItemStates(importedOrderItemStates, persistedOrder, serviceOrderItemHelper, engine);

				persistedOrder = serviceOrderHelper.Read(ServiceOrderExposers.Guid.Equal(orderDomId)).FirstOrDefault();
				ApplyOrderState(orderImport.State, persistedOrder, serviceOrderHelper, engine);

				persistedOrder = serviceOrderHelper.Read(ServiceOrderExposers.Guid.Equal(orderDomId)).FirstOrDefault();
				if (persistedOrder != null && !String.Equals(persistedOrder.Name, desiredOrderName, StringComparison.InvariantCulture))
				{
					persistedOrder.Name = desiredOrderName;
					serviceOrderHelper.CreateOrUpdate(persistedOrder);
				}

				if (exists)
				{
					updated++;
				}
				else
				{
					created++;
					existingOrdersByOrderId[orderId] = orderToPersist;
				}
			}

			engine.GenerateInformation($"[Import Service Orders] Completed. Orders C/U: {created}/{updated}. Order items C/U: {orderItemCreated}/{orderItemUpdated}. Skipped: {skipped}. Source file: {jsonPath}");
		}

		private static Dictionary<Guid, string> UpsertOrderItems(
			ServiceOrderImport orderImport,
			ServiceModels.ServiceOrder orderToPersist,
			string orderId,
			DataHelperServiceOrderItem serviceOrderItemHelper,
			Dictionary<Guid, ConfigModels.ConfigurationParameter> configurationParametersById,
			Dictionary<string, CategoryImport> categorySourceById,
			Dictionary<string, ServiceModels.ServiceCategory> categoriesByNameAndType,
			Dictionary<Guid, ServiceModels.ServiceCategory> categoriesById,
			Dictionary<string, ServiceModels.ServiceSpecification> specificationsByName,
			Dictionary<Guid, ServiceModels.ServiceSpecification> specificationsById,
			Dictionary<string, Guid> servicesByLookupKey,
			ref int orderItemCreated,
			ref int orderItemUpdated)
		{
			var orderItemStatesById = new Dictionary<Guid, string>();

			if (orderImport?.OrderItems == null || orderImport.OrderItems.Count == 0)
			{
				return orderItemStatesById;
			}

			for (int i = 0; i < orderImport.OrderItems.Count; i++)
			{
				var sourceOrderItem = orderImport.OrderItems[i];
				if (sourceOrderItem == null)
				{
					continue;
				}

				string sourceItemId = String.IsNullOrWhiteSpace(sourceOrderItem.Id) ? $"{orderId}-{i + 1}" : sourceOrderItem.Id.Trim();
				Guid sourceItemGuid = ToDeterministicGuid($"service-order-item:{orderId}:{sourceItemId}");
				var existingOrderItem = orderToPersist.OrderItems.FirstOrDefault(x => x?.ServiceOrderItem?.ID == sourceItemGuid);
				bool exists = existingOrderItem != null;

				if (!exists)
				{
					existingOrderItem = new ServiceModels.ServiceOrderItems
					{
						ServiceOrderItem = new ServiceModels.ServiceOrderItem
						{
							ID = sourceItemGuid,
							Configurations = new List<ServiceModels.ServiceOrderItemConfigurationValue>(),
						},
					};
					orderToPersist.OrderItems.Add(existingOrderItem);
				}

				var orderItem = existingOrderItem.ServiceOrderItem;
				if (orderItem.Configurations == null)
				{
					orderItem.Configurations = new List<ServiceModels.ServiceOrderItemConfigurationValue>();
				}

				orderItem.Name = FirstNonEmpty(sourceOrderItem.Label, sourceOrderItem.Name, sourceItemId, sourceOrderItem.ServiceSpecificationId);
				orderItem.Description = BuildOrderItemDescription(sourceOrderItem);
				orderItem.Action = NormalizeAction(sourceOrderItem.Action);

				Guid? resolvedCategoryId = ResolveCategoryId(sourceOrderItem.CategoryId, categorySourceById, categoriesByNameAndType, categoriesById);
				if (resolvedCategoryId.HasValue)
				{
					orderItem.ServiceCategoryId = resolvedCategoryId.Value;
				}
				else if (String.IsNullOrWhiteSpace(sourceOrderItem.CategoryId))
				{
					orderItem.ServiceCategoryId = null;
				}

				Guid? resolvedSpecificationId = ResolveSpecificationId(sourceOrderItem.ServiceSpecificationId, specificationsByName, specificationsById);
				if (resolvedSpecificationId.HasValue)
				{
					orderItem.SpecificationId = resolvedSpecificationId.Value;
				}
				else if (String.IsNullOrWhiteSpace(sourceOrderItem.ServiceSpecificationId))
				{
					orderItem.SpecificationId = null;
				}

				Guid? resolvedServiceId = ResolveServiceReferenceId(sourceOrderItem.ResultingServiceId, servicesByLookupKey);
				if (resolvedServiceId.HasValue)
				{
					orderItem.ServiceId = resolvedServiceId.Value;
				}
				else if (String.IsNullOrWhiteSpace(sourceOrderItem.ResultingServiceId))
				{
					orderItem.ServiceId = null;
				}

				orderItem.Configurations = BuildConfigurations(sourceOrderItem.ParameterValues, configurationParametersById);
				serviceOrderItemHelper.CreateOrUpdate(orderItem);
				orderItemStatesById[sourceItemGuid] = sourceOrderItem.State;

				if (exists)
				{
					orderItemUpdated++;
				}
				else
				{
					orderItemCreated++;
				}
			}

			return orderItemStatesById;
		}

		private static string BuildDescription(ServiceOrderImport orderImport)
		{
			_ = orderImport;
			return String.Empty;
		}

		private static string BuildOrderItemDescription(ServiceOrderItemImport orderItemImport)
		{
			if (!String.IsNullOrWhiteSpace(orderItemImport?.Note))
			{
				return orderItemImport.Note.Trim();
			}

			return String.Empty;
		}

		private static List<ServiceModels.ServiceOrderItemConfigurationValue> BuildConfigurations(
			List<ParameterValueImport> parameterValues,
			Dictionary<Guid, ConfigModels.ConfigurationParameter> configurationParametersById)
		{
			var configurations = new List<ServiceModels.ServiceOrderItemConfigurationValue>();
			foreach (var parameterValue in parameterValues ?? Enumerable.Empty<ParameterValueImport>())
			{
				if (parameterValue == null || String.IsNullOrWhiteSpace(parameterValue.ParameterId))
				{
					continue;
				}

				Guid parameterId = ToDeterministicGuid($"cfg:param:{parameterValue.ParameterId}");
				configurationParametersById.TryGetValue(parameterId, out ConfigModels.ConfigurationParameter parameterDefinition);

				var value = new ConfigModels.ConfigurationParameterValue
				{
					ID = Guid.NewGuid(),
					ConfigurationParameterId = parameterDefinition?.ID ?? parameterId,
					Label = parameterDefinition?.Name ?? parameterValue.ParameterId,
					Type = parameterDefinition?.Type ?? InferType(parameterValue.Value),
				};

				ApplyValue(value, parameterValue.Value);
				configurations.Add(new ServiceModels.ServiceOrderItemConfigurationValue
				{
					ID = Guid.NewGuid(),
					Mandatory = false,
					ConfigurationParameter = value,
				});
			}

			return configurations;
		}

		private static SlcConfigurationsIds.Enums.Type InferType(JToken value)
		{
			if (value == null)
			{
				return SlcConfigurationsIds.Enums.Type.Text;
			}

			switch (value.Type)
			{
				case JTokenType.Integer:
				case JTokenType.Float:
					return SlcConfigurationsIds.Enums.Type.Number;
				default:
					return SlcConfigurationsIds.Enums.Type.Text;
			}
		}

		private static void ApplyValue(ConfigModels.ConfigurationParameterValue target, JToken value)
		{
			if (target == null || value == null || value.Type == JTokenType.Null)
			{
				return;
			}

			switch (target.Type)
			{
				case SlcConfigurationsIds.Enums.Type.Number:
					if (Double.TryParse(value.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double numericValue))
					{
						target.DoubleValue = numericValue;
					}

					target.StringValue = TokenToInvariantString(value);
					break;
				default:
					target.StringValue = TokenToInvariantString(value);
					break;
			}
		}

		private static string TokenToInvariantString(JToken value)
		{
			if (value == null || value.Type == JTokenType.Null)
			{
				return String.Empty;
			}

			switch (value.Type)
			{
				case JTokenType.Integer:
					return value.Value<long>().ToString(CultureInfo.InvariantCulture);
				case JTokenType.Float:
					return value.Value<double>().ToString(CultureInfo.InvariantCulture);
				case JTokenType.Boolean:
					return value.Value<bool>() ? "true" : "false";
				default:
					return value.ToString();
			}
		}

		private static string NormalizeAction(string action)
		{
			string normalized = (action ?? String.Empty).Trim().ToLowerInvariant();
			switch (normalized)
			{
				case "add":
					return "Add";
				case "modify":
					return "Modify";
				case "delete":
					return "Delete";
				case "nochange":
				case "no_change":
				case "no-change":
					return "NoChange";
				default:
					return "NoChange";
			}
		}

		private static void ApplyOrderItemStates(
			Dictionary<Guid, string> importedStatesByOrderItemId,
			ServiceModels.ServiceOrder order,
			DataHelperServiceOrderItem serviceOrderItemHelper,
			IEngine engine)
		{
			if (importedStatesByOrderItemId == null || importedStatesByOrderItemId.Count == 0 || order?.OrderItems == null)
			{
				return;
			}

			foreach (var orderItemLink in order.OrderItems.Where(x => x?.ServiceOrderItem != null))
			{
				if (!importedStatesByOrderItemId.TryGetValue(orderItemLink.ServiceOrderItem.ID, out string sourceState))
				{
					continue;
				}

				ApplyOrderItemState(sourceState, orderItemLink.ServiceOrderItem, serviceOrderItemHelper, engine);
			}
		}

		private static void ApplyOrderItemState(
			string sourceState,
			ServiceModels.ServiceOrderItem orderItem,
			DataHelperServiceOrderItem serviceOrderItemHelper,
			IEngine engine)
		{
			if (orderItem == null || String.IsNullOrWhiteSpace(sourceState))
			{
				return;
			}

			string targetState = MapInputStateToOrderItemStatus(sourceState);
			if (String.IsNullOrWhiteSpace(targetState))
			{
				return;
			}

			string currentState = orderItem.Status.ToString();
			var transitions = Enum.GetValues(typeof(DomHelpers.SlcServicemanagement.SlcServicemanagementIds.Behaviors.Serviceorderitem_Behavior.TransitionsEnum))
				.Cast<DomHelpers.SlcServicemanagement.SlcServicemanagementIds.Behaviors.Serviceorderitem_Behavior.TransitionsEnum>()
				.Select(t => new
				{
					Transition = t,
					Parts = t.ToString().Split(new[] { "_To_" }, StringSplitOptions.None),
				})
				.Where(x => x.Parts.Length == 2)
				.Select(x => new { x.Transition, From = x.Parts[0], To = x.Parts[1] })
				.ToList();

			var path = FindTransitionPath(currentState, targetState, transitions.Select(t => (t.From, t.To)).ToList());
			if (path.Count == 0)
			{
				return;
			}

			foreach (int transitionIndex in path)
			{
				var transition = transitions[transitionIndex].Transition;
				engine.GenerateInformation($"[Import Service Orders] Order item status transition: {orderItem.Name} -> {transition}");
				orderItem = serviceOrderItemHelper.UpdateState(orderItem, transition);
			}
		}

		private static void ApplyOrderState(
			string sourceState,
			ServiceModels.ServiceOrder order,
			DataHelperServiceOrder serviceOrderHelper,
			IEngine engine)
		{
			if (order == null || String.IsNullOrWhiteSpace(sourceState))
			{
				return;
			}

			string targetState = MapInputStateToOrderStatus(sourceState);
			if (String.IsNullOrWhiteSpace(targetState))
			{
				return;
			}

			string currentState = order.Status.ToString();
			if (String.Equals(NormalizeStateName(currentState), NormalizeStateName(targetState), StringComparison.InvariantCultureIgnoreCase))
			{
				return;
			}

			var transitions = Enum.GetValues(typeof(DomHelpers.SlcServicemanagement.SlcServicemanagementIds.Behaviors.Serviceorder_Behavior.TransitionsEnum))
				.Cast<DomHelpers.SlcServicemanagement.SlcServicemanagementIds.Behaviors.Serviceorder_Behavior.TransitionsEnum>()
				.Select(t => new
				{
					Transition = t,
					Parts = t.ToString().Split(new[] { "_To_" }, StringSplitOptions.None),
				})
				.Where(x => x.Parts.Length == 2)
				.Select(x => new { x.Transition, From = x.Parts[0], To = x.Parts[1] })
				.ToList();

			var path = FindTransitionPath(currentState, targetState, transitions.Select(t => (t.From, t.To)).ToList());
			if (path.Count == 0)
			{
				return;
			}

			foreach (int transitionIndex in path)
			{
				var transition = transitions[transitionIndex].Transition;
				engine.GenerateInformation($"[Import Service Orders] Order status transition: {order.Name} -> {transition}");
				order = serviceOrderHelper.UpdateState(order, transition);
			}
		}

		private static List<int> FindTransitionPath(string currentState, string targetState, List<(string From, string To)> transitions)
		{
			string currentNormalized = NormalizeStateName(currentState);
			string targetNormalized = NormalizeStateName(targetState);
			if (String.IsNullOrWhiteSpace(currentNormalized) || String.IsNullOrWhiteSpace(targetNormalized) || currentNormalized == targetNormalized)
			{
				return new List<int>();
			}

			var queue = new Queue<string>();
			var previous = new Dictionary<string, (string State, int TransitionIndex)>(StringComparer.InvariantCultureIgnoreCase);
			var visited = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);

			queue.Enqueue(currentState);
			visited.Add(currentState);

			string foundState = null;
			while (queue.Count > 0 && foundState == null)
			{
				string state = queue.Dequeue();
				string normalized = NormalizeStateName(state);
				if (normalized == targetNormalized)
				{
					foundState = state;
					break;
				}

				for (int i = 0; i < transitions.Count; i++)
				{
					if (!String.Equals(NormalizeStateName(transitions[i].From), normalized, StringComparison.InvariantCultureIgnoreCase))
					{
						continue;
					}

					string next = transitions[i].To;
					if (!visited.Add(next))
					{
						continue;
					}

					previous[next] = (state, i);
					queue.Enqueue(next);
				}
			}

			if (String.IsNullOrWhiteSpace(foundState))
			{
				return new List<int>();
			}

			var path = new List<int>();
			string cursor = foundState;
			while (previous.TryGetValue(cursor, out var backLink))
			{
				path.Add(backLink.TransitionIndex);
				cursor = backLink.State;
			}

			path.Reverse();
			return path;
		}

		private static string MapInputStateToOrderStatus(string sourceState)
		{
			switch (NormalizeStateName(sourceState))
			{
				case "new":
					return "New";
				case "pending":
					return "Pending";
				case "acknowledged":
					return "Acknowledged";
				case "inprogress":
					return "Inprogress";
				case "completed":
					return "Completed";
				case "cancelled":
				case "canceled":
					return "Cancelled";
				case "rejected":
					return "Rejected";
				case "held":
					return "Held";
				default:
					return String.Empty;
			}
		}

		private static string MapInputStateToOrderItemStatus(string sourceState)
		{
			switch (NormalizeStateName(sourceState))
			{
				case "new":
					return "New";
				case "pending":
					return "Pending";
				case "acknowledged":
					return "Acknowledged";
				case "inprogress":
					return "Inprogress";
				case "completed":
					return "Completed";
				case "cancelled":
				case "canceled":
					return "Cancelled";
				case "rejected":
					return "Rejected";
				case "failed":
					return "Failed";
				default:
					return String.Empty;
			}
		}

		private static string NormalizeStateName(string value)
		{
			if (String.IsNullOrWhiteSpace(value))
			{
				return String.Empty;
			}

			var chars = value.Where(Char.IsLetterOrDigit).ToArray();
			return new string(chars).ToLowerInvariant();
		}

		private static bool TryResolveOrganization(
			string relatedParty,
			Dictionary<string, OrgModels.Organization> organizationsByName,
			Dictionary<string, OrgModels.Organization> organizationsByNormalizedName,
			out OrgModels.Organization organization)
		{
			organization = null;
			if (String.IsNullOrWhiteSpace(relatedParty))
			{
				return false;
			}

			string trimmedName = relatedParty.Trim();
			if (organizationsByName.TryGetValue(trimmedName, out organization))
			{
				return true;
			}

			return organizationsByNormalizedName.TryGetValue(NormalizeLookupKey(trimmedName), out organization);
		}

		private static string NormalizeLookupKey(string value)
		{
			if (String.IsNullOrWhiteSpace(value))
			{
				return String.Empty;
			}

			var chars = value.Trim().Where(c => Char.IsLetterOrDigit(c) || Char.IsWhiteSpace(c)).ToArray();
			var collapsed = String.Join(" ", new string(chars).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
			return collapsed.ToLowerInvariant();
		}

		private static Dictionary<string, Guid> BuildServiceReferenceLookup(DataHelperService serviceHelper)
		{
			var lookup = new Dictionary<string, Guid>(StringComparer.InvariantCultureIgnoreCase);

			foreach (ServiceModels.Service service in serviceHelper.Read())
			{
				if (service == null || service.ID == Guid.Empty)
				{
					continue;
				}

				AddServiceLookupEntry(lookup, service.ServiceID, service.ID);
				AddServiceLookupEntry(lookup, service.Name, service.ID);
				AddServiceLookupEntry(lookup, service.ID.ToString("D"), service.ID);
			}

			return lookup;
		}

		private static Dictionary<string, CategoryImport> ReadCategorySources(string categoriesJsonPath)
		{
			if (!File.Exists(categoriesJsonPath))
			{
				return new Dictionary<string, CategoryImport>(StringComparer.InvariantCultureIgnoreCase);
			}

			var payload = JsonConvert.DeserializeObject<CategoriesRoot>(File.ReadAllText(categoriesJsonPath));
			return payload?.Categories?
				.Where(c => c != null && !String.IsNullOrWhiteSpace(c.Id))
				.GroupBy(c => c.Id, StringComparer.InvariantCultureIgnoreCase)
				.ToDictionary(g => g.Key, g => g.First(), StringComparer.InvariantCultureIgnoreCase)
				?? new Dictionary<string, CategoryImport>(StringComparer.InvariantCultureIgnoreCase);
		}

		private static string BuildCategoryKey(string type, string name)
		{
			return $"{type ?? String.Empty}|{name ?? String.Empty}";
		}

		private static Guid? ResolveCategoryId(
			string sourceCategoryId,
			Dictionary<string, CategoryImport> categorySourceById,
			Dictionary<string, ServiceModels.ServiceCategory> categoriesByNameAndType,
			Dictionary<Guid, ServiceModels.ServiceCategory> categoriesById)
		{
			if (String.IsNullOrWhiteSpace(sourceCategoryId))
			{
				return null;
			}

			if (categorySourceById.TryGetValue(sourceCategoryId, out CategoryImport sourceCategory)
				&& !String.IsNullOrWhiteSpace(sourceCategory?.CategoryName))
			{
				string key = BuildCategoryKey(sourceCategory.CategoryType, sourceCategory.CategoryName);
				if (categoriesByNameAndType.TryGetValue(key, out ServiceModels.ServiceCategory categoryByTypeAndName))
				{
					return categoryByTypeAndName.ID;
				}
			}

			ServiceModels.ServiceCategory categoryByName = categoriesByNameAndType.Values
				.FirstOrDefault(c => String.Equals(c?.Name, sourceCategoryId, StringComparison.InvariantCultureIgnoreCase));
			if (categoryByName != null)
			{
				return categoryByName.ID;
			}

			if (Guid.TryParse(sourceCategoryId, out Guid explicitCategoryId) && categoriesById.ContainsKey(explicitCategoryId))
			{
				return explicitCategoryId;
			}

			return null;
		}

		private static void ExtendServiceLookupWithInventoryAliases(
			Dictionary<string, Guid> servicesByLookupKey,
			List<ServiceInventoryImport> inventorySources)
		{
			if (servicesByLookupKey == null || inventorySources == null || inventorySources.Count == 0)
			{
				return;
			}

			foreach (ServiceInventoryImport source in inventorySources)
			{
				if (source == null || String.IsNullOrWhiteSpace(source.Id))
				{
					continue;
				}

				Guid? resolved = ResolveServiceReferenceId(source.SystemServiceId, servicesByLookupKey)
					?? ResolveServiceReferenceId(source.Name, servicesByLookupKey);
				if (!resolved.HasValue)
				{
					continue;
				}

				AddServiceLookupEntry(servicesByLookupKey, source.Id, resolved.Value);
			}
		}

		private static List<ServiceInventoryImport> ReadServiceInventorySources(string jsonPath)
		{
			if (String.IsNullOrWhiteSpace(jsonPath) || !File.Exists(jsonPath))
			{
				return new List<ServiceInventoryImport>();
			}

			var root = JsonConvert.DeserializeObject<ServiceInventoryRoot>(File.ReadAllText(jsonPath));
			return root?.Services ?? new List<ServiceInventoryImport>();
		}

		private static void AddServiceLookupEntry(Dictionary<string, Guid> lookup, string key, Guid serviceId)
		{
			string normalized = NormalizeLookupKey(key);
			if (String.IsNullOrWhiteSpace(normalized))
			{
				return;
			}

			if (!lookup.ContainsKey(normalized))
			{
				lookup[normalized] = serviceId;
			}
		}

		private static Guid? ResolveServiceReferenceId(string reference, Dictionary<string, Guid> servicesByLookupKey)
		{
			if (String.IsNullOrWhiteSpace(reference))
			{
				return null;
			}

			string normalizedReference = NormalizeLookupKey(reference);
			if (servicesByLookupKey.TryGetValue(normalizedReference, out Guid resolved))
			{
				return resolved;
			}

			if (Guid.TryParse(reference.Trim(), out Guid explicitServiceId))
			{
				return explicitServiceId;
			}

			return null;
		}

		private static Guid? ResolveSpecificationId(
			string sourceSpecificationId,
			Dictionary<string, ServiceModels.ServiceSpecification> specificationsByName,
			Dictionary<Guid, ServiceModels.ServiceSpecification> specificationsById)
		{
			if (String.IsNullOrWhiteSpace(sourceSpecificationId))
			{
				return null;
			}

			if (specificationsByName.TryGetValue(sourceSpecificationId, out ServiceModels.ServiceSpecification byName))
			{
				return byName.ID;
			}

			if (Guid.TryParse(sourceSpecificationId, out Guid explicitGuid) && specificationsById.ContainsKey(explicitGuid))
			{
				return explicitGuid;
			}

			Guid deterministicGuid = ToDeterministicGuid($"service-spec:{sourceSpecificationId}");
			if (specificationsById.ContainsKey(deterministicGuid))
			{
				return deterministicGuid;
			}

			return null;
		}

		private static Guid ToDeterministicGuid(string value)
		{
			byte[] bytes = Encoding.UTF8.GetBytes(value ?? String.Empty);
			using (var sha256 = SHA256.Create())
			{
				byte[] hash = sha256.ComputeHash(bytes);
				var guidBytes = new byte[16];
				Array.Copy(hash, guidBytes, guidBytes.Length);
				return new Guid(guidBytes);
			}
		}

		private static string FirstNonEmpty(params string[] values)
		{
			foreach (string value in values)
			{
				if (!String.IsNullOrWhiteSpace(value))
				{
					return value;
				}
			}

			return String.Empty;
		}

		private static DomHelpers.SlcServicemanagement.SlcServicemanagementIds.Enums.ServiceorderpriorityEnum? DeterminePriority(ServiceOrderImport orderImport)
		{
			string importance = orderImport?.OrderItems?
				.SelectMany(x => x.ParameterValues ?? Enumerable.Empty<ParameterValueImport>())
				.FirstOrDefault(x => String.Equals(x.ParameterId, "PARAM-IMPORTANCE", StringComparison.InvariantCultureIgnoreCase))
				?.Value?
				.ToString();

			if (String.IsNullOrWhiteSpace(importance))
			{
				return Low;
			}

			switch (importance.Trim().ToLower(CultureInfo.InvariantCulture))
			{
				case "critical":
				case "high":
					return High;
				case "medium":
					return Medium;
				default:
					return Low;
			}
		}

		private sealed class ServiceOrdersRoot
		{
			[JsonProperty("serviceOrders")]
			public List<ServiceOrderImport> ServiceOrders { get; set; }
		}

		private sealed class ServiceOrderImport
		{
			[JsonProperty("id")]
			public string Id { get; set; }

			[JsonProperty("orderNumber")]
			public string OrderNumber { get; set; }

			[JsonProperty("label")]
			public string Label { get; set; }

			[JsonProperty("name")]
			public string Name { get; set; }

			[JsonProperty("state")]
			public string State { get; set; }

			[JsonProperty("relatedParty")]
			public string RelatedParty { get; set; }

			[JsonProperty("orderDate")]
			public DateTime? OrderDate { get; set; }

			[JsonProperty("completionDate")]
			public DateTime? CompletionDate { get; set; }

			[JsonProperty("cancellationDate")]
			public DateTime? CancellationDate { get; set; }

			[JsonProperty("cancellationReason")]
			public string CancellationReason { get; set; }

			[JsonProperty("orderItems")]
			public List<ServiceOrderItemImport> OrderItems { get; set; }
		}

		private sealed class ServiceOrderItemImport
		{
			[JsonProperty("id")]
			public string Id { get; set; }

			[JsonProperty("label")]
			public string Label { get; set; }

			[JsonProperty("name")]
			public string Name { get; set; }

			[JsonProperty("action")]
			public string Action { get; set; }

			[JsonProperty("state")]
			public string State { get; set; }

			[JsonProperty("serviceSpecificationId")]
			public string ServiceSpecificationId { get; set; }

			[JsonProperty("categoryId")]
			public string CategoryId { get; set; }

			[JsonProperty("parameterValues")]
			public List<ParameterValueImport> ParameterValues { get; set; }

			[JsonProperty("resultingServiceId")]
			public string ResultingServiceId { get; set; }

			[JsonProperty("note")]
			public string Note { get; set; }
		}

		private sealed class ParameterValueImport
		{
			[JsonProperty("parameterId")]
			public string ParameterId { get; set; }

			[JsonProperty("value")]
			public JToken Value { get; set; }
		}

		private sealed class CategoriesRoot
		{
			[JsonProperty("categories")]
			public List<CategoryImport> Categories { get; set; }
		}

		private sealed class CategoryImport
		{
			[JsonProperty("id")]
			public string Id { get; set; }

			[JsonProperty("categoryType")]
			public string CategoryType { get; set; }

			[JsonProperty("categoryName")]
			public string CategoryName { get; set; }
		}

		private sealed class ServiceInventoryRoot
		{
			[JsonProperty("services")]
			public List<ServiceInventoryImport> Services { get; set; }
		}

		private sealed class ServiceInventoryImport
		{
			[JsonProperty("id")]
			public string Id { get; set; }

			[JsonProperty("systemServiceId")]
			public string SystemServiceId { get; set; }

			[JsonProperty("name")]
			public string Name { get; set; }
		}
	}
}