namespace SLC_SM_RT_ServiceUpdate.TestCases
{
	using System;
	using System.Linq;

	using Newtonsoft.Json;
	using Newtonsoft.Json.Linq;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Tests;

	/// <summary>
	/// Verifies an attempted Service ID change is rejected without persisting any edits.
	/// </summary>
	internal sealed class RefusedServiceIdChangeTestCase : TestCase
	{
		private readonly ServiceUpdateTestData testData;

		public RefusedServiceIdChangeTestCase(ServiceUpdateTestData testData)
		{
			this.testData = testData ?? throw new ArgumentNullException(nameof(testData));
		}

		public override void Execute(IEngine engine)
		{
			var before = testData.ReadService();
			TestTools.Assert(before != null, $"Service '{testData.ServiceId}' was not found before the refused edit.");
			JObject snapshot = JObject.FromObject(before);

			var payload = new
			{
				ServiceID = $"RT-SVC-Rejected-{Guid.NewGuid()}",
				Name = $"RT_ServiceUpdate_Rejected_{Guid.NewGuid()}",
				Category = testData.CategoryName,
				Specification = testData.SpecificationName,
				Organization = String.Empty,
			};

			SubScriptOptions subScript = engine.PrepareSubScript(Script.ScriptUnderTest);
			subScript.Synchronous = true;
			subScript.ExtendedErrorInfo = true;
			subScript.SelectScriptParam("DOM ID", testData.ServiceId);
			subScript.SelectScriptParam("Action", "Edit");
			subScript.SelectScriptParam("Payload", JsonConvert.SerializeObject(payload));
			subScript.StartScript();

			TestTools.Assert(subScript.HadError, "An edit changing the Service ID was expected to fail.");
			string errors = String.Join(Environment.NewLine, subScript.GetErrorMessages() ?? Enumerable.Empty<string>());
			TestTools.Assert(
				errors.IndexOf("A service ID cannot be changed after creation.", StringComparison.OrdinalIgnoreCase) >= 0,
				$"The edit failed for a reason other than the immutable Service ID: {errors}");

			var after = testData.ReadService();
			TestTools.Assert(after != null, $"Service '{testData.ServiceId}' was not found after the refused edit.");
			TestTools.Assert(
				JToken.DeepEquals(snapshot, JObject.FromObject(after)),
				$"Service '{testData.ServiceId}' changed after the refused Service ID edit.");
		}

		public override void PerformCleanup(IEngine engine)
		{
			// The final validation test case owns fixture cleanup.
		}
	}
}
