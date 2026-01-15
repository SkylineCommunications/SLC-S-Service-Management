namespace SLC_SM_Common.Tests.Extensions
{
	using Moq;
	using Newtonsoft.Json;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.ServiceManagement.Common.Extensions;

	[TestClass]
	public class ScriptExtensionsTests
	{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
		private Mock<IEngine> engineMock;
		private Mock<ScriptParam> paramMock;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

		private enum TestEnum
		{
			Foo,
			Bar,
		}

		[TestMethod]
		public void ReadScriptParamFromApp_Generic_ReturnsFirstOrDefault()
		{
			var value = "42";
			paramMock.Setup(p => p.Value).Returns(value);
			engineMock.Setup(e => e.GetScriptParam("TestParam")).Returns(paramMock.Object);

			int result = engineMock.Object.ReadScriptParamFromApp<int>("TestParam");

			Assert.AreEqual(42, result);
		}

		[TestMethod]
		public void ReadScriptParamFromApp_String_ReturnsFirstOrDefault()
		{
			var value = "hello";
			paramMock.Setup(p => p.Value).Returns(value);
			engineMock.Setup(e => e.GetScriptParam("TestParam")).Returns(paramMock.Object);

			string result = engineMock.Object.ReadScriptParamFromApp("TestParam");

			Assert.AreEqual("hello", result);
		}

		[TestMethod]
		public void ReadScriptParamsFromApp_Generic_DeserializesJsonArray()
		{
			var list = new List<int> { 1, 2, 3 };
			var json = JsonConvert.SerializeObject(list);
			paramMock.Setup(p => p.Value).Returns(json);
			engineMock.Setup(e => e.GetScriptParam("TestParam")).Returns(paramMock.Object);

			var result = engineMock.Object.ReadScriptParamsFromApp<int>("TestParam");

			CollectionAssert.AreEqual(list, (List<int>)result);
		}

		[TestMethod]
		public void ReadScriptParamsFromApp_Generic_ParsesEnum()
		{
			paramMock.Setup(p => p.Value).Returns("Bar");
			engineMock.Setup(e => e.GetScriptParam("TestParam")).Returns(paramMock.Object);

			var result = engineMock.Object.ReadScriptParamsFromApp<TestEnum>("TestParam");

			Assert.AreEqual(TestEnum.Bar, ((List<TestEnum>)result)[0]);
		}

		[TestMethod]
		public void ReadScriptParamsFromApp_Generic_ParsesGuid()
		{
			var guid = Guid.NewGuid();
			paramMock.Setup(p => p.Value).Returns(guid.ToString());
			engineMock.Setup(e => e.GetScriptParam("TestParam")).Returns(paramMock.Object);

			var result = engineMock.Object.ReadScriptParamsFromApp<Guid>("TestParam");

			Assert.AreEqual(guid, ((List<Guid>)result)[0]);
		}

		[TestMethod]
		public void ReadScriptParamsFromApp_Generic_ParsesString()
		{
			paramMock.Setup(p => p.Value).Returns("abc");
			engineMock.Setup(e => e.GetScriptParam("TestParam")).Returns(paramMock.Object);

			var result = engineMock.Object.ReadScriptParamsFromApp<string>("TestParam");

			Assert.AreEqual("abc", ((List<string>)result)[0]);
		}

		[TestMethod]
		public void ReadScriptParamsFromApp_Generic_ThrowsIfParamNull()
		{
			engineMock.Setup(e => e.GetScriptParam("TestParam")).Returns((ScriptParam?)null);

			Assert.ThrowsException<ArgumentException>(() =>
			{
				engineMock.Object.ReadScriptParamsFromApp<int>("TestParam");
			});
		}

		[TestMethod]
		public void ReadScriptParamsFromApp_String_DelegatesToGeneric()
		{
			paramMock.Setup(p => p.Value).Returns("[\"a\",\"b\"]");
			engineMock.Setup(e => e.GetScriptParam("TestParam")).Returns(paramMock.Object);

			var result = engineMock.Object.ReadScriptParamsFromApp("TestParam");

			CollectionAssert.AreEqual(new List<string> { "a", "b" }, (List<string>)result);
		}

		[TestInitialize]
		public void Setup()
		{
			engineMock = new Mock<IEngine>();
			paramMock = new Mock<ScriptParam>();
		}
	}
}