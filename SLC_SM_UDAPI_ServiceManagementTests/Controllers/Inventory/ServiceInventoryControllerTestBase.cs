namespace SLC_SM_UDAPI_ServiceManagement.Tests.Controllers
{
	using System.Collections.Generic;
	using Microsoft.Extensions.Logging;
	using Moq;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ApiHelpers;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;
	using Skyline.DataMiner.SDM;
	using Skyline.DataMiner.Utils.UserDefinedApiToolkit;
	using SLDataGateway.API.Types.Querying;
	using SLC_SM_UDAPI_ServiceManagement.Controllers;
	using SLC_SM_UDAPI_ServiceManagement.Helpers.Inventory;

	using Cfg = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.Configurations;
	using ServiceStatus = DomHelpers.SlcServicemanagement.SlcServicemanagementIds.Behaviors.Service_Behavior.StatusesEnum;
	using ServiceTransition = DomHelpers.SlcServicemanagement.SlcServicemanagementIds.Behaviors.Service_Behavior.TransitionsEnum;

	/// <summary>
	/// Shared fixture: in-memory Service Management repositories so the controller runs without DataMiner.
	/// Repository reads evaluate the controller's filter against the stored objects.
	/// </summary>
	public abstract class ServiceInventoryControllerTestBase : ControllerTestBase
	{
		protected Mock<IServiceRepository> Services { get; private set; }

		protected List<Service> StoredServices { get; private set; }

		protected List<ServiceCategory> StoredCategories { get; private set; }

		protected List<ServiceSpecification> StoredSpecifications { get; private set; }

		protected List<ServiceConfigurationVersion> StoredVersions { get; private set; }

		protected List<ServiceConfigurationValue> StoredConfigurationValues { get; private set; }

		protected List<ServiceProfile> StoredServiceProfiles { get; private set; }

		protected List<Cfg.ConfigurationParameter> StoredParameters { get; private set; }

		protected List<Cfg.ConfigurationParameterValue> StoredParameterValues { get; private set; }

		protected List<Cfg.Profile> StoredProfiles { get; private set; }

		protected List<Cfg.ProfileDefinition> StoredProfileDefinitions { get; private set; }

		protected List<Cfg.DiscreteParameterOptions> StoredDiscreteOptions { get; private set; }

		protected List<Cfg.DiscreteValue> StoredDiscreteValues { get; private set; }

		protected List<Cfg.NumberParameterOptions> StoredNumberOptions { get; private set; }

		protected List<Cfg.ConfigurationUnit> StoredConfigurationUnits { get; private set; }

		protected List<ServiceOrderItem> StoredOrderItems { get; private set; }

		protected Mock<IServiceCatalogApi> Catalog { get; private set; }

		protected Mock<IMonitoringServiceManager> MonitoringServices { get; private set; }

		protected Mock<IServiceOrganizationRepository> Organizations { get; private set; }

		protected Mock<IServiceItemImplementations> ItemImplementations { get; private set; }

		protected Dictionary<Guid, string> OrganizationNames { get; private set; }

		protected Dictionary<Guid, Guid> ServiceOrganizations { get; private set; }

		protected ServiceInventoryController Controller { get; private set; }

		[TestInitialize]
		public void BaseInitialize()
		{
			StoredServices = new List<Service>();
			StoredCategories = new List<ServiceCategory>();
			StoredSpecifications = new List<ServiceSpecification>();
			StoredVersions = new List<ServiceConfigurationVersion>();
			StoredConfigurationValues = new List<ServiceConfigurationValue>();
			StoredServiceProfiles = new List<ServiceProfile>();
			StoredParameters = new List<Cfg.ConfigurationParameter>();
			StoredParameterValues = new List<Cfg.ConfigurationParameterValue>();
			StoredProfiles = new List<Cfg.Profile>();
			StoredProfileDefinitions = new List<Cfg.ProfileDefinition>();
			StoredDiscreteOptions = new List<Cfg.DiscreteParameterOptions>();
			StoredDiscreteValues = new List<Cfg.DiscreteValue>();
			StoredNumberOptions = new List<Cfg.NumberParameterOptions>();
			StoredConfigurationUnits = new List<Cfg.ConfigurationUnit>();
			StoredOrderItems = new List<ServiceOrderItem>();

			Services = new Mock<IServiceRepository>();
			Services.Setup(r => r.Read(It.IsAny<FilterElement<Service>>())).Returns<FilterElement<Service>>(f => Filter(StoredServices, f));
			Services.Setup(r => r.Read(It.IsAny<IQuery<Service>>())).Returns<IQuery<Service>>(q => Filter(StoredServices, q.Filter));
			Services.Setup(r => r.Create(It.IsAny<Service>())).Returns<Service>(s => { StoredServices.Add(s); return s; });
			Services.Setup(r => r.Update(It.IsAny<Service>())).Returns<Service>(s => s);
			Services.Setup(r => r.Delete(It.IsAny<Service>())).Callback<Service>(s => StoredServices.Remove(s));
			Services.Setup(r => r.TransitionStatus(It.IsAny<Service>(), It.IsAny<ServiceTransition>())).Returns<Service, ServiceTransition>((s, t) => s);

			var inventory = new Mock<IServiceInventoryApi>();
			inventory.SetupGet(a => a.Services).Returns(Services.Object);
			inventory.SetupGet(a => a.ServiceConfigurationVersions).Returns(Repository(StoredVersions));
			inventory.SetupGet(a => a.ServiceConfigurationValues).Returns(Repository(StoredConfigurationValues));
			inventory.SetupGet(a => a.ServiceProfiles).Returns(Repository(StoredServiceProfiles));

			var catalog = Catalog = new Mock<IServiceCatalogApi>();
			catalog.SetupGet(a => a.ServiceCategories).Returns(Repository(StoredCategories));
			catalog.SetupGet(a => a.ServiceSpecifications).Returns(Repository(StoredSpecifications));
			catalog.SetupGet(a => a.ConfigurationParameters).Returns(Repository(StoredParameters));
			catalog.SetupGet(a => a.ConfigurationParameterValues).Returns(Repository(StoredParameterValues));
			catalog.SetupGet(a => a.Profiles).Returns(Repository(StoredProfiles));
			catalog.SetupGet(a => a.ProfileDefinitions).Returns(Repository(StoredProfileDefinitions));
			catalog.SetupGet(a => a.DiscreteParameterOptions).Returns(Repository(StoredDiscreteOptions));
			catalog.SetupGet(a => a.DiscreteValues).Returns(Repository(StoredDiscreteValues));
			catalog.SetupGet(a => a.NumberParameterOptions).Returns(Repository(StoredNumberOptions));
			catalog.SetupGet(a => a.ConfigurationUnits).Returns(Repository(StoredConfigurationUnits));

			var orderItems = new Mock<IServiceOrderItemRepository>();
			orderItems.Setup(r => r.Read(It.IsAny<FilterElement<ServiceOrderItem>>())).Returns<FilterElement<ServiceOrderItem>>(f => Filter(StoredOrderItems, f));
			var order = new Mock<IServiceOrderApi>();
			order.SetupGet(a => a.ServiceOrderItems).Returns(orderItems.Object);

			var helper = new Mock<IServiceManagementApiHelper>();
			helper.SetupGet(h => h.ServiceOrder).Returns(order.Object);
			helper.SetupGet(h => h.ServiceInventory).Returns(inventory.Object);
			helper.SetupGet(h => h.ServiceCatalog).Returns(catalog.Object);

			MonitoringServices = new Mock<IMonitoringServiceManager>();

			OrganizationNames = new Dictionary<Guid, string>();
			ServiceOrganizations = new Dictionary<Guid, Guid>();
			Organizations = new Mock<IServiceOrganizationRepository>();
			Organizations.Setup(r => r.ReadOrganizationNames()).Returns(() => OrganizationNames);
			Organizations.Setup(r => r.ReadServiceOrganizationIds(It.IsAny<IEnumerable<Guid>>())).Returns(() => ServiceOrganizations);

			ItemImplementations = new Mock<IServiceItemImplementations>();

			// No live engine in unit tests; endpoints that need it (TransitionService side effects) are covered by integration tests.
			Controller = new ServiceInventoryController(
				Mock.Of<ILogger<ServiceInventoryController>>(),
				helper.Object,
				Mock.Of<IAccessor<Skyline.DataMiner.Automation.IEngine>>(a => a.Value == Mock.Of<Skyline.DataMiner.Automation.IEngine>()),
				MonitoringServices.Object,
				Organizations.Object,
				ItemImplementations.Object);

			AttachContext(Controller);
		}

		protected static T Value<T>(IApiResult result)
		{
			var value = result.GetType().GetProperty("Value").GetValue(result);
			Assert.IsInstanceOfType(value, typeof(T));
			return (T)value;
		}

		protected Service AddService(string name = "Service", ServiceStatus status = ServiceStatus.New, int number = 1)
		{
			var service = new Service
			{
				Identifier = Guid.NewGuid().ToString(),
				ServiceID = $"SERVICE-{number:00000}",
				Name = name,
				ServiceItems = new List<ServiceItem>(),
				ServiceItemsRelationships = new List<ServiceItemRelationship>(),
				ConfigurationVersions = new List<SdmObjectReference<ServiceConfigurationVersion>>(),
			};
			SetStatus(service, status);
			StoredServices.Add(service);
			return service;
		}

		protected ServiceConfigurationVersion AddVersion(Service service, string name, DateTime? startDate = null)
		{
			var version = new ServiceConfigurationVersion
			{
				Identifier = Guid.NewGuid().ToString(),
				VersionName = name,
				StartDate = startDate,
				Parameters = new List<SdmObjectReference<ServiceConfigurationValue>>(),
				Profiles = new List<SdmObjectReference<ServiceProfile>>(),
			};
			StoredVersions.Add(version);
			service.ConfigurationVersions.Add(new SdmObjectReference<ServiceConfigurationVersion>(version.Identifier));
			return version;
		}

		private static IBulkRepository<T> Repository<T>(List<T> store)
			where T : class
		{
			var repository = new Mock<IBulkRepository<T>>();
			repository.Setup(r => r.Read(It.IsAny<FilterElement<T>>())).Returns<FilterElement<T>>(f => Filter(store, f));
			repository.Setup(r => r.Create(It.IsAny<T>())).Returns<T>(item => { store.Add(item); return item; });
			repository.Setup(r => r.Update(It.IsAny<T>())).Returns<T>(item => item);
			repository.Setup(r => r.CreateOrUpdate(It.IsAny<IEnumerable<T>>()))
				.Callback<IEnumerable<T>>(items =>
				{
					foreach (var item in items ?? new T[0])
					{
						if (!store.Contains(item))
						{
							store.Add(item);
						}
					}
				});
			return repository.Object;
		}

		private static IEnumerable<T> Filter<T>(List<T> store, FilterElement<T> filter)
		{
			var predicate = filter.getLambda();
			return store.Where(predicate).ToList();
		}
	}
}
