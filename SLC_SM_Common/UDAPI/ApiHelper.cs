namespace SLC_SM_Common.UDAPI
{
	using System;
	using System.Collections.Generic;
	using Newtonsoft.Json;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Net.Apps.UserDefinableApis.Actions;
	using Skyline.DataMiner.Utils.SecureCoding.SecureSerialization.Json.Newtonsoft;
	using SLC_SM_Common.UDAPI.Models;

	public static class ApiHelper
	{
		public static ApiTriggerInput ToApiTriggerInput(this ApiRequest request)
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

		public static void ParseApiRequest(Func<IEngine, ApiTriggerInput, ApiTriggerOutput> apiCaller, IEngine engine, string rawRequest)
		{
			try
			{
				if (String.IsNullOrWhiteSpace(rawRequest))
				{
					engine.ExitFail("The 'Request' script parameter is empty.");
				}

				var request = SecureNewtonsoftDeserialization.DeserializeObject<ApiRequest>(rawRequest);

				if (request == null || String.IsNullOrWhiteSpace(request.Route))
				{
					engine.ExitFail("The 'Request' script parameter must contain a route.");
				}

				var result = apiCaller(engine, request.ToApiTriggerInput());
				engine.AddScriptOutput("StatusCode", result?.ResponseCode.ToString());
				engine.AddScriptOutput("Response", result?.ResponseBody);
			}
			catch (JsonException e)
			{
				engine.ExitFail($"The 'Request' script parameter is not a valid ApiRequest JSON: {e.Message}");
			}
			catch (Exception e)
			{
				engine.AddScriptOutput("StatusCode", "500");
				engine.Log($"Unexpected error: {e.Message}");
				engine.ExitFail($"Unexpected error.");
			}
		}
	}
}