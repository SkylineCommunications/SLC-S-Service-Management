namespace SLC_ServiceManagement.SolutionInstallerHelper.Dom
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	using Skyline.DataMiner.Net.Apps.DataMinerObjectModel;
	using Skyline.DataMiner.Net.Apps.Modules;
	using Skyline.DataMiner.Net.Messages;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.Net.Sections;

	/// <summary>
	/// <see cref="IDomConfigurationStore"/> on a live DataMiner System, through <see cref="DomHelper"/> and <see cref="ModuleSettingsHelper"/>.
	/// </summary>
	internal sealed class DomHelperConfigurationStore : IDomConfigurationStore
	{
		private readonly Dictionary<string, DomHelper> _domHelpers = new Dictionary<string, DomHelper>(StringComparer.Ordinal);
		private readonly ModuleSettingsHelper _moduleSettingsHelper;
		private readonly Func<DMSMessage[], DMSMessage[]> _sendMessages;

		public DomHelperConfigurationStore(Func<DMSMessage[], DMSMessage[]> sendMessages)
		{
			_sendMessages = sendMessages ?? throw new ArgumentNullException(nameof(sendMessages));
			_moduleSettingsHelper = new ModuleSettingsHelper(sendMessages);
		}

		public ModuleSettings GetModuleSettings(string moduleId)
		{
			return _moduleSettingsHelper.ModuleSettings.Read(ModuleSettingsExposers.ModuleId.Equal(moduleId)).FirstOrDefault();
		}

		public void CreateModuleSettings(ModuleSettings moduleSettings)
		{
			_moduleSettingsHelper.ModuleSettings.Create(moduleSettings);
		}

		public void UpdateModuleSettings(ModuleSettings moduleSettings)
		{
			_moduleSettingsHelper.ModuleSettings.Update(moduleSettings);
		}

		public IEnumerable<SectionDefinition> GetSectionDefinitions(string moduleId)
		{
			return GetDomHelper(moduleId).SectionDefinitions.Read(new TRUEFilterElement<SectionDefinition>());
		}

		public void CreateSectionDefinition(string moduleId, SectionDefinition sectionDefinition)
		{
			GetDomHelper(moduleId).SectionDefinitions.Create(sectionDefinition);
		}

		public void UpdateSectionDefinition(string moduleId, SectionDefinition sectionDefinition)
		{
			GetDomHelper(moduleId).SectionDefinitions.Update(sectionDefinition);
		}

		public IEnumerable<DomBehaviorDefinition> GetBehaviorDefinitions(string moduleId)
		{
			return GetDomHelper(moduleId).DomBehaviorDefinitions.Read(new TRUEFilterElement<DomBehaviorDefinition>());
		}

		public void CreateBehaviorDefinition(string moduleId, DomBehaviorDefinition behaviorDefinition)
		{
			GetDomHelper(moduleId).DomBehaviorDefinitions.Create(behaviorDefinition);
		}

		public void UpdateBehaviorDefinition(string moduleId, DomBehaviorDefinition behaviorDefinition)
		{
			GetDomHelper(moduleId).DomBehaviorDefinitions.Update(behaviorDefinition);
		}

		public IEnumerable<DomDefinition> GetDomDefinitions(string moduleId)
		{
			return GetDomHelper(moduleId).DomDefinitions.Read(new TRUEFilterElement<DomDefinition>());
		}

		public void CreateDomDefinition(string moduleId, DomDefinition domDefinition)
		{
			GetDomHelper(moduleId).DomDefinitions.Create(domDefinition);
		}

		public void UpdateDomDefinition(string moduleId, DomDefinition domDefinition)
		{
			GetDomHelper(moduleId).DomDefinitions.Update(domDefinition);
		}

		private DomHelper GetDomHelper(string moduleId)
		{
			if (!_domHelpers.TryGetValue(moduleId, out var helper))
			{
				helper = new DomHelper(_sendMessages, moduleId);
				_domHelpers[moduleId] = helper;
			}

			return helper;
		}
	}
}
