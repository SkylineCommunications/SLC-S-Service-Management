namespace SLC_SM_UDAPI_ServiceManagement.Tests.Controllers
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
	using SLC_SM_UDAPI_ServiceManagement.Controllers;

	[TestClass]
	public class ListServiceSpecificationsTests
	{
		private List<ServiceSpecification> stored;
		private Mock<IBulkRepository<ServiceSpecification>> repository;
		private ServiceSpecificationController controller;

		[TestInitialize]
		public void Initialize()
		{
			stored = new List<ServiceSpecification>();
			repository = new Mock<IBulkRepository<ServiceSpecification>>();
			repository.Setup(r => r.Read(It.IsAny<FilterElement<ServiceSpecification>>()))
				.Returns<FilterElement<ServiceSpecification>>(f => stored.Where(f.getLambda()).ToList());

			var catalog = new Mock<IServiceCatalogApi>();
			catalog.SetupGet(c => c.ServiceSpecifications).Returns(repository.Object);
			var helper = new Mock<IServiceManagementApiHelper>();
			helper.SetupGet(h => h.ServiceCatalog).Returns(catalog.Object);

			controller = new ServiceSpecificationController(Mock.Of<ILogger<ServiceSpecificationController>>(), helper.Object);

			// The UDAPI pipeline normally supplies the context; its setters are internal to the toolkit.
			var context = new ApiContext();
			SetProperty(context, nameof(ApiContext.InputConverters), new List<IInputConverter> { new NewtonsoftConverter() });
			SetProperty(context, nameof(ApiContext.OutputConverters), new List<IOutputConverter> { new NewtonsoftConverter() });
			SetProperty(controller, nameof(ServiceSpecificationController.ApiContext), context);
		}

		[TestMethod]
		public void List_NoFilters_ReturnsAllSpecifications()
		{
			Add("A");
			Add("B");
			var result = controller.ListServiceSpecifications();
			Assert.AreEqual(200, StatusCode(result));
			Assert.AreEqual(2, Value<List<ServiceSpecification>>(result).Count);
		}

		[TestMethod]
		public void List_Empty_ReturnsEmptyList()
		{
			Assert.AreEqual(0, Value<List<ServiceSpecification>>(controller.ListServiceSpecifications()).Count);
		}

		[TestMethod]
		public void List_Name_MatchesCaseInsensitiveSubstring()
		{
			Add("4K Contribution Feed");
			Add("HD Distribution");
			Add("Other");
			var names = Names(controller.ListServiceSpecifications(name: " contrib "));
			CollectionAssert.AreEqual(new[] { "4K Contribution Feed" }, names);
		}

		[TestMethod]
		public void List_NameNoMatch_ReturnsEmptyList()
		{
			Add("A");
			Assert.AreEqual(0, Names(controller.ListServiceSpecifications(name: "zzz")).Length);
		}

		[TestMethod]
		public void List_SortAscending_SortsByNameCaseInsensitive()
		{
			Add("b");
			Add("C");
			Add("a");
			CollectionAssert.AreEqual(new[] { "a", "b", "C" }, Names(controller.ListServiceSpecifications(sort: "name")));
		}

		[TestMethod]
		public void List_SortDescending_SortsByNameDescending()
		{
			Add("a");
			Add("c");
			Add("b");
			CollectionAssert.AreEqual(new[] { "c", "b", "a" }, Names(controller.ListServiceSpecifications(sort: "-name")));
		}

		[TestMethod]
		public void List_SortById_UsesIdentifier()
		{
			var second = Add("x", "bbbbbbbb-0000-0000-0000-000000000000");
			var first = Add("y", "aaaaaaaa-0000-0000-0000-000000000000");
			CollectionAssert.AreEqual(new[] { first.Name, second.Name }, Names(controller.ListServiceSpecifications(sort: "id")));
		}

		[DataTestMethod]
		[DataRow("unknown")]
		[DataRow("-")]
		[DataRow("serviceItems")]
		public void List_UnsupportedSort_Returns400(string sort)
		{
			Assert.AreEqual(400, StatusCode(controller.ListServiceSpecifications(sort: sort)));
			repository.Verify(r => r.Read(It.IsAny<FilterElement<ServiceSpecification>>()), Times.Never);
		}

		[TestMethod]
		public void List_Fields_ReturnsOnlyRequestedProperties()
		{
			Add("A");
			var items = Value<List<JObject>>(controller.ListServiceSpecifications(fields: "name, description"));
			Assert.AreEqual(1, items.Count);
			CollectionAssert.AreEquivalent(new[] { "Name", "Description" }, items[0].Properties().Select(p => p.Name).ToArray());
		}

		[DataTestMethod]
		[DataRow(-1, 100)]
		[DataRow(0, 0)]
		[DataRow(0, 501)]
		public void List_OffsetOrLimitOutOfRange_Returns400(int offset, int limit)
		{
			Assert.AreEqual(400, StatusCode(controller.ListServiceSpecifications(offset: offset, limit: limit)));
		}

		[TestMethod]
		public void List_OffsetAndLimit_AreNotAppliedYet()
		{
			Add("A");
			Add("B");
			Add("C");
			Assert.AreEqual(3, Names(controller.ListServiceSpecifications(offset: 1, limit: 1)).Length);
		}

		[TestMethod]
		public void List_RepositoryThrows_Returns500WithoutExceptionDetails()
		{
			repository.Setup(r => r.Read(It.IsAny<FilterElement<ServiceSpecification>>())).Throws(new InvalidOperationException("secret detail"));
			var result = controller.ListServiceSpecifications();
			Assert.AreEqual(500, StatusCode(result));
			var body = JObject.FromObject(result.GetType().GetProperty("Value").GetValue(result)).ToString();
			Assert.IsFalse(body.Contains("secret detail"));
		}

		private ServiceSpecification Add(string name, string id = null)
		{
			var spec = new ServiceSpecification
			{
				Identifier = id ?? Guid.NewGuid().ToString(),
				Name = name,
				Description = "desc",
			};
			stored.Add(spec);
			return spec;
		}

		private static string[] Names(IApiResult result)
		{
			return Value<List<ServiceSpecification>>(result).Select(s => s.Name).ToArray();
		}

		private static int StatusCode(IApiResult result)
		{
			Assert.IsInstanceOfType(result, typeof(StatusCodeResult));
			return ((StatusCodeResult)result).StatusCode;
		}

		private static T Value<T>(IApiResult result)
		{
			var value = result.GetType().GetProperty("Value").GetValue(result);
			Assert.IsInstanceOfType(value, typeof(T));
			return (T)value;
		}

		private static void SetProperty(object target, string name, object value)
		{
			var property = target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			Assert.IsNotNull(property, $"Property '{name}' not found; toolkit layout changed.");
			property.SetValue(target, value);
		}
	}
}
