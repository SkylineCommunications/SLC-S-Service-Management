namespace SLC_SM_UDAPI_ServiceManagement.Models
{
	using System.Collections.Generic;

	using Skyline.DataMiner.Net.Apps.UserDefinableApis;

	public class ApiRequest
	{
		public ApiRequest(string route, RequestMethod requestMethod, string body, Dictionary<string, List<string>> queryParameters, Dictionary<string, string> routeParameters)
		{
			Route = route;
			RequestMethod = requestMethod;
			Body = body;
			QueryParameters = queryParameters;
			RouteParameters = routeParameters;
		}

		public string Body { get; }

		public Dictionary<string, List<string>> QueryParameters { get; }

		public RequestMethod RequestMethod { get; }

		public string Route { get; }

		public Dictionary<string, string> RouteParameters { get; }
	}
}