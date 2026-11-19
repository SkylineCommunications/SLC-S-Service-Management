namespace SLC_SM_RT_ServiceProfiles.TestCases
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.SDM;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Tests;

	using Models = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;

	/// <summary>
	/// Verifies the profile was removed from the specification together with its parameter value,
	/// while the standalone parameter is untouched.
	/// </summary>
	internal sealed class ValidateProfileDeletedTestCase : TestCase
	{
		private readonly ServiceProfilesTestData testData;

		public ValidateProfileDeletedTestCase(ServiceProfilesTestData testData)
		{
			this.testData = testData ?? throw new ArgumentNullException(nameof(testData));
		}

		public override void Execute(IEngine engine)
		{
			Models.ServiceSpecification specification = testData.ReadSpecification();
			TestTools.Assert(specification != null, $"Specification '{testData.SpecificationId}' was not found after deleting its profile.");

			var specificationProfileIds = (specification.ConfigurationProfiles ?? new List<SdmObjectReference<Models.ServiceSpecificationProfile>>())
				.Select(reference => reference.Identifier)
				.ToList();
			TestTools.Assert(
				specificationProfileIds.Count == 0,
				$"Specification '{testData.SpecificationId}' still references profiles after deletion: [{String.Join(", ", specificationProfileIds)}].");
			TestTools.Assert(
				testData.ReadSpecificationProfile(testData.SpecificationProfileId) == null,
				$"Specification profile '{testData.SpecificationProfileId}' still exists after deletion.");
			TestTools.Assert(
				testData.ReadParameterValue(testData.ProfileParameterValueId) == null,
				$"Profile parameter value '{testData.ProfileParameterValueId}' still exists after deletion.");

			var configurationIds = (specification.ConfigurationParameters ?? new List<SdmObjectReference<Models.ServiceSpecificationConfigurationValue>>())
				.Select(reference => reference.Identifier)
				.ToList();
			TestTools.Assert(
				configurationIds.Count == 1 && String.Equals(configurationIds[0], testData.SpecificationConfigurationValueId, StringComparison.OrdinalIgnoreCase),
				$"Specification '{testData.SpecificationId}' configuration items after deleting the profile: expected ['{testData.SpecificationConfigurationValueId}'], got [{String.Join(", ", configurationIds)}].");
			TestTools.AssertEqual(
				ServiceProfilesTestData.EditedStandaloneValue,
				testData.ReadParameterValue(testData.StandaloneParameterValueId)?.StringValue,
				$"Standalone parameter value '{testData.StandaloneParameterValueId}' after deleting the profile");

			if (testData.ReadProfile(testData.ProfileId) != null)
			{
				// Known product behaviour: removing a profile from a specification only deletes the specification profile link.
				engine.GenerateInformation($"RT_ServiceProfiles: profile '{testData.ProfileId}' is left behind after its specification profile was deleted; it is removed by the test cleanup.");
			}
		}

		public override void PerformCleanup(IEngine engine)
		{
			testData.CleanUp();
		}
	}
}
