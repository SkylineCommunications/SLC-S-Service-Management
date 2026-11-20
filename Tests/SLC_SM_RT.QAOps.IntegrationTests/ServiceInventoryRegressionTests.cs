namespace SLC_SM_RT.QAOps.IntegrationTests
{
	/// <summary>
	/// Service Inventory regression tests: services, configuration versions, lifecycle.
	/// </summary>
	[TestClass]
	[TestCategory("IntegrationTest")]
	[TestCategory("ServiceInventory")]
	public class ServiceInventoryRegressionTests
	{
		[TestMethod]
		public void AddServiceItem_AllTestCasesPass()
		{
			RegressionScriptRunner.RunAndAssert("SLC_SM_RT_AddServiceItem");
		}

		[TestMethod]
		public void AddServiceItemDuplicateLabel_AllTestCasesPass()
		{
			RegressionScriptRunner.RunAndAssert("SLC_SM_RT_AddServiceItemDuplicateLabel");
		}

		[TestMethod]
		public void CreateServiceInventoryItem_AllTestCasesPass()
		{
			RegressionScriptRunner.RunAndAssert("SLC_SM_RT_CreateServiceInventoryItem");
		}

		[TestMethod]
		public void ServiceConfiguration_AllTestCasesPass()
		{
			RegressionScriptRunner.RunAndAssert("SLC_SM_RT_ServiceConfiguration");
		}

		[TestMethod]
		public void ServiceConfiguration_EditDuplicate_AllTestCasesPass()
		{
			RegressionScriptRunner.RunAndAssert("SLC_SM_RT_ServiceConfiguration_EditDuplicate");
		}

		[TestMethod]
		public void ServiceConfigurationItem_AllTestCasesPass()
		{
			RegressionScriptRunner.RunAndAssert("SLC_SM_RT_ServiceConfigurationItem");
		}

		[TestMethod]
		public void ServiceConfigurationItem_EditDuplicate_AllTestCasesPass()
		{
			RegressionScriptRunner.RunAndAssert("SLC_SM_RT_ServiceConfigurationItem_EditDuplicate");
		}

		[TestMethod]
		public void ServiceStateTransition_AllTestCasesPass()
		{
			RegressionScriptRunner.RunAndAssert("SLC_SM_RT_ServiceStateTransition");
		}

		[TestMethod]
		public void InvalidServiceTransition_AllTestCasesPass()
		{
			RegressionScriptRunner.RunAndAssert("SLC_SM_RT_InvalidServiceTransition");
		}

		[TestMethod]
		public void ScheduledConfigurationActivation_AllTestCasesPass()
		{
			RegressionScriptRunner.RunAndAssert("SLC_SM_RT_ScheduledConfigurationActivation");
		}

		[TestMethod]
		public void ServiceUpdate_AllTestCasesPass()
		{
			RegressionScriptRunner.RunAndAssert("SLC_SM_RT_ServiceUpdate");
		}
	}
}
