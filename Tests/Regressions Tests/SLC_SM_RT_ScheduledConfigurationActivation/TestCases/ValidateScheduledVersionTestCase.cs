namespace SLC_SM_RT_ScheduledConfigurationActivation.TestCases
{
	using System;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Tests;

	/// <summary>
	/// Verifies the future-dated version is stored as a planned version: it is linked to the service with its own
	/// configuration, while the current version stays the active one.
	/// </summary>
	internal sealed class ValidateScheduledVersionTestCase : TestCase
	{
		private const string Stage = "Scheduled";

		private readonly ScheduledConfigurationActivationTestData testData;

		public ValidateScheduledVersionTestCase(ScheduledConfigurationActivationTestData testData)
		{
			this.testData = testData ?? throw new ArgumentNullException(nameof(testData));
		}

		public override void Execute(IEngine engine)
		{
			TestTools.Assert(!String.IsNullOrWhiteSpace(testData.CurrentVersionId), $"[{Stage}] No current configuration version was created on service '{testData.ServiceId}'.");
			TestTools.Assert(!String.IsNullOrWhiteSpace(testData.PlannedVersionId), $"[{Stage}] No planned configuration version was created on service '{testData.ServiceId}'.");
			TestTools.Assert(
				!String.Equals(testData.CurrentVersionId, testData.PlannedVersionId, StringComparison.OrdinalIgnoreCase),
				$"[{Stage}] The planned version re-used the identifier '{testData.CurrentVersionId}' of the current version.");

			VersionAssertions.AssertServiceVersions(testData, Stage, testData.CurrentVersionId, testData.CurrentVersionId, testData.PlannedVersionId);

			TestTools.Assert(
				testData.PlannedStart > DateTime.UtcNow,
				$"[{Stage}] Planned start '{testData.PlannedStart:O}' is no longer in the future; the planned version cannot be validated as scheduled.");
			TestTools.Assert(
				!String.Equals(testData.CurrentConfigurationValueId, testData.PlannedConfigurationValueId, StringComparison.OrdinalIgnoreCase)
				&& !String.Equals(testData.CurrentParameterValueId, testData.PlannedParameterValueId, StringComparison.OrdinalIgnoreCase),
				$"[{Stage}] The planned version shares configuration with the current version (configuration item '{testData.PlannedConfigurationValueId}', parameter value '{testData.PlannedParameterValueId}').");

			VersionAssertions.AssertVersion(
				testData,
				Stage,
				testData.CurrentVersionId,
				testData.CurrentVersionName,
				testData.CurrentStart,
				testData.PlannedStart,
				testData.CurrentConfigurationValueId,
				testData.CurrentParameterValueId,
				ScheduledConfigurationActivationTestData.CurrentValue);
			VersionAssertions.AssertVersion(
				testData,
				Stage,
				testData.PlannedVersionId,
				testData.PlannedVersionName,
				testData.PlannedStart,
				null,
				testData.PlannedConfigurationValueId,
				testData.PlannedParameterValueId,
				ScheduledConfigurationActivationTestData.PlannedValue);
		}

		public override void PerformCleanup(IEngine engine)
		{
			// Cleanup of every fixture is owned by the test data and triggered by the last test case.
		}
	}
}
