/*
****************************************************************************
*  Copyright (c),  Skyline Communications NV  All Rights Reserved.    *
****************************************************************************

Revision History:

DATE        VERSION        AUTHOR            COMMENTS

03/09/2026  1.0.0.1        SKA           Initial version
****************************************************************************
*/
namespace SLC_SM_Clear_Import_Data
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.ServiceManagement;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM;
	using ServiceModels = Skyline.DataMiner.ProjectApi.ServiceManagement.API.ServiceManagement.Models;

	/// <summary>
	/// Clears Service Management entities required for a clean demo import baseline.
	/// </summary>
	public class Script
	{
		/// <summary>
		/// The script entry point.
		/// </summary>
		/// <param name="engine">Link with SLAutomation process.</param>
		public void Run(IEngine engine)
		{
			try
			{
				RunSafe(engine);
			}
			catch (ScriptAbortException)
			{
			}
			catch (ScriptForceAbortException)
			{
			}
			catch (ScriptTimeoutException)
			{
			}
			catch (InteractiveUserDetachedException)
			{
			}
			catch (Exception e)
			{
				engine.ExitFail($"Run|{e.Message}");
			}
		}

		private static void RunSafe(IEngine engine)
		{
			var connection = engine.GetUserConnection();
			var repo = new DataHelpersServiceManagement(connection);
			var orderHelper = new DataHelperServiceOrder(connection);
			var orderItemHelper = new DataHelperServiceOrderItem(connection);
			var serviceHelper = new DataHelperService(connection);
			var specificationHelper = new DataHelperServiceSpecification(connection);

			int orderItemsDeleted = 0;
			int ordersDeleted = 0;
			int servicesDeleted = 0;
			int specificationsDeleted = 0;
			int serviceItemsUnlinked = 0;
			int orderItemsUnlinked = 0;
			var failures = new List<string>();

			// 1) Unlink order items from orders before deletion.
			var ordersToUnlink = orderHelper.Read();
			foreach (ServiceModels.ServiceOrder order in ordersToUnlink)
			{
				if (order?.OrderItems == null || order.OrderItems.Count == 0)
				{
					continue;
				}

				try
				{
					orderItemsUnlinked += order.OrderItems.Count(item => item?.ServiceOrderItem != null);
					order.OrderItems = new List<ServiceModels.ServiceOrderItems>();
					orderHelper.CreateOrUpdate(order);
				}
				catch (Exception e)
				{
					failures.Add($"Order '{order?.OrderId ?? order?.ID.ToString() ?? "<unknown>"}': unlink items failed: {e.Message}");
				}
			}

			// 2) Delete order items.
			foreach (ServiceModels.ServiceOrderItem orderItem in orderItemHelper.Read())
			{
				if (orderItem == null)
				{
					continue;
				}

				try
				{
					if (repo.ServiceOrderItems.TryDelete(orderItem))
					{
						orderItemsDeleted++;
					}
					else
					{
						failures.Add($"Order item '{orderItem.ID}' could not be deleted.");
					}
				}
				catch (Exception e)
				{
					failures.Add($"Order item '{orderItem.ID}': delete failed: {e.Message}");
				}
			}

			// 3) Delete orders.
			foreach (ServiceModels.ServiceOrder order in orderHelper.Read())
			{
				if (order == null)
				{
					continue;
				}

				try
				{
					if (repo.ServiceOrders.TryDelete(order))
					{
						ordersDeleted++;
					}
					else
					{
						failures.Add($"Order '{order.OrderId ?? order.ID.ToString()}' could not be deleted.");
					}
				}
				catch (Exception e)
				{
					failures.Add($"Order '{order.OrderId ?? order.ID.ToString()}': delete failed: {e.Message}");
				}
			}

			// 4) Unlink service items/relationships to remove external references before service deletion.
			foreach (ServiceModels.Service service in serviceHelper.Read())
			{
				if (service == null)
				{
					continue;
				}

				try
				{
					serviceItemsUnlinked += service.ServiceItems?.Count ?? 0;
					service.ServiceItems = new List<ServiceModels.ServiceItem>();
					service.ServiceItemsRelationships = new List<ServiceModels.ServiceItemRelationShip>();
					serviceHelper.CreateOrUpdate(service);
				}
				catch (Exception e)
				{
					failures.Add($"Service '{service.ServiceID ?? service.Name ?? service.ID.ToString()}': unlink items failed: {e.Message}");
				}
			}

			// 5) Delete services.
			foreach (ServiceModels.Service service in serviceHelper.Read())
			{
				if (service == null)
				{
					continue;
				}

				try
				{
					if (repo.Services.TryDelete(service))
					{
						servicesDeleted++;
					}
					else
					{
						failures.Add($"Service '{service.ServiceID ?? service.Name ?? service.ID.ToString()}' could not be deleted.");
					}
				}
				catch (Exception e)
				{
					failures.Add($"Service '{service.ServiceID ?? service.Name ?? service.ID.ToString()}': delete failed: {e.Message}");
				}
			}

			// 6) Delete service specifications.
			foreach (ServiceModels.ServiceSpecification specification in specificationHelper.Read())
			{
				if (specification == null)
				{
					continue;
				}

				try
				{
					if (repo.ServiceSpecifications.TryDelete(specification))
					{
						specificationsDeleted++;
					}
					else
					{
						failures.Add($"Service specification '{specification.Name ?? specification.ID.ToString()}' could not be deleted.");
					}
				}
				catch (Exception e)
				{
					failures.Add($"Service specification '{specification.Name ?? specification.ID.ToString()}': delete failed: {e.Message}");
				}
			}

			engine.GenerateInformation(
				$"[Clear Import Data] Completed. Unlinked order-items: {orderItemsUnlinked}, deleted order-items: {orderItemsDeleted}, deleted orders: {ordersDeleted}, unlinked service-items: {serviceItemsUnlinked}, deleted services: {servicesDeleted}, deleted specifications: {specificationsDeleted}, failures: {failures.Count}.");

			if (failures.Count > 0)
			{
				engine.ExitFail($"[Clear Import Data] Failed with {failures.Count} issue(s): {String.Join(" | ", failures)}");
			}
		}
	}
}
