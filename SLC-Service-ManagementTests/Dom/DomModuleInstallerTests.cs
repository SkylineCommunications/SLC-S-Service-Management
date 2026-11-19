namespace SLC_ServiceManagement.Tests.Dom
{
	using Newtonsoft.Json;
	using Skyline.DataMiner.Net.Apps.Sections.SectionDefinitions;
	using Skyline.DataMiner.Net.Sections;

	using SLC_ServiceManagement.SolutionInstallerHelper.Dom;

	[TestClass]
	public class DomModuleInstallerTests
	{
		private const string ConfigurationsModuleId = "(slc)configurations";
		private static readonly Guid ConfigurationParameterValueSectionId = new Guid("32879046-c83c-49d8-b6bc-39c77bc7b3b0");

		private static IReadOnlyList<DomModuleDefinition> LoadShippedModules()
		{
			return DomModuleDefinition.LoadAll(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DomModules"));
		}

		private static int CountObjects(IEnumerable<DomModuleDefinition> modules)
		{
			return modules.Sum(m => 1 + m.SectionDefinitions.Count + m.BehaviorDefinitions.Count + m.DomDefinitions.Count);
		}

		private static DomInstallResult Install(InMemoryDomConfigurationStore store)
		{
			return new DomModuleInstaller(store, null).Install(LoadShippedModules());
		}

		[TestMethod]
		public void LoadAll_ShippedModules_AreConsistent()
		{
			var modules = LoadShippedModules();

			CollectionAssert.AreEquivalent(new[] { ConfigurationsModuleId, "(slc)servicemanagement" }, modules.Select(m => m.ModuleId).ToList());
			Assert.IsTrue(modules.All(m => m.SectionDefinitions.Count > 0 && m.DomDefinitions.Count > 0));
		}

		[TestMethod]
		public void Install_EmptySystem_CreatesEverything_AndRerunChangesNothing()
		{
			var store = new InMemoryDomConfigurationStore();
			int total = CountObjects(LoadShippedModules());

			var first = Install(store);
			int writesAfterFirst = store.Writes;
			var second = Install(store);

			Assert.AreEqual(total, first.Created);
			Assert.AreEqual(0, first.Updated);
			Assert.IsFalse(second.HasChanges, second.ToString());
			Assert.AreEqual(total, second.Unchanged);
			Assert.AreEqual(writesAfterFirst, store.Writes);
		}

		[TestMethod]
		public void Install_OutdatedSection_UpdatesOnlyThatSection()
		{
			var store = new InMemoryDomConfigurationStore();
			Install(store);
			var section = (CustomSectionDefinition)store.GetSection(ConfigurationsModuleId, ConfigurationParameterValueSectionId);
			var field = section.GetAllFieldDescriptors().First();
			string shippedTooltip = field.Tooltip;
			field.Tooltip = "Changed on the system";
			section.AddOrReplaceFieldDescriptor(field);
			store.Replace(ConfigurationsModuleId, section);

			var result = Install(store);

			Assert.AreEqual(1, result.Updated);
			Assert.AreEqual(0, result.Created);
			var restored = store.GetSection(ConfigurationsModuleId, ConfigurationParameterValueSectionId).GetAllFieldDescriptors().Single(f => f.ID.Id == field.ID.Id);
			Assert.AreEqual(shippedTooltip, restored.Tooltip);
		}

		[TestMethod]
		public void Install_SectionWithFieldThatIsNotShipped_KeepsThatField()
		{
			var store = new InMemoryDomConfigurationStore();
			Install(store);
			var section = (CustomSectionDefinition)store.GetSection(ConfigurationsModuleId, ConfigurationParameterValueSectionId);
			var legacyField = new FieldDescriptor { ID = new FieldDescriptorID(Guid.NewGuid()), Name = "Legacy", FieldType = typeof(string) };
			section.AddOrReplaceFieldDescriptor(legacyField);
			var shippedField = section.GetAllFieldDescriptors().First();
			shippedField.Tooltip = "Changed on the system";
			section.AddOrReplaceFieldDescriptor(shippedField);
			store.Replace(ConfigurationsModuleId, section);

			var update = Install(store);
			var rerun = Install(store);

			Assert.AreEqual(1, update.Updated);
			Assert.IsFalse(rerun.HasChanges, rerun.ToString());
			var fields = store.GetSection(ConfigurationsModuleId, ConfigurationParameterValueSectionId).GetAllFieldDescriptors();
			Assert.IsTrue(fields.Any(f => f.ID.Id == legacyField.ID.Id), "The field that is not shipped must be kept.");
			Assert.AreNotEqual("Changed on the system", fields.Single(f => f.ID.Id == shippedField.ID.Id).Tooltip);
		}

		[TestMethod]
		public void Install_DefinitionWithSectionLinkThatIsNotShipped_KeepsThatLink()
		{
			var store = new InMemoryDomConfigurationStore();
			var modules = LoadShippedModules();
			Install(store);
			var shipped = modules.Single(m => m.ModuleId == ConfigurationsModuleId).DomDefinitions.First();
			var definition = store.GetDefinition(ConfigurationsModuleId, shipped.ID.Id);
			var legacyLink = new SectionDefinitionLink(new SectionDefinitionID(Guid.NewGuid()));
			definition.SectionDefinitionLinks.Add(legacyLink);
			store.Replace(ConfigurationsModuleId, definition);

			var rerun = Install(store);

			Assert.IsFalse(rerun.HasChanges, rerun.ToString());
			Assert.IsTrue(store.GetDefinition(ConfigurationsModuleId, shipped.ID.Id).SectionDefinitionLinks.Any(l => l.SectionDefinitionID.Id == legacyLink.SectionDefinitionID.Id));
		}

		[TestMethod]
		public void Deserialize_TypeOutsideSlNetTypes_IsRejected()
		{
			string json = "{\"$type\":\"System.IO.FileInfo, mscorlib\",\"fileName\":\"C:\\\\x.txt\"}";

			var ex = Assert.ThrowsException<JsonSerializationException>(() => DomJson.Deserialize<SectionDefinition>(json));
			StringAssert.Contains(ex.InnerException?.Message ?? ex.Message, "is not allowed in a DOM module definition");
		}
	}
}
