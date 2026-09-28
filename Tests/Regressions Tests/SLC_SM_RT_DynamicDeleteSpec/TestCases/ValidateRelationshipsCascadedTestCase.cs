namespace SLC_SM_RT_DynamicDeleteSpec.TestCases
{
	using System;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Tests;

	using Models = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;

	/// <summary>
	/// Verifies that deleting a node also removes the relationships that node took part in.
	/// </summary>
	internal sealed class ValidateRelationshipsCascadedTestCase : TestCase
	{
		private readonly DynamicDeleteSpecTestData testData;

		public ValidateRelationshipsCascadedTestCase(DynamicDeleteSpecTestData testData)
		{
			this.testData = testData ?? throw new ArgumentNullException(nameof(testData));
		}

		public override void Execute(IEngine engine)
		{
			Models.ServiceSpecification specification = testData.ReadSpecification();

			int relationshipCount = specification.ServiceItemsRelationships?.Count ?? 0;

			TestTools.AssertEqual(
				"0",
				relationshipCount.ToString(),
				$"Number of relationships left after deleting service item '{DynamicDeleteSpecTestData.DeletedNodeId}'");
		}

		public override void PerformCleanup(IEngine engine)
		{
			testData.CleanUp();
		}
	}
}
