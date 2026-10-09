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
		private readonly UserDefinableApiHelper _helper;

		private const string ApiPrefix = "service-management/v1/";

		internal UserDefinedApiInstaller(IEngine engine)
		{
			_helper = new UserDefinableApiHelper(engine.SendSLNetMessages);
		}

		internal void Install()
		{
			InstallServiceInventoryEndpoint();
			InstallServiceInventoryByIdEndpoint();
		}

		private void InstallServiceInventoryEndpoint()
		{
			var definition = new ApiDefinition
			{
				ID = Constants.ApiDefinitions.ServiceInventory_UDAPI_Id,
				Name = "Service Inventory",
				Description = "UDAPI to create or retrieve services.",
				ActionType = ActionType.AutomationScript,
				Route = $"{ApiPrefix}service",
				ActionMeta = new AutomationScriptActionMeta
				{
					InputType = InputType.RawBody,
					ScriptName = Constants.ApiScripts.ServiceInventory_UDAPI,
				},
			};

			Import(definition);
		}

		private void InstallServiceInventoryByIdEndpoint()
		{
			var definition = new ApiDefinition
			{
				ID = Constants.ApiDefinitions.ServiceInventory_ById_UDAPI_Id,
				Name = "Service Inventory By Id UDAPI",
				Description = "UDAPI to retrieve, partially update, or delete a single service by its identifier.",
				ActionType = ActionType.AutomationScript,
				Route = $"{ApiPrefix}service/{{id}}",
				ActionMeta = new AutomationScriptActionMeta
				{
					InputType = InputType.RawBody,
					ScriptName = Constants.ApiScripts.ServiceInventory_UDAPI,
				},
			};

			// Inherit token access from the parent service inventory endpoint so that tokens are the same.
			var parent = _helper.ApiDefinitions.Read(ApiDefinitionExposers.Id.Equal(Constants.ApiDefinitions.ServiceInventory_UDAPI_Id)).FirstOrDefault();
			if (parent != null)
			{
				definition.SecuritySettings = parent.SecuritySettings;
			}

			Import(definition);
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
	}
}