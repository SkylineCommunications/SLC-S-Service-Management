namespace SLC_SM_RT.QAOps.IntegrationTests
{
	/// <summary>
	/// Service Catalog regression tests: specifications, configurations, deletes.
	/// </summary>
	[TestClass]
	[TestCategory("IntegrationTest")]
	[TestCategory("ServiceCatalog")]
	public class ServiceCatalogRegressionTests
	{
		[TestMethod]
		public void CreateServiceSpecification_AllTestCasesPass()
		{
			RegressionScriptRunner.RunAndAssert("SLC_SM_RT_CreateServiceSpecification");
		}

		[TestMethod]
		public void DynamicDeleteSpec_AllTestCasesPass()
		{
			RegressionScriptRunner.RunAndAssert("SLC_SM_RT_DynamicDeleteSpec");
		}

		[TestMethod]
		public void DeleteServiceCategory_AllTestCasesPass()
		{
			RegressionScriptRunner.RunAndAssert("SLC_SM_RT_DeleteServiceCategory");
		}

		[TestMethod]
		public void DynamicDeleteService_AllTestCasesPass()
		{
			RegressionScriptRunner.RunAndAssert("SLC_SM_RT_DynamicDeleteService");
		}

		[TestMethod]
		public void ServiceSpecificationConfiguration_AllTestCasesPass()
		{
			RegressionScriptRunner.RunAndAssert("SLC_SM_RT_ServiceSpecificationConfiguration");
		}

		[TestMethod]
		public void ServiceSpecificationConfiguration_Edit_AllTestCasesPass()
		{
			RegressionScriptRunner.RunAndAssert("SLC_SM_RT_ServiceSpecificationConfiguration_Edit");
		}

		[TestMethod]
		public void ServiceProfiles_AllTestCasesPass()
		{
			RegressionScriptRunner.RunAndAssert("SLC_SM_RT_ServiceProfiles");
		}

		[TestMethod]
		public void ServiceSpecificationEdit_AllTestCasesPass()
		{
			RegressionScriptRunner.RunAndAssert("SLC_SM_RT_ServiceSpecificationEdit");
		}
	}
}
