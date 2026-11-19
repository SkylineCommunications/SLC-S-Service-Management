namespace SLC_SM_RT.QAOps.IntegrationTests
{
	/// <summary>
	/// Service Ordering regression tests: orders, order items, lifecycle.
	/// </summary>
	[TestClass]
	[TestCategory("IntegrationTest")]
	[TestCategory("ServiceOrder")]
	public class ServiceOrderRegressionTests
	{
		[TestMethod]
		public void CreateServiceOrder_AllTestCasesPass()
		{
			RegressionScriptRunner.RunAndAssert("SLC_SM_RT_CreateServiceOrder");
		}

		[TestMethod]
		public void ServiceOrderItem_AllTestCasesPass()
		{
			RegressionScriptRunner.RunAndAssert("SLC_SM_RT_ServiceOrderItem");
		}

		[TestMethod]
		public void ServiceOrderItem_Edit_AllTestCasesPass()
		{
			RegressionScriptRunner.RunAndAssert("SLC_SM_RT_ServiceOrderItem_Edit");
		}

		[TestMethod]
		public void ServiceOrderItemConfiguration_AllTestCasesPass()
		{
			RegressionScriptRunner.RunAndAssert("SLC_SM_RT_ServiceOrderItemConfiguration");
		}

		[TestMethod]
		public void ServiceOrderItemConfiguration_Edit_AllTestCasesPass()
		{
			RegressionScriptRunner.RunAndAssert("SLC_SM_RT_ServiceOrderItemConfiguration_Edit");
		}

		[TestMethod]
		public void ServiceOrderItemStateTransition_AllTestCasesPass()
		{
			RegressionScriptRunner.RunAndAssert("SLC_SM_RT_ServiceOrderItemStateTransition");
		}

		[TestMethod]
		public void ServiceOrderItemLifecycle_AllTestCasesPass()
		{
			RegressionScriptRunner.RunAndAssert("SLC_SM_RT_ServiceOrderItemLifecycle");
		}

		[TestMethod]
		public void ServiceOrderItemCancellationFlow_AllTestCasesPass()
		{
			RegressionScriptRunner.RunAndAssert("SLC_SM_RT_ServiceOrderItemCancellationFlow");
		}

		[TestMethod]
		public void ServiceOrderCompletion_AllTestCasesPass()
		{
			RegressionScriptRunner.RunAndAssert("SLC_SM_RT_ServiceOrderCompletion");
		}

		[TestMethod]
		public void ServiceOrderAcknowledgeReject_AllTestCasesPass()
		{
			RegressionScriptRunner.RunAndAssert("SLC_SM_RT_ServiceOrderAcknowledgeReject");
		}

		[TestMethod]
		public void ServiceOrderItemRejection_AllTestCasesPass()
		{
			RegressionScriptRunner.RunAndAssert("SLC_SM_RT_ServiceOrderItemRejection");
		}

		[TestMethod]
		public void ServiceOrderToServiceLink_AllTestCasesPass()
		{
			RegressionScriptRunner.RunAndAssert("SLC_SM_RT_ServiceOrderToServiceLink");
		}

		[TestMethod]
		public void ServiceOrderEdit_AllTestCasesPass()
		{
			RegressionScriptRunner.RunAndAssert("SLC_SM_RT_ServiceOrderEdit");
		}
	}
}
