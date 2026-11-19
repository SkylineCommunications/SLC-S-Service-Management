namespace SLC_SM_RT_ServiceOrderCompletion.TestCases
{
	using System;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Tests;

	using Models = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;

	/// <summary>
	/// Runs a single service order item state transition and verifies the resulting status.
	/// One instance is added per transition, so every step of the flow is reported separately.
	/// </summary>
	internal sealed class TransitionOrderItemTestCase : TestCase
	{
		private readonly ServiceOrderFlowTestData testData;
		private readonly string previousState;
		private readonly string nextState;
		private readonly string expectedStatus;
		private readonly bool cleanUpAfterwards;

		public TransitionOrderItemTestCase(
			ServiceOrderFlowTestData testData,
			string previousState,
			string nextState,
			string expectedStatus,
			bool cleanUpAfterwards = false)
		{
			this.testData = testData ?? throw new ArgumentNullException(nameof(testData));
			this.previousState = previousState ?? throw new ArgumentNullException(nameof(previousState));
			this.nextState = nextState ?? throw new ArgumentNullException(nameof(nextState));
			this.expectedStatus = expectedStatus ?? throw new ArgumentNullException(nameof(expectedStatus));
			this.cleanUpAfterwards = cleanUpAfterwards;
		}

		/// <inheritdoc/>
		public override string Name => $"OrderItem_{previousState}_To_{nextState}";

		public override void Execute(IEngine engine)
		{
			testData.InvokeOrderItemTransition(previousState, nextState);

			Models.ServiceOrderItem orderItem = testData.ReadOrderItem();

			TestTools.AssertEqual(
				expectedStatus,
				orderItem.Status.ToString(),
				$"Service order item status after '{previousState}' -> '{nextState}'");
		}

		public override void PerformCleanup(IEngine engine)
		{
			if (cleanUpAfterwards)
			{
				testData.CleanUp();
			}
		}
	}
}
