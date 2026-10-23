namespace SLC_SM_UDAPI_ServiceManagement.Tests.Controllers
{
	using System.Collections.Generic;
	using Moq;
	using Newtonsoft.Json.Linq;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;
	using Skyline.DataMiner.SDM;
	using SLC_SM_UDAPI_ServiceManagement.Models.Inventory;
	using SLC_SM_UDAPI_ServiceManagement.Models.Inventory.FullService;

	using Cfg = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.Configurations;
	using ParameterType = DomHelpers.SlcConfigurations.SlcConfigurationsIds.Enums.Type;

	[TestClass]
	public class ConfigurationVersionEndpointTests : ServiceInventoryControllerTestBase
	{
		private Service service;

		[TestInitialize]
		public void Initialize()
		{
			service = AddService();

			// FullServiceBuilder dereferences these references, so give them (unresolvable) identifiers.
			service.CategoryId = new SdmObjectReference<ServiceCategory>(Guid.NewGuid().ToString());
			service.ServiceSpecificationId = new SdmObjectReference<ServiceSpecification>(Guid.NewGuid().ToString());
			service.ServiceConfigurationId = new SdmObjectReference<ServiceConfigurationVersion>(Guid.NewGuid().ToString());
		}

		// ---- GET /service/{id}/configurationVersion ----
		[TestMethod]
		public void ListConfigurationVersions_SortsNewestStartDateFirstAndUndatedLast()
		{
			AddVersion(service, "undated");
			AddVersion(service, "old", new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc));
			AddVersion(service, "new", new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));

			var result = Controller.ListConfigurationVersions(service.ServiceID);

			Assert.AreEqual(200, StatusCode(result));
			var names = Value<List<FullServiceConfigurationVersion>>(result).Select(v => v.VersionName).ToArray();
			CollectionAssert.AreEqual(new[] { "new", "old", "undated" }, names);
		}

		[TestMethod]
		public void ListConfigurationVersions_IncludesActiveVersionNotInList()
		{
			var active = new ServiceConfigurationVersion { Identifier = Guid.NewGuid().ToString(), VersionName = "active" };
			StoredVersions.Add(active);
			service.ServiceConfigurationId = new SdmObjectReference<ServiceConfigurationVersion>(active.Identifier);

			var versions = Value<List<FullServiceConfigurationVersion>>(Controller.ListConfigurationVersions(service.ServiceID));

			Assert.AreEqual(1, versions.Count);
			Assert.AreEqual("active", versions[0].VersionName);
		}

		[TestMethod]
		public void ListConfigurationVersions_NoVersions_ReturnsEmptyList()
		{
			var versions = Value<List<FullServiceConfigurationVersion>>(Controller.ListConfigurationVersions(service.ServiceID));
			Assert.AreEqual(0, versions.Count);
		}

		[TestMethod]
		public void ListConfigurationVersions_Unknown_Returns404()
		{
			Assert.AreEqual(404, StatusCode(Controller.ListConfigurationVersions("SERVICE-00404")));
		}

		// ---- GET /service/{id}/configurationVersion/{versionId} ----
		[TestMethod]
		public void GetConfigurationVersion_Existing_Returns200()
		{
			var version = AddVersion(service, "v1");
			var result = Controller.GetConfigurationVersion(service.ServiceID, version.Identifier);
			Assert.AreEqual(200, StatusCode(result));
			Assert.AreEqual("v1", Value<FullServiceConfigurationVersion>(result).VersionName);
		}

		[DataTestMethod]
		[DataRow(null)]
		[DataRow("not-a-guid")]
		public void GetConfigurationVersion_InvalidVersionId_Returns404(string versionId)
		{
			Assert.AreEqual(404, StatusCode(Controller.GetConfigurationVersion(service.ServiceID, versionId)));
		}

		[TestMethod]
		public void GetConfigurationVersion_VersionOfOtherService_Returns404()
		{
			var other = AddService("Other", number: 2);
			var version = AddVersion(other, "foreign");
			Assert.AreEqual(404, StatusCode(Controller.GetConfigurationVersion(service.ServiceID, version.Identifier)));
		}

		[TestMethod]
		public void GetConfigurationVersion_UnknownService_Returns404()
		{
			Assert.AreEqual(404, StatusCode(Controller.GetConfigurationVersion("SERVICE-00404", Guid.NewGuid().ToString())));
		}

		// ---- POST /service/{id}/configurationVersion ----
		[TestMethod]
		public void CreateConfigurationVersion_WithParameter_StoresRecordsAndReturns201()
		{
			var parameter = AddParameter("Bitrate", ParameterType.Number);
			var request = new CreateConfigurationVersionRequest
			{
				VersionName = " v2 ",
				Parameters = new List<ServiceConfigurationValueRequest>
				{
					new ServiceConfigurationValueRequest
					{
						Mandatory = true,
						ConfigurationParameter = new ConfigurationParameterValueRequest { ConfigurationParameterId = Guid.Parse(parameter.Identifier), DoubleValue = 10 },
					},
				},
			};

			var result = Controller.CreateConfigurationVersion(service.ServiceID, request);

			Assert.AreEqual(201, StatusCode(result));
			Assert.AreEqual(1, StoredParameterValues.Count);
			Assert.AreEqual(10, StoredParameterValues[0].DoubleValue);
			Assert.AreEqual("Bitrate", StoredParameterValues[0].Label);
			Assert.AreEqual(1, StoredConfigurationValues.Count);
			Assert.AreEqual("v2", StoredVersions.Single().VersionName);
			Assert.AreEqual(1, service.ConfigurationVersions.Count);
			Assert.AreNotEqual(StoredVersions.Single().Identifier, service.ServiceConfigurationId.Identifier);
		}

		[TestMethod]
		public void CreateConfigurationVersion_Activate_SetsActiveVersion()
		{
			var result = Controller.CreateConfigurationVersion(service.ServiceID, new CreateConfigurationVersionRequest(), activate: true);

			Assert.AreEqual(201, StatusCode(result));
			Assert.AreEqual("New Version", StoredVersions.Single().VersionName);
			Assert.AreEqual(StoredVersions.Single().Identifier, service.ServiceConfigurationId.Identifier);
		}

		[TestMethod]
		public void CreateConfigurationVersion_WithProfile_LinksExistingProfile()
		{
			var definition = new Cfg.ProfileDefinition { Identifier = Guid.NewGuid().ToString() };
			var profile = new Cfg.Profile { Identifier = Guid.NewGuid().ToString() };
			StoredProfileDefinitions.Add(definition);
			StoredProfiles.Add(profile);

			var request = new CreateConfigurationVersionRequest
			{
				Profiles = new List<ServiceProfileRequest>
				{
					new ServiceProfileRequest { ProfileDefinitionId = Guid.Parse(definition.Identifier), ProfileId = Guid.Parse(profile.Identifier) },
				},
			};

			Assert.AreEqual(201, StatusCode(Controller.CreateConfigurationVersion(service.ServiceID, request)));
			Assert.AreEqual(profile.Identifier, StoredServiceProfiles.Single().ProfileId.Identifier);
		}

		[TestMethod]
		public void CreateConfigurationVersion_Unknown_Returns404()
		{
			Assert.AreEqual(404, StatusCode(Controller.CreateConfigurationVersion("SERVICE-00404", new CreateConfigurationVersionRequest())));
		}

		[TestMethod]
		public void CreateConfigurationVersion_NullBody_Returns400()
		{
			Assert.AreEqual(400, StatusCode(Controller.CreateConfigurationVersion(service.ServiceID, null)));
		}

		[TestMethod]
		public void CreateConfigurationVersion_EndBeforeStart_Returns400()
		{
			var request = new CreateConfigurationVersionRequest { StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddDays(-1) };
			Assert.AreEqual(400, StatusCode(Controller.CreateConfigurationVersion(service.ServiceID, request)));
		}

		[TestMethod]
		public void CreateConfigurationVersion_UnknownParameter_Returns400AndWritesNothing()
		{
			var request = ParameterRequest(new ConfigurationParameterValueRequest { ConfigurationParameterId = Guid.NewGuid(), StringValue = "x" });
			Assert.AreEqual(400, StatusCode(Controller.CreateConfigurationVersion(service.ServiceID, request)));
			AssertNothingWritten();
		}

		[TestMethod]
		public void CreateConfigurationVersion_MissingParameterId_Returns400()
		{
			var request = ParameterRequest(new ConfigurationParameterValueRequest { StringValue = "x" });
			Assert.AreEqual(400, StatusCode(Controller.CreateConfigurationVersion(service.ServiceID, request)));
		}

		[TestMethod]
		public void CreateConfigurationVersion_MandatoryWithoutValue_Returns400AndWritesNothing()
		{
			var parameter = AddParameter("Name", ParameterType.Text);
			var request = ParameterRequest(new ConfigurationParameterValueRequest { ConfigurationParameterId = Guid.Parse(parameter.Identifier) }, mandatory: true);
			var result = Controller.CreateConfigurationVersion(service.ServiceID, request);
			Assert.AreEqual(400, StatusCode(result));
			StringAssert.Contains(ErrorDetail(result), "Mandatory");
			AssertNothingWritten();
		}

		[TestMethod]
		public void CreateConfigurationVersion_InvalidType_Returns400()
		{
			var parameter = AddParameter("Name", ParameterType.Text);
			var request = ParameterRequest(new ConfigurationParameterValueRequest { ConfigurationParameterId = Guid.Parse(parameter.Identifier), Type = "bogus" });
			Assert.AreEqual(400, StatusCode(Controller.CreateConfigurationVersion(service.ServiceID, request)));
		}

		[TestMethod]
		public void CreateConfigurationVersion_UnknownProfile_Returns400()
		{
			var request = new CreateConfigurationVersionRequest
			{
				Profiles = new List<ServiceProfileRequest> { new ServiceProfileRequest { ProfileDefinitionId = Guid.NewGuid(), ProfileId = Guid.NewGuid() } },
			};
			Assert.AreEqual(400, StatusCode(Controller.CreateConfigurationVersion(service.ServiceID, request)));
			AssertNothingWritten();
		}

		[TestMethod]
		public void CreateConfigurationVersion_RepositoryThrows_Returns500()
		{
			Services.Setup(r => r.Update(It.IsAny<Service>())).Throws(new InvalidOperationException());
			Assert.AreEqual(500, StatusCode(Controller.CreateConfigurationVersion(service.ServiceID, new CreateConfigurationVersionRequest())));
		}

		[DataTestMethod]
		[DataRow("")]
		[DataRow("2500")]
		public void SaveServiceConfiguration_AddsNumberParameter(string value)
		{
			var parameter = AddParameter("Bitrate", ParameterType.Number);
			var version = AddVersion(service, "v1");
			service.ServiceConfigurationId = new SdmObjectReference<ServiceConfigurationVersion>(version.Identifier);
			var request = new ServiceConfigurationRequest
			{
				VersionId = version.Identifier,
				VersionName = version.VersionName,
				ActivateVersion = true,
				Parameters = new List<ServiceConfigurationParameterRequest>
				{
					new ServiceConfigurationParameterRequest
					{
						ParameterId = parameter.Identifier,
						Value = value,
					},
				},
			};

			var result = Controller.SaveServiceConfiguration(service.ServiceID, JObject.FromObject(request));
			int statusCode = StatusCode(result);
			Assert.AreEqual(200, statusCode, statusCode == 200 ? String.Empty : ErrorDetail(result));

			Assert.AreEqual(1, version.Parameters.Count);
			Assert.AreEqual(1, StoredParameterValues.Count);
			Assert.AreEqual(!String.IsNullOrWhiteSpace(value), StoredParameterValues[0].DoubleValue.HasValue);
			Assert.IsNull(StoredParameterValues[0].StringValue);
			Assert.AreEqual(1, StoredConfigurationValues.Count);
			var record = Value<List<ConfigurationRecord>>(result).Single(configuration => configuration.RecordType == "Parameter");
			Assert.AreEqual(parameter.Identifier, record.ParameterId);
			Assert.AreEqual(String.IsNullOrWhiteSpace(value), Boolean.Parse(record.ParameterNotApplicable));
			Assert.AreEqual(String.IsNullOrWhiteSpace(value) ? String.Empty : value, record.ParameterValue);
		}

		[TestMethod]
		public void SaveServiceConfiguration_RepositoryThrows_Returns500WithErrorReference()
		{
			Services.Setup(repository => repository.Update(It.IsAny<Service>()))
				.Throws(new InvalidOperationException("Repository failure"));

			var result = Controller.SaveServiceConfiguration(
				service.ServiceID,
				JObject.FromObject(new ServiceConfigurationRequest { ActivateVersion = true }));

			Assert.AreEqual(500, StatusCode(result));
			string detail = ErrorDetail(result);
			StringAssert.Contains(detail, "Reference: ");
			Assert.IsFalse(detail.Contains("Repository failure"));
			Assert.AreEqual(1, StoredVersions.Count, "A failed multi-write save may leave earlier records persisted.");
		}

		private static CreateConfigurationVersionRequest ParameterRequest(ConfigurationParameterValueRequest value, bool mandatory = false)
		{
			return new CreateConfigurationVersionRequest
			{
				Parameters = new List<ServiceConfigurationValueRequest>
				{
					new ServiceConfigurationValueRequest { Mandatory = mandatory, ConfigurationParameter = value },
				},
			};
		}

		private Cfg.ConfigurationParameter AddParameter(string name, ParameterType type)
		{
			var parameter = new Cfg.ConfigurationParameter { Identifier = Guid.NewGuid().ToString(), Name = name, Type = type };
			StoredParameters.Add(parameter);
			return parameter;
		}

		private void AssertNothingWritten()
		{
			Assert.AreEqual(0, StoredParameterValues.Count);
			Assert.AreEqual(0, StoredConfigurationValues.Count);
			Assert.AreEqual(0, StoredServiceProfiles.Count);
			Assert.AreEqual(0, StoredVersions.Count);
			Services.Verify(r => r.Update(It.IsAny<Service>()), Times.Never);
		}
	}
}
