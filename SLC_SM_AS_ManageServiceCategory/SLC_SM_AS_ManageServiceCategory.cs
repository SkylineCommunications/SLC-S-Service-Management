/*
****************************************************************************
*  Copyright (c),  Skyline Communications NV  All Rights Reserved.    *
****************************************************************************

Revision History:

DATE        VERSION        AUTHOR            COMMENTS

09/10/2026  1.0.0.1        SKA, Skyline		 DataMinerApp support
****************************************************************************
*/

namespace SLC_SM_AS_ManageServiceCategory
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ApiHelpers;
	using Skyline.DataMiner.Utils.ServiceManagement.Common.Extensions;

	using Models = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;

	/// <summary>
	/// Represents the service-category management Automation script.
	/// </summary>
	public class Script
	{
		private enum CategoryAction
		{
			Add,
			Edit,
			Delete,
		}

		/// <summary>
		/// Runs the service-category management action.
		/// </summary>
		/// <param name="engine">Link with SLAutomation.</param>
		public void Run(IEngine engine)
		{
			try
			{
				RunSafe(engine);
			}
			catch (ScriptAbortException)
			{
				throw;
			}
			catch (ScriptForceAbortException)
			{
				throw;
			}
			catch (ScriptTimeoutException)
			{
				throw;
			}
			catch (InteractiveUserDetachedException)
			{
				throw;
			}
			catch (Exception exception)
			{
				engine.HandleUnexpectedException(exception);
			}
		}

		private static void RunSafe(IEngine engine)
		{
			var payload = engine.ReadJsonPayload<ApiPayload>("Payload", "service category");
			string actionRaw = engine.ReadScriptParamFromApp("Action");
			if (!Enum.TryParse(actionRaw, true, out CategoryAction action))
			{
				throw new InvalidOperationException("Unsupported service category action. Supported actions: Add, Edit, Delete.");
			}

			var helper = new ServiceManagementApiHelper(engine.GetUserConnection(), "Service Inventory");
			var categories = helper.ServiceCatalog.ServiceCategories
				.Read(new TRUEFilterElement<Models.ServiceCategory>())
				.ToList();

			if (action == CategoryAction.Add)
			{
				ValidateCategory(payload);
				EnsureUniqueName(categories, payload.Name.Trim());
				helper.ServiceCatalog.ServiceCategories.Create(new Models.ServiceCategory
				{
					Identifier = Guid.NewGuid().ToString(),
					Name = payload.Name.Trim(),
					Type = payload.Type.Trim(),
					Icon = payload.Icon?.Trim() ?? String.Empty,
				});
				return;
			}

			Guid categoryId = engine.ReadScriptParamFromApp<Guid>("domId");
			var category = FindCategory(categories, categoryId);
			if (action == CategoryAction.Delete)
			{
				helper.ServiceCatalog.ServiceCategories.Delete(category);
				return;
			}

			ValidateCategory(payload);
			EnsureUniqueName(categories, payload.Name.Trim(), category.Identifier);
			category.Name = payload.Name.Trim();
			category.Type = payload.Type.Trim();
			category.Icon = payload.Icon?.Trim() ?? String.Empty;
			helper.ServiceCatalog.ServiceCategories.Update(category);
		}

		private static void ValidateCategory(ApiPayload payload)
		{
			if (String.IsNullOrWhiteSpace(payload.Name))
			{
				throw new InvalidOperationException("A category name is required.");
			}

			if (String.IsNullOrWhiteSpace(payload.Type))
			{
				throw new InvalidOperationException("A category type is required.");
			}
		}

		private static void EnsureUniqueName(IEnumerable<Models.ServiceCategory> categories, string name, string excludedId = null)
		{
			if (categories.Any(category =>
				!String.Equals(category.Identifier, excludedId, StringComparison.OrdinalIgnoreCase)
				&& String.Equals(category.Name, name, StringComparison.OrdinalIgnoreCase)))
			{
				throw new InvalidOperationException($"A service category named '{name}' already exists.");
			}
		}

		private static Models.ServiceCategory FindCategory(IEnumerable<Models.ServiceCategory> categories, Guid identifier)
		{
			return categories.FirstOrDefault(category =>
				String.Equals(category.Identifier, identifier.ToString(), StringComparison.OrdinalIgnoreCase))
				?? throw new InvalidOperationException($"Service category '{identifier}' was not found.");
		}
	}
}
