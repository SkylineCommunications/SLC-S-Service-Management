namespace SLC_SM_UDAPI_ServiceManagement.Tests.Controllers
{
	using System;
	using System.Collections.Generic;
	using System.Reflection;
	using System.Runtime.CompilerServices;
	using Moq;
	using Newtonsoft.Json.Linq;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ApiHelpers;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.Configurations;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;
	using Skyline.DataMiner.SDM;
	using Skyline.DataMiner.Utils.UserDefinedApiToolkit;

	using ItemStatus = DomHelpers.SlcServicemanagement.SlcServicemanagementIds.Behaviors.Serviceorderitem_Behavior.StatusesEnum;

	/// <summary>
	/// Shared fixture: in-memory Service Management API so controllers run without DataMiner.
	/// </summary>
	public abstract class ServiceCatalogTestBase : ControllerTestBase
	{
		protected List<ServiceSpecification> Specifications { get; private set; }

		protected List<ServiceCategory> Categories { get; private set; }

		protected List<ServiceSpecificationConfigurationValue> ConfigValues { get; private set; }

		protected List<ServiceSpecificationProfile> SpecProfiles { get; private set; }

		protected List<ConfigurationParameter> ConfigurationParameters { get; private set; }

		protected List<Profile> Profiles { get; private set; }

		protected List<ProfileDefinition> ProfileDefinitions { get; private set; }

		protected List<Service> Services { get; private set; }

		protected List<ServiceOrderItem> OrderItems { get; private set; }

		/// <summary>Number of times the service order item repository was read.</summary>
		protected int OrderItemReads { get; set; }

		protected Mock<IServiceManagementApiHelper> Helper { get; private set; }

		protected Mock<IServiceCatalogApi> Catalog { get; private set; }

		[TestInitialize]
		public void BaseInitialize()
		{
			RuntimeHelpers.RunClassConstructor(typeof(ServiceSpecificationExposers).TypeHandle);
			RuntimeHelpers.RunClassConstructor(typeof(ServiceCategoryExposers).TypeHandle);

			Specifications = new List<ServiceSpecification>();
			Categories = new List<ServiceCategory>();
			ConfigValues = new List<ServiceSpecificationConfigurationValue>();
			SpecProfiles = new List<ServiceSpecificationProfile>();
			ConfigurationParameters = new List<ConfigurationParameter>();
			Profiles = new List<Profile>();
			ProfileDefinitions = new List<ProfileDefinition>();
			Services = new List<Service>();
			OrderItems = new List<ServiceOrderItem>();

			Catalog = new Mock<IServiceCatalogApi>();
			Catalog.SetupGet(c => c.ServiceSpecifications).Returns(Repository(Specifications).Object);
			Catalog.SetupGet(c => c.ServiceCategories).Returns(Repository(Categories).Object);
			Catalog.SetupGet(c => c.ServiceSpecificationConfigurationValues).Returns(Repository(ConfigValues).Object);
			Catalog.SetupGet(c => c.ServiceSpecificationProfiles).Returns(Repository(SpecProfiles).Object);
			Catalog.SetupGet(c => c.ConfigurationParameters).Returns(Repository(ConfigurationParameters).Object);
			Catalog.SetupGet(c => c.Profiles).Returns(Repository(Profiles).Object);
			Catalog.SetupGet(c => c.ProfileDefinitions).Returns(Repository(ProfileDefinitions).Object);

			var services = new Mock<IServiceRepository>();
			services.Setup(r => r.Read(It.IsAny<FilterElement<Service>>()))
				.Returns<FilterElement<Service>>(f => Services.Where(f.getLambda()).ToList());
			var inventory = new Mock<IServiceInventoryApi>();
			inventory.SetupGet(i => i.Services).Returns(services.Object);

			var items = new Mock<IServiceOrderItemRepository>();
			items.Setup(r => r.Read(It.IsAny<FilterElement<ServiceOrderItem>>()))
				.Callback(() => OrderItemReads++)
				.Returns(() => OrderItems.ToList());
			var order = new Mock<IServiceOrderApi>();
			order.SetupGet(o => o.ServiceOrderItems).Returns(items.Object);

			Helper = new Mock<IServiceManagementApiHelper>();
			Helper.SetupGet(h => h.ServiceCatalog).Returns(Catalog.Object);
			Helper.SetupGet(h => h.ServiceInventory).Returns(inventory.Object);
			Helper.SetupGet(h => h.ServiceOrder).Returns(order.Object);
		}

		/// <summary>In-memory repository: Read applies the filter, Create/Update/Delete mutate the list.</summary>
		protected static Mock<IBulkRepository<T>> Repository<T>(List<T> store)
			where T : SdmObject<T>
		{
			var mock = new Mock<IBulkRepository<T>>();
			mock.Setup(r => r.Read(It.IsAny<FilterElement<T>>()))
				.Returns<FilterElement<T>>(f => store.Where(f.getLambda()).ToList());
			mock.Setup(r => r.Create(It.IsAny<T>())).Callback<T>(store.Add).Returns<T>(o => o);
			mock.Setup(r => r.Update(It.IsAny<T>())).Returns<T>(o => o);
			mock.Setup(r => r.Delete(It.IsAny<T>())).Callback<T>(o => store.RemoveAll(x => x.Identifier == o.Identifier));
			return mock;
		}

		protected static T Value<T>(IApiResult result)
		{
			Assert.IsInstanceOfType(result, typeof(ObjectResult<T>));
			return ((ObjectResult<T>)result).Value;
		}

		protected static void SetItemStatus(ServiceOrderItem item, string status)
		{
			SetStatus(item, (ItemStatus)Enum.Parse(typeof(ItemStatus), status, true));
		}

		protected T AddWithId<T>(List<T> store, T value)
			where T : SdmObject<T>
		{
			value.Identifier = Guid.NewGuid().ToString();
			store.Add(value);
			return value;
		}
	}
}
