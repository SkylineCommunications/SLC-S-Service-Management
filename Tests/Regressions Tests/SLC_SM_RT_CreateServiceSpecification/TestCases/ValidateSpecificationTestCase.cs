namespace SLC_SM_RT_CreateServiceSpecification.TestCases
{
	using System;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Tests;

	using Models = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;

	/// <summary>
	/// Verifies the created Service Specification was persisted and kept its name.
	/// </summary>
	internal sealed class ValidateSpecificationTestCase : TestCase
	{
		private readonly CreateServiceSpecificationTestData testData;

		public ValidateSpecificationTestCase(CreateServiceSpecificationTestData testData)
		{
			this.testData = testData ?? throw new ArgumentNullException(nameof(testData));
		}

		public override void Execute(IEngine engine)
		{
			Models.ServiceSpecification persisted = testData.ReadSpecification();

			TestTools.Assert(persisted != null, "Created service specification was not found.");
			TestTools.AssertEqual(testData.ExpectedName, persisted.Name, "Service specification name");
		}

		public override void PerformCleanup(IEngine engine)
		{
			testData.CleanUp();
		}
	}
}
