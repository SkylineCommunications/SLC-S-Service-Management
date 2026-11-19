namespace SLC_SM_RT_AddServiceItemDuplicateLabel.TestCases
{
	using System;
	using System.Collections.Generic;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Tests;

	using Models = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;

	/// <summary>
	/// Adds one service item with the shared definition reference and verifies how many items exist afterwards.
	/// One instance is added per invocation, so the first add and the duplicate add are reported separately.
	/// </summary>
	internal sealed class AddServiceItemTestCase : TestCase
	{
		private readonly AddServiceItemTestData testData;
		private readonly string name;
		private readonly int expectedItemCount;

		public AddServiceItemTestCase(AddServiceItemTestData testData, string name, int expectedItemCount)
		{
			this.testData = testData ?? throw new ArgumentNullException(nameof(testData));
			this.name = name ?? throw new ArgumentNullException(nameof(name));
			this.expectedItemCount = expectedItemCount;
		}

		/// <inheritdoc/>
		public override string Name => name;

		public override void Execute(IEngine engine)
		{
			testData.InvokeAddServiceItem();

			IReadOnlyList<Models.ServiceItem> addedItems = testData.ReadAddedServiceItems();

			TestTools.AssertEqual(
				expectedItemCount.ToString(),
				addedItems.Count.ToString(),
				$"Number of service items with definition reference '{testData.DefinitionReference}'");
		}

		public override void PerformCleanup(IEngine engine)
		{
			// Cleanup of the fixture is owned by the test data and triggered by the last test case.
		}
	}
}
