namespace SLC_SM_Setup_UDAPI
{
	using System.Linq;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Net.Apps.UserDefinableApis;
	using Skyline.DataMiner.Net.Apps.UserDefinableApis.Actions;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;

	internal class UserDefinedApiInstaller
	{
		private const string ApiPrefix = "service-management/v1/";
		private readonly UserDefinableApiHelper _helper;

		internal UserDefinedApiInstaller(IEngine engine)
		{
			_helper = new UserDefinableApiHelper(engine.SendSLNetMessages);
		}

		internal void Install()
		{
			RemoveDecommissionedEndpoints();

			// Service Order
			InstallEndpoint(
				apiId: Constants.ApiDefinitions.ServiceOrder_UDAPI_Id,
				name: "Service Order - Orders List",
				description: "UDAPI to retrieve, update or delete service orders.",
				route: "serviceOrder");

			InstallEndpoint(
				apiId: Constants.ApiDefinitions.ServiceOrder_ById_UDAPI_Id,
				name: "Service Order - Order By Id",
				description: "UDAPI to retrieve, update or delete a service order by id.",
				route: "serviceOrder/{id}");

			InstallEndpoint(
				apiId: Constants.ApiDefinitions.ServiceOrder_ById_Transitions_UDAPI_Id,
				name: "Service Order - Transitions of an Order",
				description: "UDAPI to retrieve or execute service order state transitions.",
				route: "serviceOrder/{id}/transition");

			InstallEndpoint(
				apiId: Constants.ApiDefinitions.ServiceOrder_ById_Item_UDAPI_Id,
				name: "Service Order - List Items of an Order",
				description: "UDAPI to create or retrieve the items of a service order.",
				route: "serviceOrder/{id}/item");

			InstallEndpoint(
				apiId: Constants.ApiDefinitions.ServiceOrder_ById_ByItemId_UDAPI_Id,
				name: "Service Order - Item By Id of an Order",
				description: "UDAPI to retrieve, update or delete a service order item by id.",
				route: "serviceOrder/{id}/item/{itemId}");

			InstallEndpoint(
				apiId: Constants.ApiDefinitions.ServiceOrder_ById_ByItemId_Transition_UDAPI_Id,
				name: "Service Order - Item Transition of an Order",
				description: "UDAPI to execute a service order item state transition.",
				route: "serviceOrder/{id}/item/{itemId}/transition");

			InstallEndpoint(
				apiId: Constants.ApiDefinitions.ServiceOrder_ById_ByItemId_Configuration_UDAPI_Id,
				name: "Service Order - Item Configuration",
				description: "UDAPI to retrieve or save a service order item's configuration.",
				route: "serviceOrder/{id}/item/{itemId}/configuration");

			InstallEndpoint(
				apiId: Constants.ApiDefinitions.ServiceOrder_ById_ByItemId_InitializeService_UDAPI_Id,
				name: "Service Order - Initialize Service from Item",
				description: "UDAPI to create and link a service from a service order item.",
				route: "serviceOrder/{id}/item/{itemId}/initializeService");

			// Service Inventory
			InstallEndpoint(
				apiId: Constants.ApiDefinitions.ServiceInventory_UDAPI_Id,
				name: "Service Inventory - Service List",
				description: "UDAPI to create or retrieve services.",
				route: "service");

			InstallEndpoint(
				apiId: Constants.ApiDefinitions.ServiceInventory_ItemOptions_UDAPI_Id,
				name: "Service Inventory - Service Item Options",
				description: "UDAPI to retrieve workflow and SRM booking options for service items.",
				route: "service/itemOptions");

			InstallEndpoint(
				apiId: Constants.ApiDefinitions.ServiceInventory_ById_UDAPI_Id,
				name: "Service Inventory - Service By Id",
				description: "UDAPI to retrieve, update or delete a service by id.",
				route: "service/{id}");

			InstallEndpoint(
				apiId: Constants.ApiDefinitions.ServiceInventory_ById_Transitions_UDAPI_Id,
				name: "Service Inventory - Transitions of a Service",
				description: "UDAPI to retrieve or execute service inventory state transitions.",
				route: "service/{id}/transition");

			InstallEndpoint(
				apiId: Constants.ApiDefinitions.ServiceInventory_ById_ByConfigurationId_Duplicate_UDAPI_Id,
				name: "Service Inventory - Duplicate Configuration Version of a Service",
				description: "UDAPI to duplicate a configuration version of a service as a new inactive version.",
				route: "service/{id}/configuration/{versionId}/duplicate");

			InstallEndpoint(
				apiId: Constants.ApiDefinitions.ServiceInventory_ById_Configuration_Editor_UDAPI_Id,
				name: "Service Inventory - Service Configuration",
				description: "UDAPI to retrieve or save the flat service configuration editor data.",
				route: "service/{id}/configuration");

			InstallEndpoint(
				apiId: Constants.ApiDefinitions.ServiceInventory_ById_Duplicate_UDAPI_Id,
				name: "Service Inventory - Duplicate Service",
				description: "UDAPI to duplicate a service and its configuration.",
				route: "service/{id}/duplicate");

			InstallEndpoint(
				apiId: Constants.ApiDefinitions.ServiceInventory_ById_RelatedOrder_UDAPI_Id,
				name: "Service Inventory - Related Service Orders",
				description: "UDAPI to retrieve the service orders related to a service.",
				route: "service/{id}/relatedOrder");

			InstallEndpoint(
				apiId: Constants.ApiDefinitions.ServiceInventory_ById_Script_UDAPI_Id,
				name: "Service Inventory - Service Scripts",
				description: "UDAPI to list, add, update or remove service scripts.",
				route: "service/{id}/script");

			InstallEndpoint(
				apiId: Constants.ApiDefinitions.ServiceInventory_ById_Item_UDAPI_Id,
				name: "Service Inventory - List Items of a Service",
				description: "UDAPI to create or retrieve the items of a service.",
				route: "service/{id}/item");

			InstallEndpoint(
				apiId: Constants.ApiDefinitions.ServiceInventory_ById_ByItemId_UDAPI_Id,
				name: "Service Inventory - Item By Id of a Service",
				description: "UDAPI to update or delete a service item by id.",
				route: "service/{id}/item/{itemId}");

			// Service Specification
			InstallEndpoint(
				apiId: Constants.ApiDefinitions.ServiceSpecification_UDAPI_Id,
				name: "Service Specification - Service Specification List",
				description: "UDAPI to list or create service specifications.",
				route: "serviceSpecification");

			InstallEndpoint(
				apiId: Constants.ApiDefinitions.ServiceSpecification_ById_UDAPI_Id,
				name: "Service Specification - Service Specification By Id",
				description: "UDAPI to retrieve, partially update or delete a service specification by id.",
				route: "serviceSpecification/{id}");

			InstallEndpoint(
				apiId: Constants.ApiDefinitions.ServiceSpecification_ById_Configuration_UDAPI_Id,
				name: "Service Specification - Service Specification Configuration",
				description: "UDAPI to retrieve or replace the configuration parameters and profiles of a service specification.",
				route: "serviceSpecification/{id}/configuration");

			InstallEndpoint(
				apiId: Constants.ApiDefinitions.ServiceSpecification_ById_Item_UDAPI_Id,
				name: "Service Specification - Add Service Item",
				description: "UDAPI to add a service item to a service specification.",
				route: "serviceSpecification/{id}/item");

			InstallEndpoint(
				apiId: Constants.ApiDefinitions.ServiceSpecification_ById_ByItemId_UDAPI_Id,
				name: "Service Specification - Service Item By Id",
				description: "UDAPI to update or delete a service item in a service specification.",
				route: "serviceSpecification/{id}/item/{itemId}");

			// Service Category
			InstallEndpoint(
				apiId: Constants.ApiDefinitions.ServiceCategory_UDAPI_Id,
				name: "Service Category - Service Category List",
				description: "UDAPI to list or create service categories.",
				route: "serviceCategory");

			InstallEndpoint(
				apiId: Constants.ApiDefinitions.ServiceCategory_ById_UDAPI_Id,
				name: "Service Category - Service Category By Id",
				description: "UDAPI to retrieve or delete a service category by id.",
				route: "serviceCategory/{id}");


			InstallEndpoint(
				apiId: Constants.ApiDefinitions.PlannedChange_UDAPI_Id,
				name: "Planned Change - List",
				description: "UDAPI to retrieve the planned configuration changes of services.",
				route: "plannedChange");
		}

