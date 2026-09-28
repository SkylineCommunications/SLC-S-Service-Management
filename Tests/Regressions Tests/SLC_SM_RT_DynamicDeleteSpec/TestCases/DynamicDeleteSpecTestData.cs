namespace SLC_SM_RT_DynamicDeleteSpec.TestCases
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	using DomHelpers.SlcServicemanagement;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ApiHelpers;

	using Models = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;

	/// <summary>
	/// Owns the fixture of the test and the state shared between its test cases.
	/// </summary>
	internal sealed class DynamicDeleteSpecTestData
	{
		/// <summary>
		/// The service item that is deleted by the script under test.
		/// </summary>
		public const long DeletedNodeId = 0;

		/// <summary>
		/// The service item that has to survive the delete.
		/// </summary>
		public const long RemainingNodeId = 1;

		private readonly IEngine engine;

		public DynamicDeleteSpecTestData(IEngine engine)
		{
			this.engine = engine ?? throw new ArgumentNullException(nameof(engine));
			Api = engine.GetUserConnection().GetServiceManagementApiHelper("Service Catalog");
		}

		public IServiceManagementApiHelper Api { get; }

		public string SpecificationId { get; private set; } = String.Empty;

		public void CreateFixtures()
		{
			var specification = new Models.ServiceSpecification
			{
				Identifier = Guid.NewGuid().ToString(),
				Name = $"RT_DynamicDeleteSpec_{Guid.NewGuid()}",
				Description = "Fixture for dynamic delete regression test.",
				ServiceItems = new List<Models.ServiceItem>
				{
					new Models.ServiceItem
					{
						ServiceItemID = DeletedNodeId,
						Label = "RT Node A",
						Type = SlcServicemanagementIds.Enums.ServiceitemtypesEnum.Service,
						DefinitionReference = "RT-A",
					},
					new Models.ServiceItem
					{
						ServiceItemID = RemainingNodeId,
						Label = "RT Node B",
						Type = SlcServicemanagementIds.Enums.ServiceitemtypesEnum.Service,
						DefinitionReference = "RT-B",
					},
				},
				ServiceItemsRelationships = new List<Models.ServiceItemRelationship>
				{
					new Models.ServiceItemRelationship
					{
						ParentServiceItem = DeletedNodeId.ToString(),
						ParentServiceItemInterfaceId = String.Empty,
						ChildServiceItem = RemainingNodeId.ToString(),
						ChildServiceItemInterfaceId = String.Empty,
					},
				},
			};

			Api.ServiceCatalog.ServiceSpecifications.Create(specification);
			SpecificationId = specification.Identifier;
		}

		/// <summary>
		/// Runs the script under test for the fixture.
		/// </summary>
		/// <param name="nodeIds">The service item IDs to delete.</param>
		/// <param name="connectionIds">The relationship IDs to delete.</param>
		public void InvokeDynamicDelete(string nodeIds, string connectionIds)
		{
			SubScriptOptions subScript = engine.PrepareSubScript(Script.ScriptUnderTest);
			subScript.Synchronous = true;
			subScript.ExtendedErrorInfo = true;
			subScript.SelectScriptParam("DomId", SpecificationId);
			subScript.SelectScriptParam("NodeIds", nodeIds);
			subScript.SelectScriptParam("ConnectionIds", connectionIds);
			subScript.StartScript();

			if (subScript.HadError)
			{
				throw new InvalidOperationException(
					$"Subscript '{Script.ScriptUnderTest}' failed:{Environment.NewLine}{String.Join(Environment.NewLine, subScript.GetErrorMessages() ?? Enumerable.Empty<string>())}");
			}
		}

		/// <summary>
		/// Reads the specification under test back from the system.
		/// </summary>
		/// <returns>The specification.</returns>
		public Models.ServiceSpecification ReadSpecification()
		{
			return Api.ServiceCatalog.ServiceSpecifications
				.Read(Models.ServiceSpecificationExposers.Identifier.Equal(SpecificationId))
				.FirstOrDefault()
				?? throw new InvalidOperationException($"Service Specification '{SpecificationId}' was not found.");
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
				engine.GenerateInformation($"RT_DynamicDeleteSpec: cleanup failed for specification: {ex}");
			}
		}
	}
}
