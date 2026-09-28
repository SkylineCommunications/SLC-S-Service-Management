namespace SLC_SM_RT_InvalidServiceTransition.TestCases
{
	using System;

	using DomHelpers.SlcServicemanagement;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Tests;

	using Models = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;

	/// <summary>
	/// Asks the state transition script for a transition that does not exist and verifies the service
	/// is left untouched. "New" to "Active" is not part of the supported Service transitions, so the
	/// script is expected to reject it rather than to move the service.
	/// </summary>
	internal sealed class RejectInvalidTransitionTestCase : TestCase
	{
		private const string FromState = "New";
		private const string ToState = "Active";

		private readonly InvalidServiceTransitionTestData testData;

		public RejectInvalidTransitionTestCase(InvalidServiceTransitionTestData testData)
		{
			this.testData = testData ?? throw new ArgumentNullException(nameof(testData));
		}

		public override void Execute(IEngine engine)
		{
			Models.Service serviceBefore = testData.ReadService();

			TestTools.AssertEqual(
				SlcServicemanagementIds.Behaviors.Service_Behavior.StatusesEnum.New.ToString(),
				serviceBefore.Status.ToString(),
				"Service status before the invalid transition");

			bool reportedError = testData.TryInvokeStateTransition(FromState, ToState);

			engine.GenerateInformation(
				$"RT_InvalidServiceTransition: the script {(reportedError ? "reported an error" : "did not report an error")} for '{FromState}' -> '{ToState}'.");

			Models.Service serviceAfter = testData.ReadService();

			TestTools.AssertEqual(
				SlcServicemanagementIds.Behaviors.Service_Behavior.StatusesEnum.New.ToString(),
				serviceAfter.Status.ToString(),
				$"Service status after the unsupported transition '{FromState}' -> '{ToState}'");
		}

		public override void PerformCleanup(IEngine engine)
		{
			testData.CleanUp();
		}
	}
}
