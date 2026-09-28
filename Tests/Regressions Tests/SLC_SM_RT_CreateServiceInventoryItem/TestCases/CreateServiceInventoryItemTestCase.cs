namespace SLC_SM_RT_CreateServiceInventoryItem.TestCases
{
	using System;
	using System.Collections.Generic;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Tests;

	/// <summary>
	/// Runs the script under test, which turns the service order item into a service inventory item.
	/// </summary>
	internal sealed class CreateServiceInventoryItemTestCase : TestCase
	{
		private readonly ServiceInventoryItemTestData testData;

		public CreateServiceInventoryItemTestCase(ServiceInventoryItemTestData testData)
		{
			this.testData = testData ?? throw new ArgumentNullException(nameof(testData));
		}

		public override void Execute(IEngine engine)
		{
			var scriptParameters = new Dictionary<string, string>
			{
				{ "DOM ID", testData.OrderItemId },
				{ "Action", "AddItemSilent" },
			};

			TestTools.ExecuteSubScript(this, engine, Script.ScriptUnderTest, scriptParameters);
		}

		public override void PerformCleanup(IEngine engine)
		{
			// Cleanup of every fixture is owned by the test data and triggered by the last test case.
		}
	}
}
