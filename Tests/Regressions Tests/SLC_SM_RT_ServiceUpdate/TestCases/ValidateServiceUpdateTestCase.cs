namespace SLC_SM_RT_ServiceUpdate.TestCases
{
	using System;
	using System.Linq;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Tests;

	using Models = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;

	/// <summary>
	/// Verifies the edited fields persisted while the identity, specification link, lifecycle state,
	/// configuration and service items of the service were kept.
	/// </summary>
	internal sealed class ValidateServiceUpdateTestCase : TestCase
	{
		private static readonly TimeSpan DateTolerance = TimeSpan.FromSeconds(1);

		private readonly ServiceUpdateTestData testData;
		private readonly string stage;
		private readonly bool cleanUpAfterwards;

		public ValidateServiceUpdateTestCase(ServiceUpdateTestData testData, string stage, bool cleanUpAfterwards = false)
		{
			this.testData = testData ?? throw new ArgumentNullException(nameof(testData));
			this.stage = stage ?? throw new ArgumentNullException(nameof(stage));
			this.cleanUpAfterwards = cleanUpAfterwards;
		}

		/// <inheritdoc/>
		public override string Name => $"{nameof(ValidateServiceUpdateTestCase)}_{stage}";

		public override void Execute(IEngine engine)
		{
			ServiceSnapshot baseline = testData.Baseline;
			TestTools.Assert(baseline != null, $"[{stage}] No baseline of service '{testData.ServiceId}' was recorded before the edit.");

			Models.Service service = testData.ReadService();
			TestTools.Assert(service != null, $"[{stage}] Service '{testData.ServiceId}' was not found after the edit.");

			// Edited fields.
			TestTools.AssertEqual(testData.ExpectedServiceIdentifier, service.ServiceID, $"[{stage}] Service ID of service '{testData.ServiceId}'");
			TestTools.AssertEqual(testData.ExpectedName, service.Name, $"[{stage}] Name of service '{testData.ServiceId}'");
			AssertDate(testData.ExpectedStartTime, service.StartTime, $"[{stage}] Start time of service '{testData.ServiceId}'");
			AssertDate(testData.ExpectedEndTime, service.EndTime, $"[{stage}] End time of service '{testData.ServiceId}'");
			TestTools.AssertEqual(testData.CategoryId, service.CategoryId != null ? service.CategoryId.Identifier : null, $"[{stage}] Category of service '{testData.ServiceId}'");
			TestTools.AssertEqual(testData.CategoryIcon, service.Icon, $"[{stage}] Icon of service '{testData.ServiceId}' (taken from its category)");

			// Kept fields.
			TestTools.AssertEqual(baseline.Identifier, service.Identifier, $"[{stage}] Identifier of the edited service");
			TestTools.AssertEqual(baseline.Status, service.Status.ToString(), $"[{stage}] Lifecycle state of service '{testData.ServiceId}'");
			TestTools.AssertEqual(baseline.Description, service.Description ?? String.Empty, $"[{stage}] Description of service '{testData.ServiceId}'");
			TestTools.AssertEqual(baseline.SpecificationId, service.ServiceSpecificationId != null ? service.ServiceSpecificationId.Identifier : null, $"[{stage}] Specification of service '{testData.ServiceId}'");
			TestTools.AssertEqual(baseline.ActiveVersionId, service.ServiceConfigurationId != null ? service.ServiceConfigurationId.Identifier : null, $"[{stage}] Active configuration version of service '{testData.ServiceId}'");

			var current = new ServiceSnapshot(service);
			TestTools.Assert(
				current.VersionIds.SequenceEqual(baseline.VersionIds, StringComparer.OrdinalIgnoreCase),
				$"[{stage}] Configuration versions of service '{testData.ServiceId}': expected [{String.Join(", ", baseline.VersionIds)}], got [{String.Join(", ", current.VersionIds)}].");
			TestTools.Assert(
				current.Items.SequenceEqual(baseline.Items, StringComparer.Ordinal),
				$"[{stage}] Service items of service '{testData.ServiceId}': expected [{String.Join("; ", baseline.Items)}], got [{String.Join("; ", current.Items)}].");
			TestTools.Assert(
				current.Relationships.SequenceEqual(baseline.Relationships, StringComparer.Ordinal),
				$"[{stage}] Service item relationships of service '{testData.ServiceId}': expected [{String.Join("; ", baseline.Relationships)}], got [{String.Join("; ", current.Relationships)}].");
		}

		public override void PerformCleanup(IEngine engine)
		{
			if (cleanUpAfterwards)
			{
				testData.CleanUp();
			}
		}

		private static void AssertDate(DateTime? expected, DateTime? actual, string subject)
		{
			DateTime? actualUtc = ServiceUpdateTestData.ToUtc(actual);
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
