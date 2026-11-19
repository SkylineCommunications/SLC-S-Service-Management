namespace SLC_SM_RT_ScheduledConfigurationActivation.TestCases
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	using Skyline.DataMiner.SDM;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Tests;

	using ConfigModels = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.Configurations;
	using Models = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;

	/// <summary>
	/// Assertions shared by the validation test cases.
	/// </summary>
	internal static class VersionAssertions
	{
		private static readonly TimeSpan DateTolerance = TimeSpan.FromSeconds(1);

		/// <summary>
		/// Asserts the service references exactly the expected versions, in the expected order, and the expected active version.
		/// </summary>
		/// <param name="testData">The test data.</param>
		/// <param name="stage">The stage of the test, used in the failure messages.</param>
		/// <param name="expectedActiveVersionId">The identifier of the version that must be active.</param>
		/// <param name="expectedVersionIds">The identifiers of the versions in their expected order.</param>
		public static void AssertServiceVersions(ScheduledConfigurationActivationTestData testData, string stage, string expectedActiveVersionId, params string[] expectedVersionIds)
		{
			Models.Service service = testData.ReadService();
			TestTools.Assert(service != null, $"[{stage}] Service '{testData.ServiceId}' was not found.");

			string activeVersionId = service.ServiceConfigurationId != null ? service.ServiceConfigurationId.Identifier : null;
			TestTools.Assert(
				String.Equals(expectedActiveVersionId, activeVersionId, StringComparison.OrdinalIgnoreCase),
				$"[{stage}] Active configuration version of service '{testData.ServiceId}': expected '{expectedActiveVersionId}', got '{activeVersionId}'.");

			var versionIds = (service.ConfigurationVersions ?? new List<SdmObjectReference<Models.ServiceConfigurationVersion>>())
				.Select(reference => reference.Identifier)
				.ToList();
			TestTools.Assert(
				versionIds.SequenceEqual(expectedVersionIds, StringComparer.OrdinalIgnoreCase),
				$"[{stage}] Configuration versions of service '{testData.ServiceId}': expected [{String.Join(", ", expectedVersionIds)}], got [{String.Join(", ", versionIds)}].");
		}

		/// <summary>
		/// Asserts a configuration version and its single parameter were persisted as expected.
		/// </summary>
		/// <param name="testData">The test data.</param>
		/// <param name="stage">The stage of the test, used in the failure messages.</param>
		/// <param name="versionId">The identifier of the version.</param>
		/// <param name="expectedName">The expected version name.</param>
		/// <param name="expectedStart">The expected start date (UTC).</param>
		/// <param name="expectedEnd">The expected end date (UTC).</param>
		/// <param name="expectedConfigurationValueId">The expected identifier of the configuration item.</param>
		/// <param name="expectedParameterValueId">The expected identifier of the parameter value.</param>
		/// <param name="expectedValue">The expected parameter value.</param>
		public static void AssertVersion(
			ScheduledConfigurationActivationTestData testData,
			string stage,
			string versionId,
			string expectedName,
			DateTime expectedStart,
			DateTime? expectedEnd,
			string expectedConfigurationValueId,
			string expectedParameterValueId,
			string expectedValue)
		{
			Models.ServiceConfigurationVersion version = testData.ReadConfigurationVersion(versionId);
			TestTools.Assert(version != null, $"[{stage}] Configuration version '{versionId}' was not found.");
			TestTools.AssertEqual(expectedName, version.VersionName, $"[{stage}] Name of configuration version '{versionId}'");
			AssertDate(expectedStart, version.StartDate, $"[{stage}] Start date of configuration version '{versionId}'");
			AssertDate(expectedEnd, version.EndDate, $"[{stage}] End date of configuration version '{versionId}'");

			var configurationIds = (version.Parameters ?? new List<SdmObjectReference<Models.ServiceConfigurationValue>>())
				.Select(reference => reference.Identifier)
				.ToList();
			TestTools.Assert(
				configurationIds.Count == 1 && String.Equals(configurationIds[0], expectedConfigurationValueId, StringComparison.OrdinalIgnoreCase),
				$"[{stage}] Configuration items of version '{versionId}': expected ['{expectedConfigurationValueId}'], got [{String.Join(", ", configurationIds)}].");

			Models.ServiceConfigurationValue configurationValue = testData.ReadConfigurationValue(expectedConfigurationValueId);
			TestTools.Assert(configurationValue != null, $"[{stage}] Configuration item '{expectedConfigurationValueId}' was not found.");
			TestTools.AssertEqual(expectedParameterValueId, configurationValue.ConfigurationParameterId.Identifier, $"[{stage}] Parameter value of configuration item '{expectedConfigurationValueId}'");

			ConfigModels.ConfigurationParameterValue value = testData.ReadParameterValue(expectedParameterValueId);
			TestTools.Assert(value != null, $"[{stage}] Parameter value '{expectedParameterValueId}' was not found.");
			TestTools.AssertEqual(expectedValue, value.StringValue, $"[{stage}] Value of parameter value '{expectedParameterValueId}'");
			TestTools.AssertEqual(testData.ConfigurationParameterId, value.ConfigurationParameterId.Identifier, $"[{stage}] Configuration parameter of parameter value '{expectedParameterValueId}'");
		}

		private static void AssertDate(DateTime? expected, DateTime? actual, string subject)
		{
			DateTime? actualUtc = ScheduledConfigurationActivationTestData.ToUtc(actual);
			if (!expected.HasValue)
			{
				TestTools.Assert(!actualUtc.HasValue, $"{subject}: expected no date, got '{actualUtc:O}'.");
				return;
			}

			TestTools.Assert(
				actualUtc.HasValue && (actualUtc.Value - expected.Value).Duration() <= DateTolerance,
				$"{subject}: expected '{expected:O}', got '{(actualUtc.HasValue ? actualUtc.Value.ToString("O") : "<none>")}'.");
		}
	}
}
