namespace SLCSMUDAPIServiceInventory
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

	using SLC_SM_UDAPI_ServiceInventory.Helpers;

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
		public static ApiTriggerOutput OnApiTrigger(IEngine engine, ApiTriggerInput requestData)
		{
			engine.Log($"Request: {JsonConvert.SerializeObject(requestData, Formatting.Indented)}");

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
		/// The script entry point.
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
				// Catch normal abort exceptions (engine.ExitFail or engine.ExitSuccess)
				throw; // Comment if it should be treated as a normal exit of the script.
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
				engine.ExitFail("Run|Something went wrong: " + e);
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

			var result = OnApiTrigger(engine, ToApiTriggerInput(request));
			engine.AddScriptOutput("StatusCode", result?.ResponseCode.ToString());
			engine.AddScriptOutput("Response", result?.ResponseBody);
		}

		private static ApiTriggerInput ToApiTriggerInput(ApiRequest request)
		{
			return new ApiTriggerInput
			{
				Route = request.Route,
				RequestMethod = request.RequestMethod,
				RawBody = request.Body ?? String.Empty,
				Parameters = new Dictionary<string, string>(),
				QueryParameters = new QueryParameters(request.QueryParameters ?? new Dictionary<string, List<string>>()),
				RouteParameters = request.RouteParameters ?? new Dictionary<string, string>(),
			};
		}
	}
}