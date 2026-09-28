namespace SLC_SM_RT_CreateServiceSpecification.TestCases
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
	internal sealed class CreateServiceSpecificationTestData
	{
		private readonly IEngine engine;

		public CreateServiceSpecificationTestData(IEngine engine)
		{
			this.engine = engine ?? throw new ArgumentNullException(nameof(engine));
			Api = engine.GetUserConnection().GetServiceManagementApiHelper("Service Catalog");
			ExpectedName = $"RT_CreateServiceSpecification_{Guid.NewGuid()}";
		}

		public IServiceManagementApiHelper Api { get; }

		/// <summary>
		/// Gets the name the created Service Specification is expected to keep.
		/// </summary>
		public string ExpectedName { get; }

		public string SpecificationId { get; private set; } = String.Empty;

		/// <summary>
		/// Creates the Service Specification that is the subject of this test.
		/// </summary>
		public void CreateSpecification()
		{
			var specification = new Models.ServiceSpecification
			{
				Identifier = Guid.NewGuid().ToString(),
				Name = ExpectedName,
				Description = "Temporary fixture for regression testing.",
				ServiceItems = new List<Models.ServiceItem> { new Models.ServiceItem() },
				ServiceItemsRelationships = new List<Models.ServiceItemRelationship> { new Models.ServiceItemRelationship() },
			};

			Api.ServiceCatalog.ServiceSpecifications.Create(specification);
			SpecificationId = specification.Identifier;
		}

		/// <summary>
		/// Reads the created Service Specification back from the system.
		/// </summary>
		/// <returns>The specification, or <c>null</c> when it was not persisted.</returns>
		public Models.ServiceSpecification ReadSpecification()
		{
			return Api.ServiceCatalog.ServiceSpecifications
				.Read(Models.ServiceSpecificationExposers.Identifier.Equal(SpecificationId))
				.FirstOrDefault();
		}

		/// <summary>
		/// Removes the fixture this test created.
		/// A failure never breaks the test run.
		/// </summary>
		public void CleanUp()
		{
			try
			{
				if (String.IsNullOrWhiteSpace(SpecificationId))
				{
					return;
				}

				Models.ServiceSpecification specification = ReadSpecification();
				if (specification != null)
				{
					Api.ServiceCatalog.ServiceSpecifications.Delete(specification);
				}
			}
			catch (Exception ex)
			{
				engine.GenerateInformation($"RT_CreateServiceSpecification: cleanup failed for specification: {ex}");
			}
		}
	}
}
