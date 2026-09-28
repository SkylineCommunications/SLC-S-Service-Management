namespace SLC_SM_RT_ServiceSpecificationConfiguration_Edit.TestCases
{
	using System;
	using System.Linq;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Tests;

	using Models = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;

	/// <summary>
	/// Verifies the specification still references the edited configuration.
	/// </summary>
	internal sealed class ValidateSpecificationLinksTestCase : TestCase
	{
		private readonly ServiceSpecificationConfigurationEditTestData testData;

		public ValidateSpecificationLinksTestCase(ServiceSpecificationConfigurationEditTestData testData)
		{
			this.testData = testData ?? throw new ArgumentNullException(nameof(testData));
		}

		public override void Execute(IEngine engine)
		{
			Models.ServiceSpecification specification = testData.ReadSpecification();

			TestTools.Assert(specification != null, "Specification was not found during validation.");

			bool hasConfigurationRef = specification.ConfigurationParameters != null
				&& specification.ConfigurationParameters.Any(x => x.Identifier == testData.SpecificationConfigurationId);

			TestTools.Assert(
				hasConfigurationRef,
				"Specification does not reference the edited configuration.");
		}

		public override void PerformCleanup(IEngine engine)
		{
			// Cleanup of every fixture is owned by the test data and triggered by the last test case.
		}
	}
}
