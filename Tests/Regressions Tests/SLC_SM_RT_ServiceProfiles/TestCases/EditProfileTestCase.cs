namespace SLC_SM_RT_ServiceProfiles.TestCases
{
	using System;
	using System.Collections.Generic;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Tests;

	/// <summary>
	/// Edits the profile parameter value, the standalone parameter value and a profile flag, re-using the existing identifiers.
	/// </summary>
	internal sealed class EditProfileTestCase : TestCase
	{
		private readonly ServiceProfilesTestData testData;

		public EditProfileTestCase(ServiceProfilesTestData testData)
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
					testData.BuildSavePayload(ServiceProfilesTestData.EditedProfileValue, ServiceProfilesTestData.EditedStandaloneValue, exposeAtServiceOrder: false, includeIdentifiers: true)
				},
			};

			TestTools.ExecuteSubScript(this, engine, Script.ScriptUnderTest, scriptParameters);
		}

		public override void PerformCleanup(IEngine engine)
		{
			// Cleanup of every fixture is owned by the test data and triggered by the last test case.
		}
	}
}
