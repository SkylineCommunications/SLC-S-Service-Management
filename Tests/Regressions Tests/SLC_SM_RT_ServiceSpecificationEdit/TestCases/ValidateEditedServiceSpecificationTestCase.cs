namespace SLC_SM_RT_ServiceSpecificationEdit.TestCases
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	using DomHelpers.SlcServicemanagement;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.SDM;
	using Skyline.DataMiner.Utils.ServiceManagement.Testing.Tests;

	using ConfigModels = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.Configurations;
	using Models = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;

	/// <summary>
	/// Verifies the edited metadata and the added service item were persisted, and the existing relationships and configuration were kept.
	/// This is the last test case: its cleanup removes every fixture.
	/// </summary>
	internal sealed class ValidateEditedServiceSpecificationTestCase : TestCase
	{
		private readonly ServiceSpecificationEditTestData testData;

		public ValidateEditedServiceSpecificationTestCase(ServiceSpecificationEditTestData testData)
		{
			this.testData = testData ?? throw new ArgumentNullException(nameof(testData));
		}

		public override void Execute(IEngine engine)
		{
			Models.ServiceSpecification specification = testData.ReadSpecification(testData.SpecificationId);
			TestTools.Assert(specification != null, $"Service Specification '{testData.SpecificationId}' was not found after the edit.");

			TestTools.AssertEqual(testData.EditedName, specification.Name, $"Name of specification '{testData.SpecificationId}'");
			TestTools.AssertEqual(testData.EditedDescription, specification.Description, $"Description of specification '{testData.SpecificationId}'");
			TestTools.AssertEqual(testData.EditedIcon, specification.Icon, $"Icon of specification '{testData.SpecificationId}'");

			var items = specification.ServiceItems ?? new List<Models.ServiceItem>();
			Models.ServiceItem addedItem = items.FirstOrDefault(item => String.Equals(item.DefinitionReference, testData.DefinitionReference, StringComparison.Ordinal));
			TestTools.Assert(
				addedItem != null,
				$"Specification '{testData.SpecificationId}' has no service item with definition reference '{testData.DefinitionReference}'. Items: [{String.Join(", ", items.Select(item => item.DefinitionReference ?? "<null>"))}].");
			TestTools.AssertEqual(
				SlcServicemanagementIds.Enums.ServiceItemTypesEnum.Service.ToString(),
				addedItem.Type?.ToString(),
				$"Type of service item '{testData.DefinitionReference}'");
			TestTools.AssertEqual(testData.DefinitionReference, addedItem.Label, $"Label of service item '{testData.DefinitionReference}'");
			TestTools.Assert(addedItem.ServiceItemID.HasValue, $"Service item '{testData.DefinitionReference}' did not get a service item ID.");
			TestTools.Assert(
				(specification.ServiceItemsRelationships ?? new List<Models.ServiceItemRelationship>()).Any(relationship => String.Equals(relationship.Id, testData.RelationshipId, StringComparison.OrdinalIgnoreCase)),
				$"Specification '{testData.SpecificationId}' lost its existing service item relationship '{testData.RelationshipId}' after the edit. Relationships: [{String.Join(", ", (specification.ServiceItemsRelationships ?? new List<Models.ServiceItemRelationship>()).Select(relationship => relationship.Id ?? "<null>"))}].");

			var configurationIds = (specification.ConfigurationParameters ?? new List<SdmObjectReference<Models.ServiceSpecificationConfigurationValue>>())
				.Select(reference => reference.Identifier)
				.ToList();
			TestTools.Assert(
				configurationIds.Count == 1 && String.Equals(configurationIds[0], testData.SpecificationConfigurationValueId, StringComparison.OrdinalIgnoreCase),
				$"The edit must keep the configuration of specification '{testData.SpecificationId}'. Expected ['{testData.SpecificationConfigurationValueId}'], got [{String.Join(", ", configurationIds)}].");

			Models.ServiceSpecificationConfigurationValue configuration = testData.ReadSpecificationConfigurationValue(testData.SpecificationConfigurationValueId);
			TestTools.Assert(configuration != null, $"Specification configuration item '{testData.SpecificationConfigurationValueId}' was not found after the edit.");
			TestTools.AssertEqual(
				testData.ParameterValueId,
				configuration.ConfigurationParameterId.Identifier,
				$"Configuration parameter value referenced by specification configuration item '{testData.SpecificationConfigurationValueId}'");
			TestTools.AssertEqual(Boolean.TrueString, configuration.MandatoryAtService.ToString(), $"MandatoryAtService of specification configuration item '{testData.SpecificationConfigurationValueId}'");
			TestTools.AssertEqual(Boolean.FalseString, configuration.MandatoryAtServiceOrder.ToString(), $"MandatoryAtServiceOrder of specification configuration item '{testData.SpecificationConfigurationValueId}'");
			TestTools.AssertEqual(Boolean.TrueString, configuration.ExposeAtServiceOrder.ToString(), $"ExposeAtServiceOrder of specification configuration item '{testData.SpecificationConfigurationValueId}'");

			ConfigModels.ConfigurationParameterValue parameterValue = testData.ReadParameterValue(testData.ParameterValueId);
			TestTools.Assert(parameterValue != null, $"Configuration parameter value '{testData.ParameterValueId}' was not found after the edit.");
			TestTools.AssertEqual(testData.ExpectedSourceValue, parameterValue.StringValue, $"Value of configuration parameter value '{testData.ParameterValueId}'");
			TestTools.AssertEqual(
				testData.ConfigurationParameterId,
				parameterValue.ConfigurationParameterId.Identifier,
				$"Configuration parameter of configuration parameter value '{testData.ParameterValueId}'");
		}

		public override void PerformCleanup(IEngine engine)
		{
			testData.CleanUp();
		}
	}
}
