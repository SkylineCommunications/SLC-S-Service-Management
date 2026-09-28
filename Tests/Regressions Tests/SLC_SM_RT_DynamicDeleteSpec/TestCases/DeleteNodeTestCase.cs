namespace SLC_SM_RT_DynamicDeleteSpec.TestCases
{
	using System;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Tests;

	/// <summary>
	/// Runs the script under test to delete a single node from the specification.
	/// </summary>
	internal sealed class DeleteNodeTestCase : TestCase
	{
		private readonly DynamicDeleteSpecTestData testData;

		public DeleteNodeTestCase(DynamicDeleteSpecTestData testData)
		{
			this.testData = testData ?? throw new ArgumentNullException(nameof(testData));
		}

		public override void Execute(IEngine engine)
		{
			testData.InvokeDynamicDelete(DynamicDeleteSpecTestData.DeletedNodeId.ToString(), String.Empty);
		}

		public override void PerformCleanup(IEngine engine)
		{
			// Cleanup of the fixture is owned by the test data and triggered by the last test case.
		}
	}
}
