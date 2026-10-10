namespace SLCSMUDAPIServiceOrder
{
	using System;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Net.Apps.UserDefinableApis;
	using Skyline.DataMiner.Net.Apps.UserDefinableApis.Actions;
	using Skyline.DataMiner.Utils.UserDefinedApiToolkit;
	using SLC_SM_Common.UDAPI;
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
			string rawRequest = engine.GetScriptParam("Request")?.Value;
			ApiHelper.ParseApiRequest(OnApiTrigger, engine, rawRequest);
		}
	}
}