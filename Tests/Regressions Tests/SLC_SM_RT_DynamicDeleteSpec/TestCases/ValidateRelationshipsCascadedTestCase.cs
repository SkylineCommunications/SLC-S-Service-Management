namespace SLC_SM_RT_DynamicDeleteSpec.TestCases
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Tests;

	using Models = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;

	/// <summary>
	/// Verifies that deleting a node also removes the relationships that node took part in,
	/// while relationships between the remaining nodes are kept.
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

			List<Models.ServiceItemRelationship> relationships = specification.ServiceItemsRelationships
				?? new List<Models.ServiceItemRelationship>();
			string deletedNodeId = DynamicDeleteSpecTestData.DeletedNodeId.ToString();

			TestTools.Assert(
				!relationships.Any(x => x.ParentServiceItem == deletedNodeId || x.ChildServiceItem == deletedNodeId),
				$"A relationship still references the deleted service item '{deletedNodeId}'.");

			TestTools.AssertEqual(
				"1",
				relationships.Count.ToString(),
				$"Number of relationships left after deleting service item '{deletedNodeId}'");
		}

		public override void PerformCleanup(IEngine engine)
		{
			testData.CleanUp();
		}
	}
}
