namespace SLC_SM_RT_ServiceConfiguration_EditDuplicate.TestCases
{
	using System;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Tests;

	using Models = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;

	/// <summary>
	/// Verifies the duplicated service is a new "(Copy)" service without monitoring settings.
	/// </summary>
	internal sealed class ValidateDuplicatedServiceTestCase : TestCase
	{
		private readonly ServiceConfigurationEditDuplicateTestData testData;

		public ValidateDuplicatedServiceTestCase(ServiceConfigurationEditDuplicateTestData testData)
		{
			this.testData = testData ?? throw new ArgumentNullException(nameof(testData));
		}

		public override void Execute(IEngine engine)
		{
			Models.Service source = testData.ReadService();
			Models.Service duplicate = testData.ReadDuplicatedService();

			TestTools.Assert(source != null, "Source service was not found during validation.");
			TestTools.Assert(duplicate != null, "Duplicated service was not found during validation.");

			TestTools.Assert(
				!String.Equals(source.Identifier, duplicate.Identifier, StringComparison.OrdinalIgnoreCase),
				"Duplicated service must be a new service.");
			TestTools.Assert(
				!String.Equals(source.ServiceID, duplicate.ServiceID, StringComparison.OrdinalIgnoreCase),
				"Duplicated service must get its own service ID.");
			TestTools.AssertEqual($"{source.Name} (Copy)", duplicate.Name, "Duplicated service name");
			TestTools.AssertEqual(source.Description, duplicate.Description, "Duplicated service description");
			TestTools.AssertEqual(
				source.ServiceSpecificationId.Identifier,
				duplicate.ServiceSpecificationId.Identifier,
				"Duplicated service specification");

			TestTools.Assert(duplicate.GenerateMonitoringService != true, "Duplicated service must not generate a monitoring service.");
			TestTools.Assert(String.IsNullOrEmpty(duplicate.MonitoringService), "Duplicated service must not reference a monitoring service.");
		}

		public override void PerformCleanup(IEngine engine)
		{
			// Cleanup of every fixture is owned by the test data and triggered by the last test case.
		}
	}
}
