namespace SLC_SM_UDAPI_ServiceInventory.Tests.Controllers
{
	using System.Collections.Generic;
	using System.Reflection;
	using Microsoft.Extensions.Logging;
	using Moq;
	using Newtonsoft.Json.Linq;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ApiHelpers;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;
	using Skyline.DataMiner.SDM;
	using Skyline.DataMiner.Utils.UserDefinedApiToolkit;
	using SLC_SM_UDAPI_ServiceInventory.Controllers;

	using Cfg = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.Configurations;
	using ServiceStatus = DomHelpers.SlcServicemanagement.SlcServicemanagementIds.Behaviors.Service_Behavior.StatusesEnum;
	using ServiceTransition = DomHelpers.SlcServicemanagement.SlcServicemanagementIds.Behaviors.Service_Behavior.TransitionsEnum;

	/// <summary>
	/// Shared fixture: in-memory Service Management repositories so the controller runs without DataMiner.
	/// Repository reads evaluate the controller's filter against the stored objects.
	/// </summary>
	public abstract class ServiceInventoryControllerTestBase
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

			Services = new Mock<IServiceRepository>();
			Services.Setup(r => r.Read(It.IsAny<FilterElement<Service>>())).Returns<FilterElement<Service>>(f => Filter(StoredServices, f));
			Services.Setup(r => r.Create(It.IsAny<Service>())).Returns<Service>(s => { StoredServices.Add(s); return s; });
			Services.Setup(r => r.Update(It.IsAny<Service>())).Returns<Service>(s => s);
			Services.Setup(r => r.Delete(It.IsAny<Service>())).Callback<Service>(s => StoredServices.Remove(s));
			Services.Setup(r => r.TransitionStatus(It.IsAny<Service>(), It.IsAny<ServiceTransition>())).Returns<Service, ServiceTransition>((s, t) => s);

			var inventory = new Mock<IServiceInventoryApi>();
			inventory.SetupGet(a => a.Services).Returns(Services.Object);
			inventory.SetupGet(a => a.ServiceConfigurationVersions).Returns(Repository(StoredVersions));
			inventory.SetupGet(a => a.ServiceConfigurationValues).Returns(Repository(StoredConfigurationValues));
			inventory.SetupGet(a => a.ServiceProfiles).Returns(Repository(StoredServiceProfiles));

			var catalog = new Mock<IServiceCatalogApi>();
			catalog.SetupGet(a => a.ServiceCategories).Returns(Repository(StoredCategories));
			catalog.SetupGet(a => a.ServiceSpecifications).Returns(Repository(StoredSpecifications));
			catalog.SetupGet(a => a.ConfigurationParameters).Returns(Repository(StoredParameters));
			catalog.SetupGet(a => a.ConfigurationParameterValues).Returns(Repository(StoredParameterValues));
			catalog.SetupGet(a => a.Profiles).Returns(Repository(StoredProfiles));
			catalog.SetupGet(a => a.ProfileDefinitions).Returns(Repository(StoredProfileDefinitions));

			var helper = new Mock<IServiceManagementApiHelper>();
			helper.SetupGet(h => h.ServiceInventory).Returns(inventory.Object);
			helper.SetupGet(h => h.ServiceCatalog).Returns(catalog.Object);

			// No live engine in unit tests; endpoints that need it (TransitionService side effects) are covered by integration tests.
			Controller = new ServiceInventoryController(
				Mock.Of<ILogger<ServiceInventoryController>>(),
				helper.Object,
				Mock.Of<IAccessor<Skyline.DataMiner.Automation.IEngine>>());

			// The UDAPI pipeline normally supplies the context; its setters are internal to the toolkit.
			var context = new ApiContext();
			SetProperty(context, nameof(ApiContext.InputConverters), new List<IInputConverter> { new NewtonsoftConverter() });
			SetProperty(context, nameof(ApiContext.OutputConverters), new List<IOutputConverter> { new NewtonsoftConverter() });
			SetProperty(Controller, nameof(ServiceInventoryController.ApiContext), context);
		}

		protected static int StatusCode(IApiResult result)
		{
			Assert.IsInstanceOfType(result, typeof(StatusCodeResult));
			return ((StatusCodeResult)result).StatusCode;
		}

		protected static T Value<T>(IApiResult result)
		{
			var value = result.GetType().GetProperty("Value").GetValue(result);
			Assert.IsInstanceOfType(value, typeof(T));
			return (T)value;
		}

		protected static string ErrorDetail(IApiResult result)
		{
			var value = result.GetType().GetProperty("Value").GetValue(result);
			return (string)JObject.FromObject(value)["error"][0]["detail"];
		}

		protected static void SetStatus(Service service, ServiceStatus status)
		{
			var field = typeof(Service).GetField("<Status>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
			Assert.IsNotNull(field, "Status backing field not found; package layout changed.");
			field.SetValue(service, status);
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
			return repository.Object;
		}

		private static IEnumerable<T> Filter<T>(List<T> store, FilterElement<T> filter)
		{
			var predicate = filter.getLambda();
			return store.Where(predicate).ToList();
		}

		private static void SetProperty(object target, string name, object value)
		{
			var property = target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			Assert.IsNotNull(property, $"Property '{name}' not found; toolkit layout changed.");
			property.SetValue(target, value);
		}
	}
}
