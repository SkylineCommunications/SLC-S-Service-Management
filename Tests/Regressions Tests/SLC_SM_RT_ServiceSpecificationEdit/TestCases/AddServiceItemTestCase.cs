namespace SLC_SM_RT_ServiceSpecificationEdit.TestCases
{
	using System;
	using System.Collections.Generic;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Tests;

	/// <summary>
	/// Edits the service items of the specification by adding a "Service" item.
	/// </summary>
	internal sealed class AddServiceItemTestCase : TestCase
	{
		private readonly ServiceSpecificationEditTestData testData;

		public AddServiceItemTestCase(ServiceSpecificationEditTestData testData)
		{
			this.testData = testData ?? throw new ArgumentNullException(nameof(testData));
		}

		public override void Execute(IEngine engine)
		{
			var scriptParameters = new Dictionary<string, string>
			{
				{ "DOM ID", testData.SpecificationId },
				{ "ServiceItemType", "Service" },
				{ "DefinitionReference", testData.DefinitionReference },
			};

			TestTools.ExecuteSubScript(this, engine, Script.AddServiceItemScript, scriptParameters);
		}

		public override void PerformCleanup(IEngine engine)
		{
			// Cleanup of every fixture is owned by the test data and triggered by the last test case.
		}
	}
}
