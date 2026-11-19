namespace SLC_SM_RT_ServiceUpdate.TestCases
{
	using System;
	using System.Collections.Generic;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Tests;

	/// <summary>
	/// Edits the general information of the service through the script under test.
	/// </summary>
	internal sealed class EditServiceTestCase : TestCase
	{
		private readonly ServiceUpdateTestData testData;
		private readonly string stage;
		private readonly bool indefiniteRuntime;

		public EditServiceTestCase(ServiceUpdateTestData testData, string stage, bool indefiniteRuntime)
		{
			this.testData = testData ?? throw new ArgumentNullException(nameof(testData));
			this.stage = stage ?? throw new ArgumentNullException(nameof(stage));
			this.indefiniteRuntime = indefiniteRuntime;
		}

		/// <inheritdoc/>
		public override string Name => $"{nameof(EditServiceTestCase)}_{stage}";

		public override void Execute(IEngine engine)
		{
			var scriptParameters = new Dictionary<string, string>
			{
				{ "DOM ID", testData.ServiceId },
				{ "Action", "Edit" },
				{ "Payload", testData.BuildEditPayload(stage, indefiniteRuntime) },
			};

			TestTools.ExecuteSubScript(this, engine, Script.ScriptUnderTest, scriptParameters);
		}

		public override void PerformCleanup(IEngine engine)
		{
			// Cleanup of every fixture is owned by the test data and triggered by the last test case.
		}
	}
}
