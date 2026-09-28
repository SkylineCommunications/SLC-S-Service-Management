namespace SLC_SM_RT_DynamicDeleteService.TestCases
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Tests;

	using Models = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;

	/// <summary>
	/// Removes the remaining service item relationship by its identifier and verifies that only the
	/// relationship is removed, not the service items it connected.
	/// This covers the relationship branch of the script under test, which the specification
	/// regression test does not exercise.
	/// </summary>
	internal sealed class DeleteServiceConnectionTestCase : TestCase
	{
		private readonly DynamicDeleteServiceTestData testData;

		public DeleteServiceConnectionTestCase(DynamicDeleteServiceTestData testData)
		{
			this.testData = testData ?? throw new ArgumentNullException(nameof(testData));
		}

		public override void Execute(IEngine engine)
		{
			Models.Service service = testData.ReadServiceOrThrow();

			Models.ServiceItemRelationship relationship = (service.ServiceItemsRelationships ?? new List<Models.ServiceItemRelationship>())
				.FirstOrDefault();

			TestTools.Assert(relationship != null, "Expected one remaining service item relationship to delete.");

			TestTools.Assert(
				Guid.TryParse(relationship.Id, out _),
				$"The script under test only deletes relationships whose identifier is a GUID, got '{relationship.Id}'.");

			var scriptParameters = new Dictionary<string, string>
			{
				{ "DomId", testData.ServiceId },
				{ "NodeIds", String.Empty },
				{ "ConnectionIds", relationship.Id },
			};

			TestTools.ExecuteSubScript(this, engine, Script.ScriptUnderTest, scriptParameters);

			Models.Service updatedService = testData.ReadServiceOrThrow();

			TestTools.AssertEqual(
				"0",
				(updatedService.ServiceItemsRelationships?.Count ?? 0).ToString(),
				"Number of service item relationships after deleting the last relationship");

			TestTools.AssertEqual(
				"2",
				(updatedService.ServiceItems?.Count ?? 0).ToString(),
				"Number of service items after deleting a relationship");
		}

		public override void PerformCleanup(IEngine engine)
		{
			testData.CleanUp();
		}
	}
}
