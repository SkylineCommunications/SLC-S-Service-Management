namespace SLC_SM_GQIDS_Get_Services
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using DomHelpers.SlcPeople_Organizations;
	using DomHelpers.SlcServicemanagement;
	using Skyline.DataMiner.Analytics.GenericInterface;
	using Skyline.DataMiner.Net.Apps.DataMinerObjectModel;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.ServiceManagement;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ApiHelpers;
	using SLC_SM_Common.Extensions;
	using LegacyOrganizationModel = Skyline.DataMiner.ProjectApi.ServiceManagement.API.PeopleAndOrganization.Models.Organization;
	using LegacyServiceExposers = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceExposers;
	using LegacyServiceModel = Skyline.DataMiner.ProjectApi.ServiceManagement.API.ServiceManagement.Models.Service;
	using Models = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;
	using OrganizationExposers = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.OrganizationExposers;

	[GQIMetaData(Name = DataSourceName)]
	public sealed class GetServices : IGQIDataSource, IGQIInputArguments, IGQIOnInit
	{
		private const string DataSourceName = "Get_Services";
		private readonly GQIStringArgument domIdArg = new GQIStringArgument("DOM ID") { IsRequired = false };

		private GQIDMS _gqiDms;
		private IGQILogger _logger;
		private IServiceManagementApiHelper _serviceManagementApiHelper;
		private string _domId;

		public GQIColumn[] GetColumns()
		{
			return new GQIColumn[]
			{
				new GQIStringColumn("DOM ID"),
				new GQIStringColumn("Service ID"),
				new GQIStringColumn("Service Name"),
				new GQIStringColumn("Service Status"),
				new GQIDateTimeColumn("Service Start"),
				new GQIDateTimeColumn("Service End"),
				new GQIStringColumn("Service Category"),
				new GQIStringColumn("Service Category Type"),
				new GQIStringColumn("Service Logo"),
				new GQIStringColumn("Service Specification"),
				new GQIIntColumn("Alarm Level"),
				new GQIStringColumn("Service Organization"),
				new GQIStringColumn("Monitoring Service"),
				new GQIStringColumn("Related Service Orders"),
				new GQIIntColumn("Related Service Order Count"),
			};
		}

		public GQIArgument[] GetInputArguments() => new[] { domIdArg };

		public OnInitOutputArgs OnInit(OnInitInputArgs args)
		{
			_gqiDms = args.DMS;
			_logger = args.Logger;
			_logger.MinimumLogLevel = GQILogLevel.Debug;
			_serviceManagementApiHelper = new ServiceManagementApiHelper(_gqiDms.GetConnection(), "Service Inventory");
			return default;
		}

		public OnArgumentsProcessedOutputArgs OnArgumentsProcessed(OnArgumentsProcessedInputArgs args)
		{
			_domId = args.GetArgumentValue(domIdArg);
			return new OnArgumentsProcessedOutputArgs();
		}

		public GQIPage GetNextPage(GetNextPageInputArgs args)
		{
			return _logger.PerformanceLogger(nameof(GetNextPage), BuildPage);
		}

		private GQIPage BuildPage()
		{
			try
			{
				var services = ReadServices();
				_logger.Debug($"GQIDS|{DataSourceName}|Retrieved {services.Count} service inventory records.");
				var categories = ReadCategories(services);
				var specifications = ReadSpecifications(services);
				var serviceOrganizations = ReadServiceOrganizations(services);
				var relatedOrders = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

				return new GQIPage(services.Select(service => BuildRow(service, categories, specifications, serviceOrganizations, relatedOrders)).ToArray()) { HasNextPage = false };
			}
			catch (Exception exception)
			{
				_gqiDms.GenerateInformationMessage($"GQIDS|{DataSourceName}|Exception: {exception}");
				_logger.Error($"GQIDS|{DataSourceName}|Exception: {exception}");
				throw;
			}
		}

		private List<Models.Service> ReadServices()
		{
			if (String.IsNullOrWhiteSpace(_domId))
			{
				return _serviceManagementApiHelper.ServiceInventory.Services
					.Read(new TRUEFilterElement<Models.Service>())
					.ToList();
			}

			if (!Guid.TryParse(_domId, out var serviceId))
			{
				return new List<Models.Service>();
			}

			return _serviceManagementApiHelper.ServiceInventory.Services
				.Read(Models.ServiceExposers.Identifier.Equal(serviceId.ToString()))
				.Take(1)
				.ToList();
		}

		private Dictionary<string, Models.ServiceCategory> ReadCategories(IEnumerable<Models.Service> services)
		{
			try
			{
				FilterElement<Models.ServiceCategory> filter = null;
				foreach (var categoryId in services
					.Select(service => service.CategoryId != null ? service.CategoryId.Identifier : null)
					.Where(identifier => !String.IsNullOrWhiteSpace(identifier))
					.Distinct(StringComparer.OrdinalIgnoreCase))
				{
					var categoryFilter = Models.ServiceCategoryExposers.Identifier.Equal(categoryId);
					filter = filter == null ? categoryFilter : filter.OR(categoryFilter);
				}

				if (filter == null)
				{
					return new Dictionary<string, Models.ServiceCategory>(StringComparer.OrdinalIgnoreCase);
				}

				return _serviceManagementApiHelper.ServiceCatalog.ServiceCategories
					.Read(filter)
					.Where(category => !String.IsNullOrWhiteSpace(category.Identifier))
					.ToDictionary(category => category.Identifier, StringComparer.OrdinalIgnoreCase);
			}
			catch (Exception exception)
			{
				_logger.Error($"GQIDS|{DataSourceName}|Could not read service categories: {exception}");
				return new Dictionary<string, Models.ServiceCategory>(StringComparer.OrdinalIgnoreCase);
			}
		}

		private Dictionary<string, Models.ServiceSpecification> ReadSpecifications(IEnumerable<Models.Service> services)
		{
			try
			{
				FilterElement<Models.ServiceSpecification> filter = null;
				foreach (var specificationId in services
					.Select(service => service.ServiceSpecificationId != null ? service.ServiceSpecificationId.Identifier : null)
					.Where(identifier => !String.IsNullOrWhiteSpace(identifier))
					.Distinct(StringComparer.OrdinalIgnoreCase))
				{
					var specificationFilter = Models.ServiceSpecificationExposers.Identifier.Equal(specificationId);
					filter = filter == null ? specificationFilter : filter.OR(specificationFilter);
				}

				if (filter == null)
				{
					return new Dictionary<string, Models.ServiceSpecification>(StringComparer.OrdinalIgnoreCase);
				}

				return _serviceManagementApiHelper.ServiceCatalog.ServiceSpecifications
					.Read(filter)
					.Where(specification => !String.IsNullOrWhiteSpace(specification.Identifier))
					.ToDictionary(specification => specification.Identifier, StringComparer.OrdinalIgnoreCase);
			}
			catch (Exception exception)
			{
				_logger.Error($"GQIDS|{DataSourceName}|Could not read service specifications: {exception}");
				return new Dictionary<string, Models.ServiceSpecification>(StringComparer.OrdinalIgnoreCase);
			}
		}

		private GQIRow BuildRow(
			Models.Service service,
			IReadOnlyDictionary<string, Models.ServiceCategory> categories,
			IReadOnlyDictionary<string, Models.ServiceSpecification> specifications,
			IReadOnlyDictionary<string, string> serviceOrganizations,
			IReadOnlyDictionary<string, List<string>> relatedOrders)
		{
			var id = service.Identifier ?? String.Empty;
			var metadata = Guid.TryParse(id, out var domId)
				? new GenIfRowMetadata(new[] { new ObjectRefMetadata { Object = new DomInstanceId(domId) { ModuleId = SlcServicemanagementIds.ModuleId } } })
				: null;

			var alarmLevel = 0;
			var category = service.CategoryId != null && !String.IsNullOrWhiteSpace(service.CategoryId.Identifier) && categories.TryGetValue(service.CategoryId.Identifier, out var categoryValue) ? categoryValue : null;
			var specification = service.ServiceSpecificationId != null && !String.IsNullOrWhiteSpace(service.ServiceSpecificationId.Identifier) && specifications.TryGetValue(service.ServiceSpecificationId.Identifier, out var specificationValue) ? specificationValue : null;
			var related = relatedOrders.TryGetValue(id, out var serviceOrders) ? serviceOrders : new List<string>();

			return new GQIRow(
				id,
				new[]
				{
					new GQICell { Value = id },
					new GQICell { Value = service.ServiceID ?? String.Empty },
					new GQICell { Value = service.Name ?? String.Empty },
					new GQICell { Value = service.Status.ToString() },
					new GQICell { Value = service.StartTime?.ToUniversalTime() },
					new GQICell { Value = service.EndTime?.ToUniversalTime() },
					new GQICell { Value = category?.Name ?? String.Empty },
					new GQICell { Value = category?.Type ?? String.Empty },
					new GQICell { Value = service.Icon ?? String.Empty },
					new GQICell { Value = specification?.Name ?? String.Empty },
					new GQICell { Value = (int)alarmLevel },
					new GQICell { Value = serviceOrganizations.TryGetValue(id, out var organization) ? organization : String.Empty },
					new GQICell { Value = service.MonitoringService ?? String.Empty },
					new GQICell { Value = String.Join(", ", related) },
					new GQICell { Value = related.Count },
				})
			{
				Metadata = metadata,
			};
		}

		private Dictionary<string, string> ReadServiceOrganizations(IEnumerable<Models.Service> services)
		{
			var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			var serviceIds = services
				.Select(service => service.Identifier)
				.Select(identifier => Guid.TryParse(identifier, out var parsedId) ? parsedId : Guid.Empty)
				.Where(identifier => identifier != Guid.Empty)
				.Distinct()
				.ToList();
			if (serviceIds.Count == 0 || !_gqiDms.DomModelExists(SlcPeople_OrganizationsIds.ModuleId))
			{
				return result;
			}

			FilterElement<LegacyServiceModel> serviceFilter = LegacyServiceExposers.Guid.Equal(serviceIds[0]);
			foreach (var serviceId in serviceIds.Skip(1))
			{
				serviceFilter = serviceFilter.OR(LegacyServiceExposers.Guid.Equal(serviceId));
			}

			var serviceOrganizationIds = new DataHelperService(_gqiDms.GetConnection())
				.Read(serviceFilter)
				.Where(service => service.OrganizationId.HasValue)
				.ToDictionary(service => service.ID, service => service.OrganizationId.Value);
			var organizationIds = serviceOrganizationIds.Values.Distinct().ToList();
			if (organizationIds.Count == 0)
			{
				return result;
			}

			FilterElement<LegacyOrganizationModel> organizationFilter = OrganizationExposers.Guid.Equal(organizationIds[0]);
			foreach (var organizationId in organizationIds.Skip(1))
			{
				organizationFilter = organizationFilter.OR(OrganizationExposers.Guid.Equal(organizationId));
			}

			var organizationNames = new DataHelpersPeopleAndOrganizations(_gqiDms.GetConnection())
				.Organizations
				.Read(organizationFilter)
				.Where(organization => !String.IsNullOrWhiteSpace(organization.Name))
				.ToDictionary(organization => organization.ID, organization => organization.Name);

			foreach (var serviceOrganization in serviceOrganizationIds)
			{
				if (organizationNames.TryGetValue(serviceOrganization.Value, out var organizationName))
				{
					result[serviceOrganization.Key.ToString()] = organizationName;
				}
			}

			return result;
		}

	}
}
