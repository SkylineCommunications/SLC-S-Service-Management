namespace SLC_SM_RT_ServiceProfiles.TestCases
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.SDM;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Tests;

	using ConfigModels = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.Configurations;
	using Models = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;

	/// <summary>
	/// Verifies the specification profile, the profile, its parameter value and the standalone parameter were persisted
	/// and that the identifiers recorded after creation are still the ones linked to the specification.
	/// </summary>
	internal sealed class ValidateProfileTestCase : TestCase
	{
		private readonly ServiceProfilesTestData testData;
		private readonly string stage;
		private readonly string expectedProfileValue;
		private readonly string expectedStandaloneValue;
		private readonly bool expectExposedAtServiceOrder;

		public ValidateProfileTestCase(ServiceProfilesTestData testData, string stage, string expectedProfileValue, string expectedStandaloneValue, bool expectExposedAtServiceOrder)
		{
			this.testData = testData ?? throw new ArgumentNullException(nameof(testData));
			this.stage = stage ?? throw new ArgumentNullException(nameof(stage));
			this.expectedProfileValue = expectedProfileValue;
			this.expectedStandaloneValue = expectedStandaloneValue;
			this.expectExposedAtServiceOrder = expectExposedAtServiceOrder;
		}

		public override string Name => $"{nameof(ValidateProfileTestCase)}_{stage}";

		public override void Execute(IEngine engine)
		{
			Models.ServiceSpecification specification = testData.ReadSpecification();
			TestTools.Assert(specification != null, $"[{stage}] Specification '{testData.SpecificationId}' was not found.");

			ValidateStandaloneParameter(specification);
			ValidateProfile(specification);
		}

		public override void PerformCleanup(IEngine engine)
		{
			// Cleanup of every fixture is owned by the test data and triggered by the last test case.
		}

		private void ValidateStandaloneParameter(Models.ServiceSpecification specification)
		{
			var configurationIds = (specification.ConfigurationParameters ?? new List<SdmObjectReference<Models.ServiceSpecificationConfigurationValue>>())
				.Select(reference => reference.Identifier)
				.ToList();
			TestTools.Assert(
				configurationIds.Count == 1 && String.Equals(configurationIds[0], testData.SpecificationConfigurationValueId, StringComparison.OrdinalIgnoreCase),
				$"[{stage}] Specification '{testData.SpecificationId}' configuration items: expected ['{testData.SpecificationConfigurationValueId}'], got [{String.Join(", ", configurationIds)}].");

			Models.ServiceSpecificationConfigurationValue configurationValue = testData.ReadSpecificationConfigurationValue(testData.SpecificationConfigurationValueId);
			TestTools.Assert(configurationValue != null, $"[{stage}] Specification configuration item '{testData.SpecificationConfigurationValueId}' was not found.");
			TestTools.Assert(configurationValue.MandatoryAtService == true, $"[{stage}] Specification configuration item '{testData.SpecificationConfigurationValueId}' is not mandatory at service.");
			TestTools.AssertEqual(testData.StandaloneParameterValueId, configurationValue.ConfigurationParameterId.Identifier, $"[{stage}] Parameter value linked to configuration item '{testData.SpecificationConfigurationValueId}'");

			ConfigModels.ConfigurationParameterValue value = testData.ReadParameterValue(testData.StandaloneParameterValueId);
			TestTools.Assert(value != null, $"[{stage}] Standalone parameter value '{testData.StandaloneParameterValueId}' was not found.");
			TestTools.AssertEqual(expectedStandaloneValue, value.StringValue, $"[{stage}] Standalone parameter value '{testData.StandaloneParameterValueId}'");
			TestTools.AssertEqual(testData.StandaloneValueLabel, value.Label, $"[{stage}] Standalone parameter value label");
			TestTools.AssertEqual(testData.ConfigurationParameterId, value.ConfigurationParameterId.Identifier, $"[{stage}] Configuration parameter of standalone value '{testData.StandaloneParameterValueId}'");
		}

		private void ValidateProfile(Models.ServiceSpecification specification)
		{
			var specificationProfileIds = (specification.ConfigurationProfiles ?? new List<SdmObjectReference<Models.ServiceSpecificationProfile>>())
				.Select(reference => reference.Identifier)
				.ToList();
			TestTools.Assert(
				specificationProfileIds.Count == 1 && String.Equals(specificationProfileIds[0], testData.SpecificationProfileId, StringComparison.OrdinalIgnoreCase),
				$"[{stage}] Specification '{testData.SpecificationId}' profiles: expected ['{testData.SpecificationProfileId}'], got [{String.Join(", ", specificationProfileIds)}].");

			Models.ServiceSpecificationProfile specificationProfile = testData.ReadSpecificationProfile(testData.SpecificationProfileId);
			TestTools.Assert(specificationProfile != null, $"[{stage}] Specification profile '{testData.SpecificationProfileId}' was not found.");
			TestTools.AssertEqual(testData.ProfileDefinitionId, specificationProfile.ProfileDefinitionId.Identifier, $"[{stage}] Profile definition of specification profile '{testData.SpecificationProfileId}'");
			TestTools.AssertEqual(testData.ProfileId, specificationProfile.ProfileId.Identifier, $"[{stage}] Profile of specification profile '{testData.SpecificationProfileId}'");
			TestTools.Assert(specificationProfile.MandatoryAtService == true, $"[{stage}] Specification profile '{testData.SpecificationProfileId}' is not mandatory at service.");
			TestTools.Assert(specificationProfile.MandatoryAtServiceOrder != true, $"[{stage}] Specification profile '{testData.SpecificationProfileId}' is unexpectedly mandatory at service order.");
			TestTools.Assert(
				(specificationProfile.ExposeAtServiceOrder == true) == expectExposedAtServiceOrder,
				$"[{stage}] Specification profile '{testData.SpecificationProfileId}' ExposeAtServiceOrder: expected {expectExposedAtServiceOrder}, got {specificationProfile.ExposeAtServiceOrder}.");

			ConfigModels.Profile profile = testData.ReadProfile(testData.ProfileId);
			TestTools.Assert(profile != null, $"[{stage}] Profile '{testData.ProfileId}' was not found.");
			TestTools.AssertEqual(testData.ProfileName, profile.Name, $"[{stage}] Name of profile '{testData.ProfileId}'");
			TestTools.AssertEqual(testData.ProfileDefinitionId, profile.ProfileDefinitionId.Identifier, $"[{stage}] Profile definition of profile '{testData.ProfileId}'");
			TestTools.Assert(profile.IsReusable != true, $"[{stage}] Profile '{testData.ProfileId}' is reusable, expected a non-reusable profile.");

			var valueIds = (profile.ConfigurationParameterValues ?? new List<SdmObjectReference<ConfigModels.ConfigurationParameterValue>>())
				.Select(reference => reference.Identifier)
				.ToList();
			TestTools.Assert(
				valueIds.Count == 1 && String.Equals(valueIds[0], testData.ProfileParameterValueId, StringComparison.OrdinalIgnoreCase),
				$"[{stage}] Profile '{testData.ProfileId}' values: expected ['{testData.ProfileParameterValueId}'], got [{String.Join(", ", valueIds)}].");

			ConfigModels.ConfigurationParameterValue value = testData.ReadParameterValue(testData.ProfileParameterValueId);
			TestTools.Assert(value != null, $"[{stage}] Profile parameter value '{testData.ProfileParameterValueId}' was not found.");
			TestTools.AssertEqual(expectedProfileValue, value.StringValue, $"[{stage}] Profile parameter value '{testData.ProfileParameterValueId}'");
			TestTools.AssertEqual(testData.ProfileValueLabel, value.Label, $"[{stage}] Profile parameter value label");
			TestTools.AssertEqual(testData.ConfigurationParameterId, value.ConfigurationParameterId.Identifier, $"[{stage}] Configuration parameter of profile value '{testData.ProfileParameterValueId}'");
		}
	}
}
