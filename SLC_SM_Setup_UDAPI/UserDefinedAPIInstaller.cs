namespace SLC_SM_Setup_UDAPI
{
	using System.Linq;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Net.Apps.UserDefinableApis;
	using Skyline.DataMiner.Net.Apps.UserDefinableApis.Actions;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.Utils.ServiceManagement.Common.UDAPI;

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
			// Service Order
			InstallEndpoint(
				apiId: Constants.ApiDefinitions.ServiceOrder_UDAPI_Id,
				name: "Service Order - Orders List",
				description: "UDAPI to retrieve, update or delete service orders.",
				route: "serviceOrder",
				scriptName: Constants.ApiScripts.ServiceOrder_UDAPI);

			InstallEndpoint(
				apiId: Constants.ApiDefinitions.ServiceOrder_ById_UDAPI_Id,
				name: "Service Order - Order By Id",
				description: "UDAPI to retrieve, update or delete a service order by id.",
				route: "serviceOrder/{id}",
				scriptName: Constants.ApiScripts.ServiceOrder_UDAPI);

			InstallEndpoint(
				apiId: Constants.ApiDefinitions.ServiceOrder_ById_Transitions_UDAPI_Id,
				name: "Service Order - Transitions of an Order",
				description: "UDAPI to retrieve or execute service order state transitions.",
				route: "serviceOrder/{id}/transition",
				scriptName: Constants.ApiScripts.ServiceOrder_UDAPI);

			InstallEndpoint(
				apiId: Constants.ApiDefinitions.ServiceOrder_ById_Item_UDAPI_Id,
				name: "Service Order - List Items of an Order",
				description: "UDAPI to create or retrieve the items of a service order.",
				route: "serviceOrder/{id}/item",
				scriptName: Constants.ApiScripts.ServiceOrder_UDAPI);

			InstallEndpoint(
				apiId: Constants.ApiDefinitions.ServiceOrder_ById_ByItemId_UDAPI_Id,
				name: "Service Order - Item By Id of an Order",
				description: "UDAPI to retrieve, update or delete a service order item by id.",
				route: "serviceOrder/{id}/item/{itemId}",
				scriptName: Constants.ApiScripts.ServiceOrder_UDAPI);

			InstallEndpoint(
				apiId: Constants.ApiDefinitions.ServiceOrder_ById_ByItemId_Transition_UDAPI_Id,
				name: "Service Order - Item Transition of an Order",
				description: "UDAPI to execute a service order item state transition.",
				route: "serviceOrder/{id}/item/{itemId}/transition",
				scriptName: Constants.ApiScripts.ServiceOrder_UDAPI);

			// Service Inventory
			InstallEndpoint(
				apiId: Constants.ApiDefinitions.ServiceInventory_UDAPI_Id,
				name: "Service Inventory - Service List",
				description: "UDAPI to create or retrieve services.",
				route: "service",
				scriptName: Constants.ApiScripts.ServiceInventory_UDAPI);

			InstallEndpoint(
				apiId: Constants.ApiDefinitions.ServiceInventory_ById_UDAPI_Id,
				name: "Service Inventory - Service By Id",
				description: "UDAPI to retrieve, update or delete a service by id.",
				route: "service/{id}",
				scriptName: Constants.ApiScripts.ServiceInventory_UDAPI);

			InstallEndpoint(
				apiId: Constants.ApiDefinitions.ServiceInventory_ById_Transitions_UDAPI_Id,
				name: "Service Inventory - Transitions of a Service",
				description: "UDAPI to retrieve or execute service inventory state transitions.",
				route: "service/{id}/transition",
				scriptName: Constants.ApiScripts.ServiceInventory_UDAPI);

			InstallEndpoint(
				apiId: Constants.ApiDefinitions.ServiceInventory_ById_Configuration_UDAPI_Id,
				name: "Service Inventory - Configuration Versions of a Service",
				description: "UDAPI to list or create the configuration versions of a service.",
				route: "service/{id}/configurationVersion",
				scriptName: Constants.ApiScripts.ServiceInventory_UDAPI);

			InstallEndpoint(
				apiId: Constants.ApiDefinitions.ServiceInventory_ById_ByConfigurationId_UDAPI_Id,
				name: "Service Inventory - Configuration Version By Id of a Service",
				description: "UDAPI to retrieve a configuration version of a service by id.",
				route: "service/{id}/configurationVersion/{versionId}",
				scriptName: Constants.ApiScripts.ServiceInventory_UDAPI);

			// Service Specification
			InstallEndpoint(
				apiId: Constants.ApiDefinitions.ServiceSpecification_UDAPI_Id,
				name: "Service Specification - Service Specification List",
				description: "UDAPI to list or create service specifications.",
				route: "serviceSpecification",
				scriptName: Constants.ApiScripts.ServiceSpecification_UDAPI);

			InstallEndpoint(
				apiId: Constants.ApiDefinitions.ServiceSpecification_ById_UDAPI_Id,
				name: "Service Specification - Service Specification By Id",
				description: "UDAPI to retrieve, partially update or delete a service specification by id.",
				route: "serviceSpecification/{id}",
				scriptName: Constants.ApiScripts.ServiceSpecification_UDAPI);

			// Service Category
			InstallEndpoint(
				apiId: Constants.ApiDefinitions.ServiceCategory_UDAPI_Id,
				name: "Service Category - Service Category List",
				description: "UDAPI to list or create service categories.",
				route: "serviceCategory",
				scriptName: Constants.ApiScripts.ServiceCategory_UDAPI);

			InstallEndpoint(
				apiId: Constants.ApiDefinitions.ServiceCategory_ById_UDAPI_Id,
				name: "Service Category - Service Category By Id",
				description: "UDAPI to retrieve or delete a service category by id.",
				route: "serviceCategory/{id}",
				scriptName: Constants.ApiScripts.ServiceCategory_UDAPI);
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

		private void InstallEndpoint(ApiDefinitionId apiId, string name, string description, string route, string scriptName)
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
					ScriptName = scriptName,
				},
			};

			Import(definition);
		}
	}
}