namespace SLCSMUDAPIServiceSpecification
{
	using System;
	using System.Collections.Generic;

	using Newtonsoft.Json;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Net.Apps.UserDefinableApis;
	using Skyline.DataMiner.Net.Apps.UserDefinableApis.Actions;
	using Skyline.DataMiner.Utils.SecureCoding.SecureSerialization.Json.Newtonsoft;
	using Skyline.DataMiner.Utils.UserDefinedApiToolkit;

	using SLC_SM_Common.UDAPI.Models;

	using SLC_SM_UDAPI_ServiceSpecification.Helpers;

	/// <summary>
	/// Represents the Service Specification user-defined API.
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
		public static ApiTriggerOutput OnApiTrigger(IEngine engine, ApiTriggerInput requestData)
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

		/// <summary>
		/// Debug entry point: runs a request passed as JSON in script parameter 2.
		/// </summary>
		/// <param name="engine">Link with SLAutomation process.</param>
		public static void Run(IEngine engine)
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
			catch (Exception e)
			{
				engine.Log($"Run|Unexpected failure: {e}");
				engine.ExitFail("Something went wrong while processing the request.");
			}
		}

		private static void RunSafe(IEngine engine)
		{
			string rawRequest = engine.GetScriptParam(2)?.Value;
			if (String.IsNullOrWhiteSpace(rawRequest))
			{
				engine.ExitFail("The 'Request' script parameter is empty.");
				return;
			}

			ApiRequest request;
			try
			{
				request = SecureNewtonsoftDeserialization.DeserializeObject<ApiRequest>(rawRequest);
			}
			catch (JsonException e)
			{
				engine.ExitFail($"The 'Request' script parameter is not a valid ApiRequest JSON: {e.Message}");
				return;
			}

			if (request == null || String.IsNullOrWhiteSpace(request.Route))
			{
				engine.ExitFail("The 'Request' script parameter must contain a route.");
				return;
			}

			var result = OnApiTrigger(engine, new ApiTriggerInput
			{
				Route = request.Route,
				RequestMethod = request.RequestMethod,
				RawBody = request.Body ?? String.Empty,
				Parameters = new Dictionary<string, string>(),
				QueryParameters = new QueryParameters(request.QueryParameters ?? new Dictionary<string, List<string>>()),
				RouteParameters = request.RouteParameters ?? new Dictionary<string, string>(),
			});

			engine.AddScriptOutput("StatusCode", result?.ResponseCode.ToString());
			engine.AddScriptOutput("Response", result?.ResponseBody);
		}
	}
}
