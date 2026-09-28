namespace SLC_SM_RT_ServiceOrderItemStateTransition.TestCases
{
	using System;

	using DomHelpers.SlcServicemanagement;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Tests;

	using Models = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;

	/// <summary>
	/// Runs the transition on the Service Order Item and verifies it reaches the expected status.
	/// </summary>
	internal sealed class TransitionOrderItemTestCase : TestCase
	{
		private readonly ServiceOrderItemStateTransitionTestData testData;
		private readonly string previousState;
		private readonly string nextState;
		private readonly SlcServicemanagementIds.Behaviors.Serviceorderitem_Behavior.StatusesEnum expectedStatus;

		public TransitionOrderItemTestCase(
			ServiceOrderItemStateTransitionTestData testData,
			string previousState,
			string nextState,
			SlcServicemanagementIds.Behaviors.Serviceorderitem_Behavior.StatusesEnum expectedStatus)
		{
			this.testData = testData ?? throw new ArgumentNullException(nameof(testData));
			this.previousState = previousState ?? throw new ArgumentNullException(nameof(previousState));
			this.nextState = nextState ?? throw new ArgumentNullException(nameof(nextState));
			this.expectedStatus = expectedStatus;
		}

		/// <inheritdoc/>
		public override string Name => $"OrderItem_{previousState}_To_{nextState}";

		public override void Execute(IEngine engine)
		{
			testData.InvokeStateTransition(previousState, nextState);

			Models.ServiceOrderItem orderItem = testData.ReadOrderItem();

			TestTools.AssertEqual(
				expectedStatus.ToString(),
				orderItem.Status.ToString(),
				$"Order item status after the '{previousState}' -> '{nextState}' transition");
		}

		public override void PerformCleanup(IEngine engine)
		{
			// Cleanup of every fixture is owned by the test data and triggered by the last test case.
		}
	}
}
