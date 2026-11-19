namespace SLC_SM_RT_ServiceProfiles.TestCases
{
	using System;
	using System.Collections.Generic;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Tests;

	/// <summary>
	/// Deletes the profile and its parameter value from the specification, keeping the standalone parameter.
	/// </summary>
	internal sealed class DeleteProfileTestCase : TestCase
	{
		private readonly ServiceProfilesTestData testData;

		public DeleteProfileTestCase(ServiceProfilesTestData testData)
		{
			this.testData = testData ?? throw new ArgumentNullException(nameof(testData));
		}

		public override void Execute(IEngine engine)
		{
			var scriptParameters = new Dictionary<string, string>
			{
				{ "DOM ID", testData.SpecificationId },
				{ "Payload", testData.BuildDeletePayload() },
			};

			TestTools.ExecuteSubScript(this, engine, Script.ScriptUnderTest, scriptParameters);
		}

		public override void PerformCleanup(IEngine engine)
		{
			// Cleanup of every fixture is owned by the test data and triggered by the last test case.
		}
	}
}
