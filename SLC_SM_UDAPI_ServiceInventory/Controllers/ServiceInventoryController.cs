namespace SLC_SM_UDAPI_ServiceInventory.Controllers
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text.RegularExpressions;

	using Microsoft.Extensions.Logging;

	using Newtonsoft.Json;
	using Newtonsoft.Json.Linq;

	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ApiHelpers;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;
	using Skyline.DataMiner.SDM.OData;
	using Skyline.DataMiner.Utils.UserDefinedApiToolkit;

	using SLC_SM_UDAPI_ServiceInventory.Exceptions;
	using SLC_SM_UDAPI_ServiceInventory.Helpers;
	using SLC_SM_UDAPI_ServiceInventory.Models;

	[ApiController]
	[Route("service-management/v1/service")]
	public class ServiceInventoryController : ControllerBase
	{
		private static readonly Regex ServiceIdPattern = new Regex(@"^SERVICE-\d+$", RegexOptions.Compiled);

		private readonly ILogger<ServiceInventoryController> _logger;
		private readonly IServiceManagementApiHelper serviceManagementApiHelper;
		private readonly ODataSdmTranslator<Service> _translator;

		public ServiceInventoryController(ILogger<ServiceInventoryController> logger, IServiceManagementApiHelper serviceManagementApiHelper)
		{
			_logger = logger ?? throw new ArgumentNullException(nameof(logger));
			this.serviceManagementApiHelper = serviceManagementApiHelper ?? throw new ArgumentNullException(nameof(serviceManagementApiHelper));
			_translator = new ODataSdmTranslator<Service>();
		}

		[HttpGet]
		public IApiResult ListServices(
			[FromQuery] string categoryId = null,
			[FromQuery] long? endDate = null,
			[FromQuery] string fields = null,
			[FromQuery] int limit = 100,
			[FromQuery] string name = null,
			[FromQuery] int offset = 0,
			[FromQuery] string organizationId = null,
			[FromQuery] string parameter = null,
			[FromQuery] string profileDefinition = null,
			[FromQuery] string serviceSpecificationId = null,
			[FromQuery] string sort = null,
			[FromQuery] long? startDate = null,
			[FromQuery] string status = null)
		{
			_logger.LogInformation("ListServices called with parameters:\ncategoryId={CategoryId}\nendDate={EndDate}\nfields={Fields}\nlimit={Limit}\nname={Name}\noffset={Offset}\norganizationId={OrganizationId}\nparameter={Parameter}\nprofileDefinition={ProfileDefinition}\nserviceSpecificationId={ServiceSpecificationId}\nsort={Sort}\nstartDate={StartDate}\nstatus={Status}",
				categoryId, endDate, fields, limit, name, offset, organizationId, JsonConvert.SerializeObject(parameter), profileDefinition, serviceSpecificationId, sort, startDate, JsonConvert.SerializeObject(status));

			var query = new ListServicesQuery
			{
				CategoryId = categoryId,
				EndDate = endDate,
				Limit = limit,
				Name = name,
				Offset = offset,
				OrganizationId = organizationId,
				Parameters = ParseParameters(SplitCommaSeparatedValues(parameter)),
				ProfileDefinitions = SplitCommaSeparatedValues(profileDefinition),
				ServiceSpecificationId = serviceSpecificationId,
				Sort = sort,
				StartDate = startDate,
				Statuses = SplitCommaSeparatedValues(status),
			};

			var search = new ServiceParameterSearch(new TimedServiceSearchDataSource(new ServiceSearchDataSource(serviceManagementApiHelper), _logger));
			var services = search.Find(query);

			if (SplitCommaSeparatedValues(fields).Count == 0)
			{
				return Ok(services);
			}

			return Ok(services.Select(s => SelectFields(s, fields)).ToList());
		}

		[HttpGet("{id}")]
		public IApiResult GetService(
			[FromRoute] string id,
			[FromQuery] string fields = null,
			[FromQuery] bool full = false)
		{
			_logger.LogInformation("GetService called with id={Id}, fields={Fields}, full={Full}", id, fields, full);

			if (String.IsNullOrWhiteSpace(id) || !ServiceIdPattern.IsMatch(id))
			{
				return NotFound();
			}

			var service = serviceManagementApiHelper.ServiceInventory.Services
				.Read(ServiceExposers.ServiceID.Equal(id))
				.FirstOrDefault();

			if (service == null)
			{
				return NotFound();
			}

			object result = full
				? (object)new FullServiceBuilder(serviceManagementApiHelper).Build(new[] { service }).First()
				: service;

			return Ok(SelectFields(result, fields));
		}

		private static object SelectFields(object value, string fields)
		{
			var requested = SplitCommaSeparatedValues(fields);
			if (requested.Count == 0)
			{
				return value;
			}

			var source = JObject.FromObject(value);
			var filtered = new JObject();
			foreach (var property in source.Properties())
			{
				if (requested.Any(f => String.Equals(f, property.Name, StringComparison.OrdinalIgnoreCase)))
				{
					filtered.Add(property.Name, property.Value);
				}
			}

			return filtered;
		}

		private static List<ServiceParameterQuery> ParseParameters(IEnumerable<string> parameters)
		{
			var result = new List<ServiceParameterQuery>();
			foreach (var parameter in parameters ?? Enumerable.Empty<string>())
			{
				int separatorIndex = parameter?.IndexOf(':') ?? -1;
				if (separatorIndex <= 0 || separatorIndex == parameter.Length - 1)
				{
					throw new InvalidQueryException("Each 'parameter' entry must use the format 'parameter:value'.");
				}

				result.Add(new ServiceParameterQuery
				{
					Parameter = parameter.Substring(0, separatorIndex).Trim(),
					Value = parameter.Substring(separatorIndex + 1).Trim(),
				});
			}

			return result;
		}

		private static List<string> SplitCommaSeparatedValues(string values)
		{
			return String.IsNullOrWhiteSpace(values)
				? new List<string>()
				: values.Split(',').Select(value => value.Trim()).Where(value => value.Length > 0).ToList();
		}
	}
}