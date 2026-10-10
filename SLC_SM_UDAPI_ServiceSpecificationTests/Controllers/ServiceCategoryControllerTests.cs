namespace SLC_SM_UDAPI_ServiceSpecification.Tests.Controllers
{
	using System;
	using System.Collections.Generic;
	using Microsoft.Extensions.Logging;
	using Moq;
	using Newtonsoft.Json.Linq;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;
	using Skyline.DataMiner.SDM;
	using SLC_SM_UDAPI_ServiceSpecification.Controllers;
	using SLC_SM_UDAPI_ServiceSpecification.Models;

	[TestClass]
	public class ServiceCategoryControllerTests : ServiceCatalogTestBase
	{
		private ServiceCategoryController controller;

		[TestInitialize]
		public void Initialize()
		{
			controller = new ServiceCategoryController(Mock.Of<ILogger<ServiceCategoryController>>(), Helper.Object);
			AttachContext(controller);
		}

		[TestMethod]
		public void Constructor_NullArguments_Throw()
		{
			Assert.ThrowsException<ArgumentNullException>(() => new ServiceCategoryController(null, Helper.Object));
			Assert.ThrowsException<ArgumentNullException>(() => new ServiceCategoryController(Mock.Of<ILogger<ServiceCategoryController>>(), null));
		}

		// ---- List ----
		[TestMethod]
		public void List_NoFilter_ReturnsAllAsApiShape()
		{
			var a = AddCategory("A", "Video", "icon-a");
			AddCategory("B", "Audio");

			var list = Value<List<JObject>>(controller.ListServiceCategories());

			Assert.AreEqual(2, list.Count);
			var first = list.Single(j => (string)j["name"] == "A");
			Assert.AreEqual(a.Identifier, (string)first["id"]);
			Assert.AreEqual("Video", (string)first["type"]);
			Assert.AreEqual("icon-a", (string)first["icon"]);
		}

		[TestMethod]
		public void List_Type_FiltersCaseInsensitive()
		{
			AddCategory("A", "Video");
			AddCategory("B", "Audio");
			var list = Value<List<JObject>>(controller.ListServiceCategories(type: " video "));
			CollectionAssert.AreEqual(new[] { "A" }, list.Select(j => (string)j["name"]).ToArray());
		}

		[TestMethod]
		public void List_Empty_ReturnsEmptyList()
		{
			Assert.AreEqual(0, Value<List<JObject>>(controller.ListServiceCategories()).Count);
		}

		[DataTestMethod]
		[DataRow(-1, 100)]
		[DataRow(0, 0)]
		[DataRow(0, 501)]
		public void List_InvalidPaging_Returns400(int offset, int limit)
		{
			Assert.AreEqual(400, StatusCode(controller.ListServiceCategories(offset, limit)));
		}

		[TestMethod]
		public void List_RepositoryThrows_Returns500()
		{
			Catalog.SetupGet(c => c.ServiceCategories).Throws(new InvalidOperationException("boom"));
			Assert.AreEqual(500, StatusCode(controller.ListServiceCategories()));
		}

		// ---- Get ----
		[TestMethod]
		public void Get_Existing_Returns200()
		{
			var category = AddCategory("A", "Video");
			var result = controller.GetServiceCategory(category.Identifier.ToUpperInvariant());
			Assert.AreEqual(200, StatusCode(result));
			Assert.AreEqual("A", (string)Value<JObject>(result)["name"]);
		}

		[TestMethod]
		public void Get_Unknown_Returns404()
		{
			Assert.AreEqual(404, StatusCode(controller.GetServiceCategory(Guid.NewGuid().ToString())));
		}

		[DataTestMethod]
		[DataRow(null)]
		[DataRow("")]
		[DataRow("not-a-guid")]
		public void Get_InvalidId_Returns400(string id)
		{
			Assert.AreEqual(400, StatusCode(controller.GetServiceCategory(id)));
		}

		// ---- Create ----
		[TestMethod]
		public void Create_Valid_Returns201AndStoresTrimmedValues()
		{
			var result = controller.CreateServiceCategory(new ServiceCategoryRequest { Name = " Video ", Type = " Live ", Icon = " i " });

			Assert.AreEqual(201, StatusCode(result));
			Assert.AreEqual(1, Categories.Count);
			Assert.AreEqual("Video", Categories[0].Name);
			Assert.AreEqual("Live", Categories[0].Type);
			Assert.AreEqual("i", Categories[0].Icon);
			Assert.IsTrue(Guid.TryParse((string)Value<JObject>(result)["id"], out _));
		}

		[TestMethod]
		public void Create_OnlyName_Returns201()
		{
			Assert.AreEqual(201, StatusCode(controller.CreateServiceCategory(new ServiceCategoryRequest { Name = "X" })));
		}

		[TestMethod]
		public void Create_NullBody_Returns400()
		{
			Assert.AreEqual(400, StatusCode(controller.CreateServiceCategory(null)));
		}

		[DataTestMethod]
		[DataRow(null)]
		[DataRow("  ")]
		public void Create_MissingName_Returns400(string name)
		{
			Assert.AreEqual(400, StatusCode(controller.CreateServiceCategory(new ServiceCategoryRequest { Name = name })));
			Assert.AreEqual(0, Categories.Count);
		}

		// ---- Delete ----
		[TestMethod]
		public void Delete_Unreferenced_Returns204AndRemoves()
		{
			var category = AddCategory("A");
			Assert.AreEqual(204, StatusCode(controller.DeleteServiceCategory(category.Identifier)));
			Assert.AreEqual(0, Categories.Count);
		}

		[TestMethod]
		public void Delete_Unknown_Returns404()
		{
			Assert.AreEqual(404, StatusCode(controller.DeleteServiceCategory(Guid.NewGuid().ToString())));
		}

		[TestMethod]
		public void Delete_InvalidId_Returns400()
		{
			Assert.AreEqual(400, StatusCode(controller.DeleteServiceCategory("x")));
		}

		[TestMethod]
		public void Delete_ReferencedByService_Returns409()
		{
			var category = AddCategory("A");
			Services.Add(new Service { Identifier = Guid.NewGuid().ToString(), CategoryId = new SdmObjectReference<ServiceCategory>(category.Identifier) });

			var result = controller.DeleteServiceCategory(category.Identifier);

			Assert.AreEqual(409, StatusCode(result));
			StringAssert.Contains(ErrorDetail(result), "1 service(s)");
			Assert.AreEqual(1, Categories.Count);
			Assert.AreEqual(0, OrderItemReads, "Order items must not be read when a service already blocks the delete.");
		}

		[TestMethod]
		public void Delete_ReferencedByOrderItem_Returns409()
		{
			var category = AddCategory("A");
			OrderItems.Add(new ServiceOrderItem
			{
				Identifier = Guid.NewGuid().ToString(),
				ServiceInfo = new ServiceOrderItemServiceInfo { ServiceCategoryId = new SdmObjectReference<ServiceCategory>(category.Identifier) },
			});

			Assert.AreEqual(409, StatusCode(controller.DeleteServiceCategory(category.Identifier)));
			Assert.AreEqual(1, Categories.Count);
		}

		[TestMethod]
		public void Delete_OtherCategoryReferenced_Returns204()
		{
			var category = AddCategory("A");
			Services.Add(new Service { Identifier = Guid.NewGuid().ToString(), CategoryId = new SdmObjectReference<ServiceCategory>(Guid.NewGuid().ToString()) });
			Assert.AreEqual(204, StatusCode(controller.DeleteServiceCategory(category.Identifier)));
		}

		private ServiceCategory AddCategory(string name, string type = null, string icon = null)
		{
			return AddWithId(Categories, new ServiceCategory { Name = name, Type = type, Icon = icon });
		}
	}
}
