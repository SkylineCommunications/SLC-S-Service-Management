namespace SLC_SM_Common.Extensions
{
	using Skyline.DataMiner.Automation;
	using System;

	using Skyline.DataMiner.Core.DataMinerSystem.Common;
	using Skyline.DataMiner.Net.Apps.Modules;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.Analytics.GenericInterface;

	public static class DmsExtensions
	{
		public static bool ServiceExistsSafe(this IDms dms, string serviceName, out IDmsService service)
		{
			try
			{
				service = dms.GetService(serviceName);
				return true;
			}
			catch
			{
				service = default;
				return false;
			}
		}

		public static bool DomModelExists(this GQIDMS dms, string moduleId, ModuleSettingsHelper moduleSettingsHelper = null)
		{
			if (moduleSettingsHelper == null)
			{
				moduleSettingsHelper = new ModuleSettingsHelper(dms.SendMessages);
			}

			if (String.IsNullOrWhiteSpace(moduleId))
			{
				return false;
			}

			var result = moduleSettingsHelper.ModuleSettings.Read(ModuleSettingsExposers.ModuleId.Equal(moduleId));
			if (result == null || result.Count == 0)
			{
				return false;
			}

			return true;
		}
	}
}
