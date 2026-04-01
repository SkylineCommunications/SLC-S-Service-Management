namespace Skyline.DataMiner.Utils.ServiceManagement.Common.Extensions
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.IO;
	using System.Linq;

	using Newtonsoft.Json;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Net.Apps.DataMinerObjectModel;
	using Skyline.DataMiner.Net.Apps.Modules;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.Net.Sections;

	public static class ScriptExtensions
	{
		public static T ReadScriptParamFromApp<T>(this IEngine engine, string name)
		{
			return ReadScriptParamsFromApp<T>(engine, name).FirstOrDefault();
		}

		public static string ReadScriptParamFromApp(this IEngine engine, string name)
		{
			return ReadScriptParamsFromApp<string>(engine, name).FirstOrDefault();
		}

		public static ICollection<T> ReadScriptParamsFromApp<T>(this IEngine engine, string name)
		{
			string param = engine.GetScriptParam(name)?.Value;
			if (param == null)
			{
				throw new ArgumentException($"No script input parameter provided with name '{name}'");
			}

			if (param.StartsWith("[") && param.EndsWith("]"))
			{
				return JsonConvert.DeserializeObject<ICollection<T>>(param);
			}

			object value;
			if (typeof(T) == typeof(Guid))
			{
				value = Guid.Parse(param);
			}
			else if (typeof(T).IsEnum)
			{
				value = Enum.Parse(typeof(T), param, ignoreCase: true);
			}
			else
			{
				value = Convert.ChangeType(param, typeof(T));
			}

			return new List<T> { (T)value };
		}

		public static ICollection<string> ReadScriptParamsFromApp(this IEngine engine, string name)
		{
			return ReadScriptParamsFromApp<string>(engine, name);
		}

		public static T PerformanceLogger<T>(this IEngine engine, string methodName, Func<T> func)
		{
			if (func == null)
			{
				throw new ArgumentNullException(nameof(func));
			}

			var stopwatch = Stopwatch.StartNew();

			try
			{
				return func();
			}
			finally
			{
				stopwatch.Stop();
				engine.GenerateInformation($"[{methodName}] executed in {stopwatch.ElapsedMilliseconds} ms");
			}
		}

		public static void PerformanceLogger(this IEngine engine, string methodName, Action action)
		{
			if (action == null)
			{
				throw new ArgumentNullException(nameof(action));
			}

			var stopwatch = Stopwatch.StartNew();

			try
			{
				action();
			}
			finally
			{
				stopwatch.Stop();
				engine.GenerateInformation($"[{methodName}] executed in {stopwatch.ElapsedMilliseconds} ms");
			}
		}

		public static bool DomModelExists(this IEngine engine, string moduleId, IEnumerable<Guid> sectionIds, ModuleSettingsHelper moduleSettingsHelper = null)
		{
			if (moduleSettingsHelper == null)
			{
				moduleSettingsHelper = new ModuleSettingsHelper(engine.SendSLNetMessages);
			}

			if (String.IsNullOrWhiteSpace(moduleId))
			{
				engine.Log("DomModelExists| Module ID is null or empty.");
				return false;
			}

			var result = moduleSettingsHelper.ModuleSettings.Read(ModuleSettingsExposers.ModuleId.Equal(moduleId));
			if (result == null || !result.Any())
			{
				engine.Log($"DomModelExists| No module settings found for module '{moduleId}'.");
				return false;
			}

			if (sectionIds == null)
			{
				return true;
			}

			var domHelper = new DomHelper(engine.SendSLNetMessages, moduleId);

			FilterElement<SectionDefinition> filter = new ORFilterElement<SectionDefinition>();
			foreach (var sectionId in sectionIds)
			{
				filter = filter.OR(SectionDefinitionExposers.ID.Equal(sectionId));
			}

			var sections = domHelper.SectionDefinitions.Read(filter);

			if (sections == null || sections.Count != sectionIds.Count())
			{
				var foundSectionIds = sections?.Select(s => s.GetID().Id) ?? Enumerable.Empty<Guid>();
				var missingSectionIds = sectionIds.Except(foundSectionIds);
				engine.Log($"DomModelExists| Not all section definitions found for module '{moduleId}'. Missing sections: [{string.Join(", ", missingSectionIds)}]");
				return false;
			}

			return true;
		}

		public static bool IsSrmInstalled(this IEngine engine)
		{
			return File.Exists("C:\\Skyline DataMiner\\Webpages\\SRM\\SRM_Solution_About.txt");
		}
	}
}