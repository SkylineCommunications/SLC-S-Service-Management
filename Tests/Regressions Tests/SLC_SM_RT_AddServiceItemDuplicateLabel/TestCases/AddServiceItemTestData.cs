namespace SLC_SM_RT_AddServiceItemDuplicateLabel.TestCases
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ApiHelpers;

	using Models = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;

	/// <summary>
	/// Owns the fixture of the test and the state shared between its test cases.
	/// </summary>
	internal sealed class AddServiceItemTestData
	{
		private readonly IEngine engine;

		public AddServiceItemTestData(IEngine engine)
		{
			this.engine = engine ?? throw new ArgumentNullException(nameof(engine));
			Api = engine.GetUserConnection().GetServiceManagementApiHelper("Service Catalog");
			DefinitionReference = $"RT-DUP-{Guid.NewGuid()}";
		}

		public IServiceManagementApiHelper Api { get; }

		/// <summary>
		/// Gets the definition reference both added service items share. This is what triggers the duplicate label.
		/// </summary>
		public string DefinitionReference { get; }

		public string SpecificationId { get; private set; } = String.Empty;

		public void CreateFixtures()
		{
			var specification = new Models.ServiceSpecification
			{
				Identifier = Guid.NewGuid().ToString(),
				Name = $"RT_AddServiceItem_Duplicate_{Guid.NewGuid()}",
				Description = "Fixture for duplicate-label regression test.",
				ServiceItems = new List<Models.ServiceItem> { new Models.ServiceItem() },
				ServiceItemsRelationships = new List<Models.ServiceItemRelationship> { new Models.ServiceItemRelationship() },
			};

			Api.ServiceCatalog.ServiceSpecifications.Create(specification);
			SpecificationId = specification.Identifier;
		}

		/// <summary>
		/// Runs the script under test once for the fixture, using the shared definition reference.
		/// </summary>
		public void InvokeAddServiceItem()
		{
			SubScriptOptions subScript = engine.PrepareSubScript(Script.ScriptUnderTest);
			subScript.Synchronous = true;
			subScript.ExtendedErrorInfo = true;
			subScript.SelectScriptParam("DOM ID", SpecificationId);
			subScript.SelectScriptParam("ServiceItemType", "Service");
			subScript.SelectScriptParam("DefinitionReference", DefinitionReference);
			subScript.StartScript();

			if (subScript.HadError)
			{
				throw new InvalidOperationException(
					$"Subscript '{Script.ScriptUnderTest}' failed:{Environment.NewLine}{String.Join(Environment.NewLine, subScript.GetErrorMessages() ?? Enumerable.Empty<string>())}");
			}
		}

		/// <summary>
		/// Reads back the service items that were added by the script under test.
		/// </summary>
		/// <returns>The added service items, ordered by label.</returns>
		public IReadOnlyList<Models.ServiceItem> ReadAddedServiceItems()
		{
			Models.ServiceSpecification specification = Api.ServiceCatalog.ServiceSpecifications
				.Read(Models.ServiceSpecificationExposers.Identifier.Equal(SpecificationId))
				.FirstOrDefault()
				?? throw new InvalidOperationException($"Service Specification '{SpecificationId}' was not found.");

			return specification.ServiceItems
				.Where(item => String.Equals(item.DefinitionReference, DefinitionReference, StringComparison.InvariantCulture))
				.OrderBy(item => item.Label, StringComparer.InvariantCulture)
				.ToList();
		}

		/// <summary>
		/// Removes the fixture this test created. A failure never breaks the test run.
		/// </summary>
		public void CleanUp()
		{
			try
			{
				if (String.IsNullOrWhiteSpace(SpecificationId))
				{
					return;
				}

				Models.ServiceSpecification specification = Api.ServiceCatalog.ServiceSpecifications
					.Read(Models.ServiceSpecificationExposers.Identifier.Equal(SpecificationId))
					.FirstOrDefault();
				if (specification != null)
				{
					Api.ServiceCatalog.ServiceSpecifications.Delete(specification);
				}
			}
			catch (Exception ex)
			{
				engine.GenerateInformation($"RT_AddServiceItemDuplicateLabel: cleanup failed for specification: {ex}");
			}
		}
	}
}
