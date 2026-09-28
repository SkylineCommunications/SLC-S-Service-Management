namespace SLC_SM_RT_ServiceStateTransition.TestCases
{
	using System;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Tests;

	using Models = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;

	/// <summary>
	/// Runs one service state transition and verifies the resulting status.
	/// One instance is added per step of the lifecycle, so every step is reported separately.
	/// </summary>
	internal sealed class TransitionServiceTestCase : TestCase
	{
		private readonly ServiceStateTransitionTestData testData;
		private readonly string previousState;
		private readonly string nextState;
		private readonly string expectedStatus;
		private readonly bool cleanUpAfterwards;

		public TransitionServiceTestCase(
			ServiceStateTransitionTestData testData,
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
		public override string Name => $"Service_{previousState}_To_{nextState}";

		public override void Execute(IEngine engine)
		{
			Models.Service serviceBefore = testData.ReadService();

			TestTools.AssertEqual(
				previousState,
				serviceBefore.Status.ToString(),
				$"Service status before the '{previousState}' -> '{nextState}' transition");

			testData.InvokeStateTransition(previousState, nextState);

			Models.Service serviceAfter = testData.ReadService();

			TestTools.AssertEqual(
				expectedStatus,
				serviceAfter.Status.ToString(),
				$"Service status after the '{previousState}' -> '{nextState}' transition");
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
