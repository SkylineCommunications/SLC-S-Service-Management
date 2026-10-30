namespace SLCSMMigrationImport.Migration
{
	using System;
	using System.Collections.Generic;

	/// <summary>
	/// IDs needed by the migration that are not (or no longer) available in the new DOM model.
	/// </summary>
	public static class KnownIds
	{
		public const string ConfigurationsModuleId = "(slc)configurations";
		public const string ServiceManagementModuleId = "(slc)servicemanagement";
		public const string WorkflowModuleId = "(slc)workflow";

		/// <summary>
		/// Modules in import order: configurations are referenced by service management, so they go first.
		/// </summary>
		public static readonly IReadOnlyList<string> ModulesInScope = new[] { ConfigurationsModuleId, ServiceManagementModuleId };

		/// <summary>
		/// Legacy definitions that were removed on purpose in the new model (data is dropped and reported).
		/// </summary>
		public static readonly IReadOnlyDictionary<Guid, string> RemovedDefinitions = new Dictionary<Guid, string>
		{
			{ new Guid("689844f0-bbc3-430b-998a-33a0a8da7358"), "ServiceProperties" },
			{ new Guid("3f3ba92f-45dc-45ef-b6ed-5b98e2d17a33"), "ServicePropertyValues" },
			{ new Guid("f384fc4f-708b-46a1-9474-bf9fbbe358a7"), "ServiceConfiguration (legacy)" },
			{ new Guid("402f8f25-7712-4f6b-b124-58b901b53ccc"), "ProtocolTest" },
			{ new Guid("b678f80b-4e71-42b1-ae03-f41d42ea07ef"), "Script" },
			{ new Guid("1ad00086-e6e7-499f-a878-2ab856fb7930"), "ReferencedProfileDefinitions" },
		};

		/// <summary>
		/// Legacy section definitions that were removed in the new model.
		/// </summary>
		public static readonly IReadOnlyDictionary<Guid, string> RemovedSections = new Dictionary<Guid, string>
		{
			{ new Guid("b2602ab2-8e69-4699-820b-f82057bea6c6"), "ReferencedProfileDefinitionsInfo" },
			{ new Guid("7b3cd4a3-54d4-4888-b849-10e0be91c613"), "ScriptInfo" },
			{ new Guid("03ce0808-7550-4102-be91-5ce101c5e9c8"), "ProtocolTestInfo" },
			{ new Guid("77cfd79c-7b47-41aa-a476-1be4c0738537"), "ServicePropertyValueInfo" },
			{ new Guid("6bd03e43-d23a-43f9-8d1d-f81c7e713556"), "ServicePropertyInfo" },
			{ new Guid("e1265675-b4a0-48ac-b19d-72ed90ed32e7"), "ServiceConfigurationParametersValues" },
			{ new Guid("79a261bf-f7b3-45e6-88f6-e0db12ab3bec"), "ServicePropertyValue" },
			{ new Guid("342bb39e-e8c2-4e82-9a97-606a942dca27"), "DiscreteServicePropertyValueOptions" },
		};

		/// <summary>
		/// Legacy fields that were removed from sections that still exist in the new model.
		/// </summary>
		public static readonly IReadOnlyDictionary<Guid, string> RemovedFields = new Dictionary<Guid, string>
		{
			{ ConfigurationParameterValue.LegacyLinkedConfigurationParameter, "ConfigurationParameterValue.LinkedConfigurationParameter" },
			{ ConfigurationParameterValue.LegacyLinkedInstanceReference, "ConfigurationParameterValue.LinkedInstanceReference" },
			{ new Guid("c1d597f1-4913-4994-9eae-b872ff849d85"), "ProfileInfo.TestedProtocols" },
			{ new Guid("e13126f7-2463-4144-9a80-5d38b4186a1d"), "ProfileDefinitionInfo.ProfileDefinitions" },
			{ new Guid("13209cca-2312-4a53-98f3-89bd1e1b6b58"), "ProfileDefinitionInfo.Scripts" },
		};

		/// <summary>
		/// A field that only exists in the new model (Service Info.Scripts): proves the new DOM model is installed.
		/// </summary>
		public static class NewModelMarker
		{
			public const string ModuleId = ServiceManagementModuleId;
			public static readonly Guid Section = new Guid("fd700f84-430c-4626-af7e-052d93b89323");
			public static readonly Guid Field = new Guid("916cc1a7-62c8-46fd-9f29-3edefc1d0472");
		}

		public static class ConfigurationParameterValue
		{
			public static readonly Guid Section = new Guid("32879046-c83c-49d8-b6bc-39c77bc7b3b0");
			public static readonly Guid IsLinked = new Guid("e8c83351-8f29-40c1-94ad-9fb01c2dbac7");
			public static readonly Guid LinkedConsumer = new Guid("e59629ef-7255-4d82-806d-f26e24342eb2");
			public static readonly Guid LinkedScript = new Guid("a71d0565-b13f-43c7-b006-045ea1b859fb");

			/// <summary>Legacy "Link" flag ("Dummy Link") written by the spec/order configuration editors; replaced by IsLinked.</summary>
			public static readonly Guid LegacyLinkedInstanceReference = new Guid("438b7699-9be3-4737-bb4f-f680efb7b7ee");

			/// <summary>Legacy producer reference (Guid); replaced by adding the producer to LinkedConsumer.</summary>
			public static readonly Guid LegacyLinkedConfigurationParameter = new Guid("429acf15-69ab-4ee5-8f34-fec675b09b17");
		}

		public static class Profile
		{
			public static readonly Guid Definition = new Guid("d5f65610-26b4-4fe0-ab8b-d4a6088711a5");
			public static readonly Guid Section = new Guid("94e60948-5051-42bd-b4d8-00b184583a72");
			public static readonly Guid NestedProfiles = new Guid("ccc27894-b87d-4d48-bee0-966bd0a77381");
		}

		/// <summary>
		/// A list field that holds the top-level profile links of a configuration (service version or specification).
		/// The legacy editors also added a link for every nested profile; the new model only keeps the top-level ones.
		/// </summary>
		public sealed class ProfileContainer
		{
			public ProfileContainer(string name, Guid definition, Guid section, Guid linksField, Guid linkDefinition, Guid linkSection, Guid linkProfileField)
			{
				Name = name;
				Definition = definition;
				Section = section;
				LinksField = linksField;
				LinkDefinition = linkDefinition;
				LinkSection = linkSection;
				LinkProfileField = linkProfileField;
			}

			public string Name { get; }

			public Guid Definition { get; }

			public Guid Section { get; }

			public Guid LinksField { get; }

			public Guid LinkDefinition { get; }

			public Guid LinkSection { get; }

			public Guid LinkProfileField { get; }
		}

		public static readonly IReadOnlyList<ProfileContainer> ProfileContainers = new[]
		{
			new ProfileContainer(
				"Service Configuration Version",
				new Guid("42e3f398-b966-41fa-96c7-a7a0189f0aab"),
				new Guid("f91f1783-75c4-447b-a82c-76150902a33d"),
				new Guid("6a6e5360-dc29-4e16-9874-d56a584df9e0"),
				new Guid("b3c7ef51-4f9a-44c1-9572-b8641c47f2b3"),
				new Guid("ffb3508a-aaac-4312-bf6d-c64c5f9cd0a0"),
				new Guid("a163544f-d48a-42fe-916e-5f41a9f3d15e")),
			new ProfileContainer(
				"Service Specification",
				new Guid("dbe96de4-ca9d-4eba-a06e-b089ec14ffca"),
				new Guid("42fa2357-bed3-4456-b2d2-9a2b8f61fbab"),
				new Guid("7038079c-a9ef-4f0b-8f43-798460315d7f"),
				new Guid("3f872685-8bf7-4f59-80f8-6d21916d1f97"),
				new Guid("78212f45-e9f1-44a1-80ad-be43dd19e21e"),
				new Guid("25e8b1c8-5ab5-43b3-9426-73be75a3bd3c")),
		};

		public static class ServiceInfo
		{
			public static readonly Guid MonitoringService = new Guid("8dc55def-3eff-4f57-a637-a0c24ab376ad");
		}

		public static class ServiceItems
		{
			public static readonly Guid Section = new Guid("1ac7e347-20e5-4b2b-8838-5cb412e89427");
			public static readonly Guid ServiceItemType = new Guid("8b3e1590-2eeb-4c2c-b249-dd61023a1b06");
			public static readonly Guid DefinitionReference = new Guid("30c7c421-7c97-4723-91e1-046ba1792ec8");
			public static readonly Guid ServiceItemScript = new Guid("bfc7a72c-5e0d-4ffe-9c96-27f5e749d3fa");
			public static readonly Guid ImplementationReference = new Guid("8cedc78a-a23f-46c4-92a3-30393704f340");

			public const string TypeWorkflow = "Workflow";
			public const string TypeService = "Service";
		}
	}
}
