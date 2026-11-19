namespace SLC_SM_RT_DeleteServiceCategory.TestCases
{
	using System;
	using System.Linq;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ApiHelpers;

	using Models = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;

	/// <summary>
	/// Owns the fixtures of the test and the state shared between its test cases.
	/// </summary>
	internal sealed class ServiceCategoryTestData
	{
		private readonly IEngine engine;

		public ServiceCategoryTestData(IEngine engine)
		{
			this.engine = engine ?? throw new ArgumentNullException(nameof(engine));
			Api = engine.GetUserConnection().GetServiceManagementApiHelper("Service Inventory");
		}

		public IServiceManagementApiHelper Api { get; }

		public string CategoryId { get; private set; } = String.Empty;

		public void CreateFixtures()
		{
			var category = new Models.ServiceCategory
			{
				Identifier = Guid.NewGuid().ToString(),
				Name = $"RT_DeleteServiceCategory_{Guid.NewGuid()}",
			};

			Api.ServiceCatalog.ServiceCategories.Create(category);
			CategoryId = category.Identifier;

			if (ReadCategory() == null)
			{
				throw new InvalidOperationException($"The service category fixture '{CategoryId}' was not persisted.");
			}
		}

		/// <summary>
		/// Reads the service category fixture back from the system.
		/// </summary>
		/// <returns>The service category, or <c>null</c> when it no longer exists.</returns>
		public Models.ServiceCategory ReadCategory()
		{
			if (String.IsNullOrWhiteSpace(CategoryId))
			{
				return null;
			}

			return Api.ServiceCatalog.ServiceCategories
				.Read(Models.ServiceCategoryExposers.Identifier.Equal(CategoryId))
				.FirstOrDefault();
		}

		/// <summary>
		/// Removes the fixture when the script under test did not already delete it.
		/// </summary>
		public void CleanUp()
		{
			try
			{
				Models.ServiceCategory category = ReadCategory();
				if (category != null)
				{
					Api.ServiceCatalog.ServiceCategories.Delete(category);
				}
			}
			catch (Exception ex)
			{
				engine.GenerateInformation($"RT_DeleteServiceCategory: cleanup failed for service category: {ex}");
			}
		}
	}
}
