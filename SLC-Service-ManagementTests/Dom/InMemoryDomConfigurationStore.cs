namespace SLC_ServiceManagement.Tests.Dom
{
	using Skyline.DataMiner.Net.Apps.DataMinerObjectModel;
	using Skyline.DataMiner.Net.Apps.Modules;
	using Skyline.DataMiner.Net.Sections;

	using SLC_ServiceManagement.SolutionInstallerHelper.Dom;

	/// <summary>
	/// In-memory DOM configuration that, like the server, stores copies and rejects creating an existing or updating a missing object.
	/// </summary>
	internal sealed class InMemoryDomConfigurationStore : IDomConfigurationStore
	{
		private readonly Dictionary<string, ModuleSettings> _moduleSettings = new Dictionary<string, ModuleSettings>();
		private readonly Dictionary<(string, Guid), SectionDefinition> _sections = new Dictionary<(string, Guid), SectionDefinition>();
		private readonly Dictionary<(string, Guid), DomBehaviorDefinition> _behaviors = new Dictionary<(string, Guid), DomBehaviorDefinition>();
		private readonly Dictionary<(string, Guid), DomDefinition> _definitions = new Dictionary<(string, Guid), DomDefinition>();

		public int Writes { get; private set; }

		public int SectionCount => _sections.Count;

		public int BehaviorCount => _behaviors.Count;

		public int DefinitionCount => _definitions.Count;

		public ModuleSettings GetModuleSettings(string moduleId) => _moduleSettings.TryGetValue(moduleId, out var s) ? DomJson.Clone(s) : null;

		public void CreateModuleSettings(ModuleSettings moduleSettings) => Create(_moduleSettings, moduleSettings.ModuleId, moduleSettings);

		public void UpdateModuleSettings(ModuleSettings moduleSettings) => Update(_moduleSettings, moduleSettings.ModuleId, moduleSettings);

		public IEnumerable<SectionDefinition> GetSectionDefinitions(string moduleId) => Read(_sections, moduleId);

		public void CreateSectionDefinition(string moduleId, SectionDefinition sectionDefinition) => Create(_sections, (moduleId, sectionDefinition.GetID().Id), sectionDefinition);

		public void UpdateSectionDefinition(string moduleId, SectionDefinition sectionDefinition) => Update(_sections, (moduleId, sectionDefinition.GetID().Id), sectionDefinition);

		public IEnumerable<DomBehaviorDefinition> GetBehaviorDefinitions(string moduleId) => Read(_behaviors, moduleId);

		public void CreateBehaviorDefinition(string moduleId, DomBehaviorDefinition behaviorDefinition) => Create(_behaviors, (moduleId, behaviorDefinition.ID.Id), behaviorDefinition);

		public void UpdateBehaviorDefinition(string moduleId, DomBehaviorDefinition behaviorDefinition) => Update(_behaviors, (moduleId, behaviorDefinition.ID.Id), behaviorDefinition);

		public IEnumerable<DomDefinition> GetDomDefinitions(string moduleId) => Read(_definitions, moduleId);

		public void CreateDomDefinition(string moduleId, DomDefinition domDefinition) => Create(_definitions, (moduleId, domDefinition.ID.Id), domDefinition);

		public void UpdateDomDefinition(string moduleId, DomDefinition domDefinition) => Update(_definitions, (moduleId, domDefinition.ID.Id), domDefinition);

		public SectionDefinition GetSection(string moduleId, Guid id) => DomJson.Clone(_sections[(moduleId, id)]);

		public DomDefinition GetDefinition(string moduleId, Guid id) => DomJson.Clone(_definitions[(moduleId, id)]);

		/// <summary>
		/// Changes an object directly on the "system", as a user or an older package version would.
		/// </summary>
		public void Replace(string moduleId, SectionDefinition section) => _sections[(moduleId, section.GetID().Id)] = DomJson.Clone(section);

		public void Replace(string moduleId, DomDefinition definition) => _definitions[(moduleId, definition.ID.Id)] = DomJson.Clone(definition);

		private static IEnumerable<T> Read<T>(Dictionary<(string, Guid), T> items, string moduleId)
		{
			return items.Where(i => i.Key.Item1 == moduleId).Select(i => DomJson.Clone(i.Value)).ToList();
		}

		private void Create<TKey, T>(Dictionary<TKey, T> items, TKey key, T value)
		{
			if (items.ContainsKey(key))
			{
				throw new InvalidOperationException($"'{key}' already exists.");
			}

			items[key] = DomJson.Clone(value);
			Writes++;
		}

		private void Update<TKey, T>(Dictionary<TKey, T> items, TKey key, T value)
		{
			if (!items.ContainsKey(key))
			{
				throw new InvalidOperationException($"'{key}' does not exist.");
			}

			items[key] = DomJson.Clone(value);
			Writes++;
		}
	}
}
