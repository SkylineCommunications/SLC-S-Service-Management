namespace SLC_SM_UDAPI_ServiceManagement.Tests
{
	using Moq;
	using Skyline.DataMiner.Automation;

	/// <summary>
	/// Covers the manual <see cref="Script.Run"/> entry point that accepts a JSON 'Request' script parameter.
	/// </summary>
	[TestClass]
	public class ScriptRunTests
	{
		private const int RequestParameterId = 2;

		[TestMethod]
		public void Run_EmptyRequest_ExitsFail()
		{
			var engine = EngineWithRequest(" ");

			Script.Run(engine.Object);

			engine.Verify(e => e.ExitFail(It.Is<string>(m => m.Contains("is empty"))), Times.Once);
			engine.Verify(e => e.AddScriptOutput(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
		}

		[TestMethod]
		public void Run_MissingRequestParameter_ExitsFail()
		{
			var engine = new Mock<IEngine>();
			engine.Setup(e => e.GetScriptParam(RequestParameterId)).Returns((ScriptParam)null);

			Script.Run(engine.Object);

			engine.Verify(e => e.ExitFail(It.Is<string>(m => m.Contains("is empty"))), Times.Once);
		}

		[TestMethod]
		public void Run_InvalidJson_ExitsFail()
		{
			var engine = EngineWithRequest("{ not json");

			Script.Run(engine.Object);

			engine.Verify(e => e.ExitFail(It.Is<string>(m => m.Contains("not a valid ApiRequest JSON"))), Times.Once);
			engine.Verify(e => e.AddScriptOutput(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
		}

		[TestMethod]
		public void Run_MissingRoute_ExitsFail()
		{
			var engine = EngineWithRequest("{ \"RequestMethod\": \"Get\" }");

			Script.Run(engine.Object);

			engine.Verify(e => e.ExitFail(It.Is<string>(m => m.Contains("must contain a route"))), Times.Once);
			engine.Verify(e => e.AddScriptOutput(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
		}

		private static Mock<IEngine> EngineWithRequest(string value)
		{
			var parameter = new Mock<ScriptParam>();
			parameter.SetupGet(p => p.Value).Returns(value);

			var engine = new Mock<IEngine>();
			engine.Setup(e => e.GetScriptParam(RequestParameterId)).Returns(parameter.Object);
			return engine;
		}
	}
}
