namespace SLC_SM_RT_DynamicDeleteSpec.TestCases
{
	using System;
	using System.Linq;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Tests;

	using Models = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;

	/// <summary>
	/// Verifies the targeted node is gone while the untargeted node survives.
	/// </summary>
	internal sealed class ValidateNodesTestCase : TestCase
	{
		private readonly DynamicDeleteSpecTestData testData;

		public ValidateNodesTestCase(DynamicDeleteSpecTestData testData)
		{
			this.testData = testData ?? throw new ArgumentNullException(nameof(testData));
		}

		public override void Execute(IEngine engine)
		{
			Models.ServiceSpecification specification = testData.ReadSpecification();

			bool containsDeletedNode = specification.ServiceItems
				.Any(item => item.ServiceItemID.HasValue && item.ServiceItemID.Value == DynamicDeleteSpecTestData.DeletedNodeId);

			TestTools.Assert(
				!containsDeletedNode,
				$"Service item '{DynamicDeleteSpecTestData.DeletedNodeId}' should have been deleted, but it still exists.");

			bool containsRemainingNode = specification.ServiceItems
				.Any(item => item.ServiceItemID.HasValue && item.ServiceItemID.Value == DynamicDeleteSpecTestData.RemainingNodeId);

			TestTools.Assert(
				containsRemainingNode,
				$"Service item '{DynamicDeleteSpecTestData.RemainingNodeId}' should have been left untouched, but it no longer exists.");
		}

		public override void PerformCleanup(IEngine engine)
		{
			// Cleanup of the fixture is owned by the test data and triggered by the last test case.
		}
	}
}
