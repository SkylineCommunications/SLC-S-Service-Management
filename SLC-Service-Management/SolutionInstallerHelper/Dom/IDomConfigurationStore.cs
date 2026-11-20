namespace SLC_ServiceManagement.SolutionInstallerHelper.Dom
{
	using System.Collections.Generic;

	using Skyline.DataMiner.Net.Apps.DataMinerObjectModel;
	using Skyline.DataMiner.Net.Apps.Modules;
	using Skyline.DataMiner.Net.Sections;

	/// <summary>
	/// Reads and writes the DOM configuration (module settings, section, behavior and DOM definitions) of a DataMiner System.
	/// </summary>
	internal interface IDomConfigurationStore
	{
		ModuleSettings GetModuleSettings(string moduleId);

		void CreateModuleSettings(ModuleSettings moduleSettings);

		void UpdateModuleSettings(ModuleSettings moduleSettings);

		IEnumerable<SectionDefinition> GetSectionDefinitions(string moduleId);

		void CreateSectionDefinition(string moduleId, SectionDefinition sectionDefinition);

		void UpdateSectionDefinition(string moduleId, SectionDefinition sectionDefinition);

		IEnumerable<DomBehaviorDefinition> GetBehaviorDefinitions(string moduleId);

		void CreateBehaviorDefinition(string moduleId, DomBehaviorDefinition behaviorDefinition);

		void UpdateBehaviorDefinition(string moduleId, DomBehaviorDefinition behaviorDefinition);

		IEnumerable<DomDefinition> GetDomDefinitions(string moduleId);

		void CreateDomDefinition(string moduleId, DomDefinition domDefinition);

		void UpdateDomDefinition(string moduleId, DomDefinition domDefinition);
	}
}
