namespace SLC_SM_IAS_Profiles.Views
{
	using System;
	using System.Collections.Generic;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations;
	using Skyline.DataMiner.Utils.InteractiveAutomationScript;
	using SLC_SM_IAS_Profiles.Data;
	using SLC_SM_IAS_Profiles.Presenters;

	public class ViewFactory
	{
		private readonly Mode _mode;

		public ViewFactory(Mode mode)
		{
			_mode = mode;
		}

		public ConfigurationView Create(
			IEngine engine,
			List<Option<Models.ConfigurationUnit>> cachedUnits,
			EventHandlers callbacks)
		{
			switch (_mode)
			{
				case Mode.Configuration:
					return new ConfigurationView(engine, cachedUnits, callbacks);
				case Mode.Profile:
					return new ProfileDefinitionView(engine, cachedUnits, callbacks);
				default:
					throw new NotSupportedException($"Script mode {_mode} not supported");
			}
		}
	}
}