		private void RemoveDecommissionedEndpoints()
		{
			RemoveEndpoint(Constants.ApiDefinitions.ServiceOrder_Overview_UDAPI_Id, "serviceOrder/overview");
			RemoveEndpoint(Constants.ApiDefinitions.ServiceInventory_Overview_UDAPI_Id, "service/overview");
			RemoveEndpoint(Constants.ApiDefinitions.ServiceSpecification_Overview_UDAPI_Id, "serviceSpecification/overview");
			RemoveEndpoint(Constants.ApiDefinitions.ServiceInventory_ById_Configuration_UDAPI_Id, "service/{id}/configurationVersion");
			RemoveEndpoint(Constants.ApiDefinitions.ServiceInventory_ById_ByConfigurationId_UDAPI_Id, "service/{id}/configurationVersion/{versionId}");
			RemoveEndpoint(Constants.ApiDefinitions.MonitoringService_UDAPI_Id, "monitoringService");
		}

		private void RemoveEndpoint(ApiDefinitionId apiId, string route)
		{
			var definitions = _helper.ApiDefinitions.Read(ApiDefinitionExposers.Id.Equal(apiId))
				.Concat(_helper.ApiDefinitions.Read(ApiDefinitionExposers.Route.Equal($"{ApiPrefix}{route}")))
				.GroupBy(definition => definition.ID.Id)
				.Select(group => group.First())
				.ToList();

			foreach (var definition in definitions)
			{
				_helper.ApiDefinitions.Delete(definition);
			}
		}

		private void Import(ApiDefinition definition)
		{
			// Remove other api definition that is using the same route
			var routeUsed = _helper.ApiDefinitions.Read(ApiDefinitionExposers.Route.Equal(definition.Route)).FirstOrDefault();
			if (routeUsed != null && routeUsed.ID.Id != definition.ID.Id)
			{
				_helper.ApiDefinitions.Delete(routeUsed);
			}

			// Import or update the api definition
			var existing = _helper.ApiDefinitions.Read(ApiDefinitionExposers.Id.Equal(definition.ID)).FirstOrDefault();
			if (existing is null)
			{
				_helper.ApiDefinitions.Create(definition);
			}
			else
			{
				definition.SecuritySettings = existing.SecuritySettings;
				_helper.ApiDefinitions.Update(definition);
			}
		}

		private void InstallEndpoint(ApiDefinitionId apiId, string name, string description, string route)
		{
			var definition = new ApiDefinition
			{
				ID = apiId,
				Name = name,
				Description = description,
				ActionType = ActionType.AutomationScript,
				Route = $"{ApiPrefix}{route}",
				ActionMeta = new AutomationScriptActionMeta
				{
					InputType = InputType.RawBody,
					ScriptName = Constants.ApiScripts.ServiceManagement_UDAPI,
				},
			};

			Import(definition);
		}
	}
}