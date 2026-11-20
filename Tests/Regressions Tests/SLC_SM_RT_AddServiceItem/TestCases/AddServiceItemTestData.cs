namespace SLC_SM_RT_AddServiceItem.TestCases
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
			DefinitionReference = $"RT_AddServiceItem_{Guid.NewGuid()}";
		}

		public IServiceManagementApiHelper Api { get; }

		/// <summary>
		/// Gets the definition reference the script under test is asked to add.
		/// </summary>
		public string DefinitionReference { get; }

		public string SpecificationId { get; private set; } = String.Empty;

		public void CreateFixtures()
		{
			var specification = new Models.ServiceSpecification
			{
				Identifier = Guid.NewGuid().ToString(),
				Name = $"RT_AddServiceItem_Spec_{Guid.NewGuid()}",
				Description = "Temporary fixture for regression testing.",
				ServiceItems = new List<Models.ServiceItem> { new Models.ServiceItem() },
				ServiceItemsRelationships = new List<Models.ServiceItemRelationship> { new Models.ServiceItemRelationship() },
			};

			Api.ServiceCatalog.ServiceSpecifications.Create(specification);
			SpecificationId = specification.Identifier;
		}

		/// <summary>
		/// Reads back the service item the script under test added to the fixture.
		/// </summary>
		/// <returns>The added service item, or <c>null</c> when it was not added.</returns>
		public Models.ServiceItem ReadAddedServiceItem()
		{
			Models.ServiceSpecification specification = Api.ServiceCatalog.ServiceSpecifications
				.Read(Models.ServiceSpecificationExposers.Identifier.Equal(SpecificationId))
				.FirstOrDefault()
				?? throw new InvalidOperationException("Service specification fixture was not found after script execution.");

			return specification.ServiceItems?
				.FirstOrDefault(item => String.Equals(item.DefinitionReference, DefinitionReference, StringComparison.InvariantCulture));
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
				engine.GenerateInformation($"RT_AddServiceItem: cleanup failed for specification: {ex}");
			}
		}
	}
}
