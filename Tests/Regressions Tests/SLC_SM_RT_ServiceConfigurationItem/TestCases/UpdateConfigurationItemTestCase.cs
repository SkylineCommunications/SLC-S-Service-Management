namespace SLC_SM_RT_ServiceConfigurationItem.TestCases
{
	using System;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Tests;

	/// <summary>
	/// Updates the Mandatory flag of the Service Configuration Item.
	/// </summary>
	internal sealed class UpdateConfigurationItemTestCase : TestCase
	{
		private readonly ServiceConfigurationItemTestData testData;

		public UpdateConfigurationItemTestCase(ServiceConfigurationItemTestData testData)
		{
			this.testData = testData ?? throw new ArgumentNullException(nameof(testData));
		}

		public override void Execute(IEngine engine)
		{
			testData.UpdateConfigurationItem();
		}

		public override void PerformCleanup(IEngine engine)
		{
			// Cleanup of every fixture is owned by the test data and triggered by the last test case.
		}
	}
}
