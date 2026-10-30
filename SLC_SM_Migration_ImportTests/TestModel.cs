namespace SLC_SM_Migration_ImportTests
{
	using Skyline.DataMiner.Net.Apps.DataMinerObjectModel;
	using Skyline.DataMiner.Net.Apps.DataMinerObjectModel.Status;
	using Skyline.DataMiner.Net.Apps.Sections.SectionDefinitions;
	using Skyline.DataMiner.Net.GenericEnums;
	using Skyline.DataMiner.Net.Sections;

	using SLCSMMigrationImport.Migration;

	using Cpv = SLCSMMigrationImport.Migration.KnownIds.ConfigurationParameterValue;

	/// <summary>
	/// Small new-model schema used by the tests: a configuration parameter value (no behavior) and a service (with behavior).
	/// </summary>
	internal sealed class TestModel
	{
		public const string Config = KnownIds.ConfigurationsModuleId;
		public const string Smg = KnownIds.ServiceManagementModuleId;

		public static readonly Guid CpvDefinition = Guid.NewGuid();
		public static readonly Guid CpvLabel = Guid.NewGuid();
		public static readonly Guid CpvType = Guid.NewGuid();

		public static readonly Guid ServiceDefinition = Guid.NewGuid();
		public static readonly Guid ServiceSection = Guid.NewGuid();
		public static readonly Guid ServiceName = Guid.NewGuid();
		public static readonly Guid ServiceProperties = Guid.NewGuid();
		public static readonly Guid ServiceNewField = Guid.NewGuid();
		public static readonly Guid ServiceBehavior = Guid.NewGuid();

		public static readonly Guid RemovedDefinition = new Guid("689844f0-bbc3-430b-998a-33a0a8da7358");
		public static readonly Guid RemovedSection = Guid.NewGuid();
		public static readonly Guid RemovedField = Guid.NewGuid();

		public Dictionary<string, TargetModuleSchema> Schemas { get; } = new Dictionary<string, TargetModuleSchema>(StringComparer.OrdinalIgnoreCase)
		{
			{ Config, BuildConfigSchema() },
			{ Smg, BuildSmgSchema() },
		};

		public static FieldDescriptor Field(Guid id, string name, Type type)
		{
			return new FieldDescriptor { ID = new FieldDescriptorID(id), Name = name, FieldType = type, IsOptional = true };
		}

		public static FieldDescriptor RefField(Guid id, string name, string moduleId, Type type)
		{
			return new DomInstanceFieldDescriptor(moduleId) { ID = new FieldDescriptorID(id), Name = name, FieldType = type, IsOptional = true };
		}

		public static MigrationExport Export(IEnumerable<ExportedInstance> config, IEnumerable<ExportedInstance> smg)
		{
			var configModule = new ExportedModule { ModuleId = Config, Instances = config.ToList() };
			configModule.SectionDefinitions.Add(new ExportedSectionDefinitionInfo
			{
				Id = Cpv.Section,
				Name = "Configuration Parameter Value",
				Fields =
				{
					new ExportedFieldInfo { Id = CpvLabel, Name = "Label" },
					new ExportedFieldInfo { Id = CpvType, Name = "Type" },
					new ExportedFieldInfo { Id = Cpv.LinkedScript, Name = "Linked Script" },
					new ExportedFieldInfo { Id = Cpv.LegacyLinkedConfigurationParameter, Name = "Linked Configuration Parameter" },
					new ExportedFieldInfo { Id = Cpv.LegacyLinkedInstanceReference, Name = "Linked Instance Reference" },
				},
			});

			var smgModule = new ExportedModule { ModuleId = Smg, Instances = smg.ToList() };
			smgModule.Definitions.Add(new ExportedDefinitionInfo { Id = RemovedDefinition, Name = "ServiceProperties" });
			smgModule.Definitions.Add(new ExportedDefinitionInfo { Id = ServiceDefinition, Name = "Service" });
			smgModule.SectionDefinitions.Add(new ExportedSectionDefinitionInfo
			{
				Id = ServiceSection,
				Name = "Service Info",
				Fields =
				{
					new ExportedFieldInfo { Id = ServiceName, Name = "Service Name" },
					new ExportedFieldInfo { Id = ServiceProperties, Name = "Service Properties" },
					new ExportedFieldInfo { Id = RemovedField, Name = "Scripts" },
				},
			});

			return new MigrationExport { FormatVersion = MigrationExport.CurrentFormatVersion, Modules = { configModule, smgModule } };
		}

		public static ExportedInstance Instance(Guid definition, string status, Guid sectionDefinition, params ExportedField[] fields)
		{
			return new ExportedInstance
			{
				Id = Guid.NewGuid(),
				DefinitionId = definition,
				Name = "Item " + Guid.NewGuid().ToString("N").Substring(0, 6),
				StatusId = status,
				Sections = { new ExportedSection { SectionId = Guid.NewGuid(), SectionDefinitionId = sectionDefinition, Fields = fields.ToList() } },
			};
		}

		public static ExportedField Value(Guid fieldId, string type, params string[] values)
		{
			return new ExportedField { FieldId = fieldId, ValueType = type, Values = values.ToList() };
		}

		public static ExportedField ListValue(Guid fieldId, string type, params string[] values)
		{
			return new ExportedField { FieldId = fieldId, ValueType = type, IsList = true, Values = values.ToList() };
		}

		public static object GetValue(DomInstance instance, Guid fieldId)
		{
			return instance.Sections.SelectMany(s => s.FieldValues).FirstOrDefault(f => f.FieldDescriptorID.Id == fieldId)?.Value?.Value;
		}

		private static TargetModuleSchema BuildConfigSchema()
		{
			var section = new CustomSectionDefinition { ID = new SectionDefinitionID(Cpv.Section) { ModuleId = Config }, Name = "Configuration Parameter Value" };
			section.AddOrReplaceFieldDescriptor(Field(CpvLabel, "Label", typeof(string)));
			section.AddOrReplaceFieldDescriptor(Field(Cpv.LinkedScript, "Linked Script", typeof(string)));
			section.AddOrReplaceFieldDescriptor(Field(Cpv.IsLinked, "IsLinked", typeof(bool)));
			section.AddOrReplaceFieldDescriptor(RefField(Cpv.LinkedConsumer, "Linked Consumer", Config, typeof(List<Guid>)));

			var typeEnum = new GenericEnum<int>();
			typeEnum.AddEntry("Number", 0);
			typeEnum.AddEntry("Text", 1);
			section.AddOrReplaceFieldDescriptor(new GenericEnumFieldDescriptor
			{
				ID = new FieldDescriptorID(CpvType),
				Name = "Type",
				FieldType = typeof(GenericEnum<int>),
				GenericEnumInstance = typeEnum,
				IsOptional = true,
			});

			var definition = new DomDefinition
			{
				ID = new DomDefinitionId(CpvDefinition) { ModuleId = Config },
				Name = "Configuration Parameter Value",
				SectionDefinitionLinks = { new SectionDefinitionLink(new SectionDefinitionID(Cpv.Section) { ModuleId = Config }) },
			};

			var profileSection = new CustomSectionDefinition { ID = new SectionDefinitionID(KnownIds.Profile.Section) { ModuleId = Config }, Name = "Profile Info" };
			profileSection.AddOrReplaceFieldDescriptor(RefField(KnownIds.Profile.NestedProfiles, "Profiles", Config, typeof(List<Guid>)));
			var profileDefinition = new DomDefinition
			{
				ID = new DomDefinitionId(KnownIds.Profile.Definition) { ModuleId = Config },
				Name = "Profile",
				SectionDefinitionLinks = { new SectionDefinitionLink(new SectionDefinitionID(KnownIds.Profile.Section) { ModuleId = Config }) },
			};

			return new TargetModuleSchema(Config, new[] { definition, profileDefinition }, new SectionDefinition[] { section, profileSection }, Array.Empty<DomBehaviorDefinition>());
		}

		private static TargetModuleSchema BuildSmgSchema()
		{
			var section = new CustomSectionDefinition { ID = new SectionDefinitionID(ServiceSection) { ModuleId = Smg }, Name = "Service Info" };
			section.AddOrReplaceFieldDescriptor(Field(ServiceName, "Service Name", typeof(string)));
			section.AddOrReplaceFieldDescriptor(RefField(ServiceProperties, "Service Properties", Smg, typeof(List<Guid>)));
			section.AddOrReplaceFieldDescriptor(Field(ServiceNewField, "New Field", typeof(string)));

			var behavior = new DomBehaviorDefinition
			{
				ID = new DomBehaviorDefinitionId(ServiceBehavior) { ModuleId = Smg },
				InitialStatusId = "new",
				Statuses = new List<DomStatus> { new DomStatus("new", "New"), new DomStatus("designed", "Designed"), new DomStatus("active", "Active"), new DomStatus("retired", "Retired") },
				StatusTransitions = new List<DomStatusTransition>
				{
					new DomStatusTransition("new_to_designed", "new", "designed"),
					new DomStatusTransition("designed_to_active", "designed", "active"),
					new DomStatusTransition("active_to_designed", "active", "designed"),
				},
			};

			var definition = new DomDefinition
			{
				ID = new DomDefinitionId(ServiceDefinition) { ModuleId = Smg },
				Name = "Service",
				SectionDefinitionLinks = { new SectionDefinitionLink(new SectionDefinitionID(ServiceSection) { ModuleId = Smg }) },
				DomBehaviorDefinitionId = new DomBehaviorDefinitionId(ServiceBehavior) { ModuleId = Smg },
			};

			var definitions = new List<DomDefinition> { definition };
			var sections = new List<SectionDefinition> { section };
			foreach (var container in KnownIds.ProfileContainers)
			{
				var ownerSection = new CustomSectionDefinition { ID = new SectionDefinitionID(container.Section) { ModuleId = Smg }, Name = container.Name + " Info" };
				ownerSection.AddOrReplaceFieldDescriptor(RefField(container.LinksField, "Profiles", Smg, typeof(List<Guid>)));
				var linkSection = new CustomSectionDefinition { ID = new SectionDefinitionID(container.LinkSection) { ModuleId = Smg }, Name = container.Name + " Profile" };
				linkSection.AddOrReplaceFieldDescriptor(RefField(container.LinkProfileField, "Profile", Config, typeof(Guid)));
				sections.Add(ownerSection);
				sections.Add(linkSection);
				definitions.Add(new DomDefinition
				{
					ID = new DomDefinitionId(container.Definition) { ModuleId = Smg },
					Name = container.Name,
					SectionDefinitionLinks = { new SectionDefinitionLink(new SectionDefinitionID(container.Section) { ModuleId = Smg }) },
				});
				definitions.Add(new DomDefinition
				{
					ID = new DomDefinitionId(container.LinkDefinition) { ModuleId = Smg },
					Name = container.Name + " Profile",
					SectionDefinitionLinks = { new SectionDefinitionLink(new SectionDefinitionID(container.LinkSection) { ModuleId = Smg }) },
				});
			}

			return new TargetModuleSchema(Smg, definitions, sections, new[] { behavior });
		}
	}

	internal sealed class FakeLookup : ITargetLookup
	{
		public Dictionary<InstanceKey, DomInstance> Existing { get; } = new Dictionary<InstanceKey, DomInstance>();

		public HashSet<InstanceKey> Others { get; } = new HashSet<InstanceKey>();

		public void Prefetch(string moduleId, IEnumerable<Guid> ids)
		{
		}

		public DomInstance GetExisting(string moduleId, Guid id)
		{
			return Existing.TryGetValue(new InstanceKey(moduleId, id), out var instance) ? instance : null;
		}

		public bool Exists(string moduleId, Guid id)
		{
			return GetExisting(moduleId, id) != null || Others.Contains(new InstanceKey(moduleId, id));
		}
	}

	internal sealed class FakeEnvironment : IEnvironmentChecks
	{
		public bool ServiceExists(string serviceKey) => true;

		public bool ScriptExists(string scriptName) => scriptName != "MissingScript";
	}

	internal sealed class FakeWriter : IDomWriter
	{
		public List<string> Calls { get; } = new List<string>();

		public string FailOnTransition { get; set; }

		public DomInstance Create(DomInstance instance)
		{
			Calls.Add("Create " + instance.ID.Id);
			return (DomInstance)instance.Clone();
		}

		public DomInstance Update(DomInstance instance)
		{
			Calls.Add($"Update {instance.ID.Id} {instance.StatusId} {TestModel.GetValue(instance, TestModel.ServiceName)}");
			return (DomInstance)instance.Clone();
		}

		public void Delete(DomInstance instance)
		{
			Calls.Add("Delete " + instance.ID.Id);
		}

		public DomInstance DoStatusTransition(DomInstance instance, string transitionId)
		{
			Calls.Add("Transition " + transitionId);
			if (transitionId == FailOnTransition)
			{
				throw new InvalidOperationException("transition rejected");
			}

			var result = (DomInstance)instance.Clone();
			result.StatusId = transitionId.Substring(transitionId.LastIndexOf('_') + 1);
			return result;
		}
	}
}
