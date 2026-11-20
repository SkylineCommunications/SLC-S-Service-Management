namespace SLC_SM_RT_ServiceUpdate.TestCases
{
	using System;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Tests;

	using Models = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;

	/// <summary>
	/// Brings the service through its lifecycle to the Active state and records the baseline the edits are compared with.
	/// </summary>
	internal sealed class ActivateServiceTestCase : TestCase
	{
		private static readonly string[] Lifecycle = { "New", "Designed", "Reserved", "Active" };

		private readonly ServiceUpdateTestData testData;

		public ActivateServiceTestCase(ServiceUpdateTestData testData)
		{
			this.testData = testData ?? throw new ArgumentNullException(nameof(testData));
		}

		public override void Execute(IEngine engine)
		{
			for (int i = 1; i < Lifecycle.Length; i++)
			{
				Models.Service before = testData.ReadService();
				TestTools.Assert(before != null, $"Service '{testData.ServiceId}' was not found before the '{Lifecycle[i - 1]}' -> '{Lifecycle[i]}' transition.");
				TestTools.AssertEqual(Lifecycle[i - 1], before.Status.ToString(), $"Status of service '{testData.ServiceId}' before the '{Lifecycle[i - 1]}' -> '{Lifecycle[i]}' transition");

				testData.InvokeStateTransition(Lifecycle[i - 1], Lifecycle[i]);
			}

			Models.Service service = testData.ReadService();
			TestTools.Assert(service != null, $"Service '{testData.ServiceId}' was not found after activation.");
			TestTools.AssertEqual("Active", service.Status.ToString(), $"Status of service '{testData.ServiceId}' after activation");

			testData.TakeBaseline();
			TestTools.AssertEqual(testData.SpecificationId, testData.Baseline.SpecificationId, $"Specification of service '{testData.ServiceId}' before the edit");
			TestTools.AssertEqual(testData.ServiceConfigurationVersionId, testData.Baseline.ActiveVersionId, $"Active configuration version of service '{testData.ServiceId}' before the edit");
		}

		public override void PerformCleanup(IEngine engine)
		{
			// Cleanup of every fixture is owned by the test data and triggered by the last test case.
		}
	}
}
