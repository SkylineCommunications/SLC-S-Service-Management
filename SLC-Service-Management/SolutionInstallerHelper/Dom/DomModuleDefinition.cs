namespace SLC_ServiceManagement.SolutionInstallerHelper.Dom
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;

	using Skyline.DataMiner.Net.Apps.DataMinerObjectModel;
	using Skyline.DataMiner.Net.Apps.Modules;
	using Skyline.DataMiner.Net.Sections;

	/// <summary>
	/// The desired DOM configuration of one module, as shipped in <c>SetupContent\DomModules</c>.
	/// </summary>
	/// <remarks>
	/// Folder layout per module: <c>&lt;ModuleId&gt;\&lt;ModuleId&gt;.json</c> (module settings) and the optional
	/// <c>SectionDefinitions</c>, <c>DomBehaviorDefinitions</c> and <c>DomDefinitions</c> subfolders with one JSON file per object.
	/// </remarks>
	internal sealed class DomModuleDefinition
	{
		internal const string SectionDefinitionsFolder = "SectionDefinitions";
		internal const string BehaviorDefinitionsFolder = "DomBehaviorDefinitions";
		internal const string DomDefinitionsFolder = "DomDefinitions";

		public DomModuleDefinition(
			ModuleSettings moduleSettings,
			IEnumerable<SectionDefinition> sectionDefinitions,
			IEnumerable<DomBehaviorDefinition> behaviorDefinitions,
			IEnumerable<DomDefinition> domDefinitions)
		{
			ModuleSettings = moduleSettings ?? throw new ArgumentNullException(nameof(moduleSettings));
			SectionDefinitions = (sectionDefinitions ?? Enumerable.Empty<SectionDefinition>()).ToList();
			BehaviorDefinitions = (behaviorDefinitions ?? Enumerable.Empty<DomBehaviorDefinition>()).ToList();
			DomDefinitions = (domDefinitions ?? Enumerable.Empty<DomDefinition>()).ToList();
			Validate();
		}

		public string ModuleId => ModuleSettings.ModuleId;

		public ModuleSettings ModuleSettings { get; }

		public IReadOnlyList<SectionDefinition> SectionDefinitions { get; }

		public IReadOnlyList<DomBehaviorDefinition> BehaviorDefinitions { get; }

		public IReadOnlyList<DomDefinition> DomDefinitions { get; }

		/// <summary>
		/// Loads every module folder under <paramref name="rootFolder"/>, ordered by module ID.
		/// </summary>
		/// <exception cref="DirectoryNotFoundException">The root folder does not exist.</exception>
		/// <exception cref="InvalidDataException">A module folder is incomplete or inconsistent.</exception>
		public static IReadOnlyList<DomModuleDefinition> LoadAll(string rootFolder)
		{
			if (!Directory.Exists(rootFolder))
			{
				throw new DirectoryNotFoundException($"DOM module folder was not found: '{rootFolder}'.");
			}

			var modules = Directory.GetDirectories(rootFolder)
				.OrderBy(d => d, StringComparer.OrdinalIgnoreCase)
				.Select(Load)
				.ToList();

			if (modules.Count == 0)
			{
				throw new InvalidDataException($"No DOM modules found in '{rootFolder}'.");
			}

			var duplicate = modules.GroupBy(m => m.ModuleId, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1);
			if (duplicate != null)
			{
				throw new InvalidDataException($"DOM module '{duplicate.Key}' is defined more than once in '{rootFolder}'.");
			}

			return modules;
		}

		/// <summary>
		/// Loads one module folder.
		/// </summary>
		/// <exception cref="InvalidDataException">The module folder is incomplete or inconsistent.</exception>
		public static DomModuleDefinition Load(string moduleFolder)
		{
			var settingsFiles = Directory.GetFiles(moduleFolder, "*.json", SearchOption.TopDirectoryOnly);
			if (settingsFiles.Length != 1)
			{
				throw new InvalidDataException($"DOM module folder '{moduleFolder}' must contain exactly one module settings JSON file, found {settingsFiles.Length}.");
			}

			try
			{
				return new DomModuleDefinition(
					Read<ModuleSettings>(settingsFiles[0]),
					ReadAll<SectionDefinition>(moduleFolder, SectionDefinitionsFolder),
					ReadAll<DomBehaviorDefinition>(moduleFolder, BehaviorDefinitionsFolder),
					ReadAll<DomDefinition>(moduleFolder, DomDefinitionsFolder));
			}
			catch (InvalidDataException ex)
			{
				throw new InvalidDataException($"DOM module folder '{moduleFolder}' is invalid: {ex.Message}", ex);
			}
		}

		private static IEnumerable<T> ReadAll<T>(string moduleFolder, string subFolder)
		{
			var folder = Path.Combine(moduleFolder, subFolder);
			if (!Directory.Exists(folder))
			{
				return Enumerable.Empty<T>();
			}

			return Directory.GetFiles(folder, "*.json", SearchOption.TopDirectoryOnly)
				.OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
				.Select(Read<T>)
				.ToList();
		}

		private static T Read<T>(string file)
		{
			T value;
			try
			{
				value = DomJson.Deserialize<T>(File.ReadAllText(file));
			}
			catch (Exception ex)
			{
				throw new InvalidDataException($"'{file}' could not be read as {typeof(T).Name}: {ex.Message}", ex);
			}

			if (value == null)
			{
				throw new InvalidDataException($"'{file}' does not contain a {typeof(T).Name}.");
			}

			return value;
		}

		private static void EnsureUnique<T>(IEnumerable<T> items, Func<T, Guid> getId, string kind)
		{
			var duplicate = items.GroupBy(getId).FirstOrDefault(g => g.Count() > 1);
			if (duplicate != null)
			{
				throw new InvalidDataException($"{kind} '{duplicate.Key}' is defined more than once.");
			}
		}

		private void Validate()
		{
			if (String.IsNullOrWhiteSpace(ModuleId))
			{
				throw new InvalidDataException("The module settings have no module ID.");
			}

			foreach (var section in SectionDefinitions)
			{
				EnsureModule(section.GetID()?.ModuleId, $"Section definition '{section.GetName()}'");
			}

			foreach (var behavior in BehaviorDefinitions)
			{
				EnsureModule(behavior.ID?.ModuleId, $"Behavior definition '{behavior.Name}'");
			}

			foreach (var definition in DomDefinitions)
			{
				EnsureModule(definition.ID?.ModuleId, $"DOM definition '{definition.Name}'");
			}

			EnsureUnique(SectionDefinitions, s => s.GetID().Id, "Section definition");
			EnsureUnique(BehaviorDefinitions, b => b.ID.Id, "Behavior definition");
			EnsureUnique(DomDefinitions, d => d.ID.Id, "DOM definition");
		}

		private void EnsureModule(string moduleId, string description)
		{
			if (!String.Equals(moduleId, ModuleId, StringComparison.Ordinal))
			{
				throw new InvalidDataException($"{description} belongs to module '{moduleId}' instead of '{ModuleId}'.");
			}
		}
	}
}
