namespace SLC_SM_RT_ScheduledConfigurationActivation.TestCases
{
	using System;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Tests;

	/// <summary>
	/// Verifies the planned version became the active version, both versions are kept in their order
	/// and neither version's configuration was altered by the activation.
	/// </summary>
	internal sealed class ValidateActivationTestCase : TestCase
	{
		private const string Stage = "Activated";

		private readonly ScheduledConfigurationActivationTestData testData;

		public ValidateActivationTestCase(ScheduledConfigurationActivationTestData testData)
		{
			this.testData = testData ?? throw new ArgumentNullException(nameof(testData));
		}

		public override void Execute(IEngine engine)
		{
			VersionAssertions.AssertServiceVersions(testData, Stage, testData.PlannedVersionId, testData.CurrentVersionId, testData.PlannedVersionId);

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
		}

		public override void PerformCleanup(IEngine engine)
		{
			testData.CleanUp();
		}
	}
}
