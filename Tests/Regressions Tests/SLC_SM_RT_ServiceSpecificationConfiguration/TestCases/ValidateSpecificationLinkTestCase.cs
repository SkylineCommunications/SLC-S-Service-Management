namespace SLC_SM_RT_ServiceSpecificationConfiguration.TestCases
{
	using System;
	using System.Linq;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Tests;

	using Models = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;

	/// <summary>
	/// Verifies the specification keeps a reference to the created configuration.
	/// </summary>
	internal sealed class ValidateSpecificationLinkTestCase : TestCase
	{
		private readonly ServiceSpecificationConfigurationTestData testData;

		public ValidateSpecificationLinkTestCase(ServiceSpecificationConfigurationTestData testData)
		{
			this.testData = testData ?? throw new ArgumentNullException(nameof(testData));
		}

		public override void Execute(IEngine engine)
		{
			Models.ServiceSpecification persisted = testData.ReadSpecification();

			TestTools.Assert(persisted != null, "Specification was not found after linking configuration.");

			bool hasConfigurationRef = persisted.ConfigurationParameters != null
				&& persisted.ConfigurationParameters.Any(x => x.Identifier == testData.SpecificationConfigurationId);

			TestTools.Assert(hasConfigurationRef, "Specification does not contain the expected configuration reference.");
		}

		public override void PerformCleanup(IEngine engine)
		{
			// Cleanup of every fixture is owned by the test data and triggered by the last test case.
		}
	}
}
