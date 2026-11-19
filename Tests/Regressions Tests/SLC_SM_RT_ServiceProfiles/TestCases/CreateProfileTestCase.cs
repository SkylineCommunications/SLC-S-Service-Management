namespace SLC_SM_RT_ServiceProfiles.TestCases
{
	using System;
	using System.Collections.Generic;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Tests;

	/// <summary>
	/// Creates a profile with a configuration parameter value and a standalone configuration parameter on the specification.
	/// </summary>
	internal sealed class CreateProfileTestCase : TestCase
	{
		private readonly ServiceProfilesTestData testData;

		public CreateProfileTestCase(ServiceProfilesTestData testData)
		{
			this.testData = testData ?? throw new ArgumentNullException(nameof(testData));
		}

		public override void Execute(IEngine engine)
		{
			var scriptParameters = new Dictionary<string, string>
			{
				{ "DOM ID", testData.SpecificationId },
				{
					"Payload",
					testData.BuildSavePayload(ServiceProfilesTestData.InitialProfileValue, ServiceProfilesTestData.InitialStandaloneValue, exposeAtServiceOrder: true, includeIdentifiers: false)
				},
			};

			try
			{
				TestTools.ExecuteSubScript(this, engine, Script.ScriptUnderTest, scriptParameters);
			}
			finally
			{
				// Record whatever was created, also on failure, so that cleanup can remove it.
				testData.CaptureCreatedIds();
			}
		}

		public override void PerformCleanup(IEngine engine)
		{
			// Cleanup of every fixture is owned by the test data and triggered by the last test case.
		}
	}
}
