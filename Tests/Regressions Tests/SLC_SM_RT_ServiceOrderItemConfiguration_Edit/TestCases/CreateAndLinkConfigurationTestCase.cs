namespace SLC_SM_RT_ServiceOrderItemConfiguration_Edit.TestCases
{
	using System;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Tests;

	/// <summary>
	/// Creates the parameter value and order item configuration, and links them to the order item.
	/// </summary>
	internal sealed class CreateAndLinkConfigurationTestCase : TestCase
	{
		private readonly ServiceOrderItemConfigurationEditTestData testData;

		public CreateAndLinkConfigurationTestCase(ServiceOrderItemConfigurationEditTestData testData)
		{
			this.testData = testData ?? throw new ArgumentNullException(nameof(testData));
		}

		public override void Execute(IEngine engine)
		{
			testData.CreateAndLinkConfiguration();
		}

		public override void PerformCleanup(IEngine engine)
		{
			// Cleanup of every fixture is owned by the test data and triggered by the last test case.
		}
	}
}
