namespace SLC_SM_RT_AddServiceItemDuplicateLabel.TestCases
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	using DomHelpers.SlcServicemanagement;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Tests;

	using Models = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;

	/// <summary>
	/// Verifies that adding the same definition reference twice produces the labels
	/// "&lt;base&gt;" and "&lt;base&gt; (1)" and that both items keep the requested service item type.
	/// </summary>
	internal sealed class ValidateDuplicateLabelsTestCase : TestCase
	{
		private readonly AddServiceItemTestData testData;

		public ValidateDuplicateLabelsTestCase(AddServiceItemTestData testData)
		{
			this.testData = testData ?? throw new ArgumentNullException(nameof(testData));
		}

		public override void Execute(IEngine engine)
		{
			IReadOnlyList<Models.ServiceItem> addedItems = testData.ReadAddedServiceItems();

			TestTools.AssertEqual("2", addedItems.Count.ToString(), "Number of added service items");

			TestTools.Assert(
				addedItems.Any(item => String.Equals(item.Label, testData.DefinitionReference, StringComparison.InvariantCulture)),
				$"A service item labelled '{testData.DefinitionReference}' should exist, but the labels are: {DescribeLabels(addedItems)}.");

			string expectedDuplicateLabel = $"{testData.DefinitionReference} (1)";

			TestTools.Assert(
				addedItems.Any(item => String.Equals(item.Label, expectedDuplicateLabel, StringComparison.InvariantCulture)),
				$"A service item labelled '{expectedDuplicateLabel}' should exist, but the labels are: {DescribeLabels(addedItems)}.");

			TestTools.Assert(
				addedItems.All(item => item.Type == SlcServicemanagementIds.Enums.ServiceitemtypesEnum.Service),
				$"Both added service items should have type '{SlcServicemanagementIds.Enums.ServiceitemtypesEnum.Service}', but the types are: {DescribeTypes(addedItems)}.");
		}

		public override void PerformCleanup(IEngine engine)
		{
			testData.CleanUp();
		}

		private static string DescribeLabels(IEnumerable<Models.ServiceItem> items)
		{
			return String.Join(", ", items.Select(item => $"'{item.Label}'"));
		}

		private static string DescribeTypes(IEnumerable<Models.ServiceItem> items)
		{
			return String.Join(", ", items.Select(item => $"'{item.Type}'"));
		}
	}
}
