namespace SLC_SM_RT_AddServiceItem.TestCases
{
	using System;
	using System.Collections.Generic;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Tests;

	/// <summary>
	/// Runs the script under test, which adds a service item to the Service Specification fixture.
	/// </summary>
	internal sealed class AddServiceItemTestCase : TestCase
	{
		private readonly AddServiceItemTestData testData;

		public AddServiceItemTestCase(AddServiceItemTestData testData)
		{
			this.testData = testData ?? throw new ArgumentNullException(nameof(testData));
		}

		public override void Execute(IEngine engine)
		{
			var scriptParameters = new Dictionary<string, string>
			{
				{ "DOM ID", testData.SpecificationId },
				{ "ServiceItemType", Script.ServiceItemType },
				{ "DefinitionReference", testData.DefinitionReference },
			};

			TestTools.ExecuteSubScript(this, engine, Script.ScriptUnderTest, scriptParameters);
		}

		public override void PerformCleanup(IEngine engine)
		{
			// Cleanup of the fixture is owned by the test data and triggered by the last test case.
		}
	}
}
