namespace SLCSMUDAPIServiceOrder
{
	using System;
	using Newtonsoft.Json;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Net.Apps.UserDefinableApis;
	using Skyline.DataMiner.Net.Apps.UserDefinableApis.Actions;
	using Skyline.DataMiner.Utils.UserDefinedApiToolkit;

	using SLC_SM_UDAPI_ServiceOrder.Helpers;

	/// <summary>
	/// Represents a DataMiner user-defined API.
	/// </summary>
	public class Script
	{
		private static IUserDefinedApi _api;

		/// <summary>
		/// The API trigger.
		/// </summary>
		/// <param name="engine">Link with SLAutomation process.</param>
		/// <param name="requestData">Holds the API request data.</param>
		/// <returns>An object with the script API output data.</returns>
		[AutomationEntryPoint(AutomationEntryPointType.Types.OnApiTrigger)]
		public ApiTriggerOutput OnApiTrigger(IEngine engine, ApiTriggerInput requestData)
		{
			try
			{
				if (_api is null)
				{
					_api = UserDefinedApi.CreateBuilder()
						.AddControllers()
						.AddServices()
						.Build();
				}

				return _api.Run(engine, requestData);
			}
			catch (System.Exception ex)
			{
				engine.Log($"Service Order UDAPI failed: {ex}");
				return new ApiTriggerOutput
				{
					ResponseBody = String.Empty,
					ResponseCode = (int)StatusCode.InternalServerError,
				};
			}
		}

		public void Run(IEngine engine)
		{
			try
			{
				// This method is required for the script to compile, but it will never be called.
				ApiTriggerInput requestData = new ApiTriggerInput
				{
					Route = String.Empty,
					RouteParameters = null,
				};

				var apiResult = OnApiTrigger(engine, requestData);

				engine.AddScriptOutput("status", apiResult.ResponseCode.ToString());
				engine.AddScriptOutput("result", JsonConvert.SerializeObject(apiResult));
			}
			catch (System.Exception)
			{
				engine.AddScriptOutput("status", "500");
				engine.ExitFail("ERROR");
			}
		}
	}
}