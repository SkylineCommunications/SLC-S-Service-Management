/*
****************************************************************************
*  Copyright (c),  Skyline Communications NV  All Rights Reserved.    *
****************************************************************************

Revision History:

DATE		VERSION		AUTHOR			COMMENTS

26/10/2026	1.0.0.1		SKA, Skyline	Initial version
****************************************************************************
*/

namespace SLCSMMigrationExport
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.IO;
	using System.Linq;
	using System.Text;

	using Newtonsoft.Json;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Net.Apps.DataMinerObjectModel;
	using Skyline.DataMiner.Utils.SecureCoding.SecureIO;

	using SLCSMMigrationExport.Migration;

	/// <summary>
	/// Exports the Service Management (SMG) DOM data of a legacy system to a JSON file. Does not modify any data.
	/// </summary>
	public class Script
	{
		public const string DefaultFolder = @"C:\Skyline DataMiner\Documents\DMA_COMMON_DOCUMENTS";
		public const string DefaultFileName = "SMG_Migration_Export.json";
		public const string PackagerProfilesFileName = "SMG_Packager_Profiles.txt";

		private const string ParamOutputFile = "Output File";

		private static readonly string[] Modules = { "(slc)configurations", "(slc)servicemanagement" };

		private static readonly Guid ProfileDefinitionId = new Guid("d5f65610-26b4-4fe0-ab8b-d4a6088711a5");
		private static readonly Guid ProfileNameFieldId = new Guid("ffac1d81-d314-4430-99e7-db82b57c4a23");
		private static readonly Guid ProfileIsReusableFieldId = new Guid("520179df-1c10-44d5-ba36-74ea97961d44");

		/// <summary>
		/// The script entry point.
		/// </summary>
		/// <param name="engine">Link with SLAutomation process.</param>
		public void Run(IEngine engine)
		{
			try
			{
				RunSafe(engine);
			}
			catch (ScriptAbortException)
			{
				throw;
			}
			catch (ScriptForceAbortException)
			{
				throw;
			}
			catch (ScriptTimeoutException)
			{
				throw;
			}
			catch (Exception e)
			{
				engine.Log($"SMG migration export failed: {e}");
				engine.ExitFail($"SMG migration export failed: {e.Message}");
			}
		}

		private static string ResolvePath(string raw)
		{
			string value = (raw ?? String.Empty).Trim();
			if (value.StartsWith("[", StringComparison.Ordinal) && value.EndsWith("]", StringComparison.Ordinal))
			{
				try
				{
					value = JsonConvert.DeserializeObject<string[]>(value)?.FirstOrDefault() ?? String.Empty;
				}
				catch (JsonException)
				{
					// Not a JSON array: use the raw value.
				}
			}

			value = value.Trim().Trim('"').Trim();
			if (String.IsNullOrEmpty(value))
			{
				value = DefaultFileName;
			}

			return Path.IsPathRooted(value)
				? SecurePath.CreateSecurePath(value)
				: SecurePath.ConstructSecurePath(DefaultFolder, value);
		}

		private static void RunSafe(IEngine engine)
		{
			engine.Timeout = TimeSpan.FromHours(1);
			var total = Stopwatch.StartNew();
			string outputFile = ResolvePath(engine.GetScriptParam(ParamOutputFile)?.Value);

			var export = new MigrationExport
			{
				FormatVersion = MigrationExport.CurrentFormatVersion,
				ExportedAtUtc = DateTime.UtcNow,
				SourceHost = Environment.MachineName,
			};

			var summary = new StringBuilder();
			foreach (string moduleId in Modules)
			{
				var module = DomExporter.ExportModule(new DomHelper(engine.SendSLNetMessages, moduleId));
				export.Modules.Add(module);

				if (module.Definitions.Count == 0)
				{
					engine.Log($"SMG migration export: module '{moduleId}' has no DOM definitions on this system.");
				}

				summary.Append($"{moduleId}: {module.Instances.Count} instances");
				foreach (var group in module.Instances.GroupBy(i => i.DefinitionId))
				{
					string name = module.Definitions.FirstOrDefault(d => d.Id == group.Key)?.Name ?? group.Key.ToString();
					engine.Log($"SMG migration export: [{moduleId}] {name}: {group.Count()} instances.");
				}

				summary.Append("; ");
			}

			string directory = Path.GetDirectoryName(outputFile);
			if (!String.IsNullOrEmpty(directory))
			{
				Directory.CreateDirectory(directory);
			}

			// Writing the file itself is not included: the duration is stored inside that file.
			TimeSpan readDuration = total.Elapsed;
			export.ExportDuration = FormatDuration(readDuration);

			using (var writer = File.CreateText(outputFile))
			{
				JsonSerializer.Create(new JsonSerializerSettings { Formatting = Formatting.Indented }).Serialize(writer, export);
			}

			TimeSpan writeDuration = total.Elapsed - readDuration;
			string profilesFile = WritePackagerProfiles(export, directory ?? DefaultFolder, readDuration, writeDuration, total);
			engine.AddScriptOutput("PackagerProfilesFile", profilesFile);

			string message = $"SMG migration export written to '{outputFile}' in {FormatDuration(total.Elapsed)}: {summary.ToString().TrimEnd(' ', ';')}.";
			engine.Log(message);
			engine.GenerateInformation(message);
			engine.AddScriptOutput("OutputFile", outputFile);
			engine.AddScriptOutput("Duration", FormatDuration(total.Elapsed));

			string timing = $"SMG migration export duration: total {FormatDuration(total.Elapsed)} (reading DOM data {FormatDuration(readDuration)}, writing export file {FormatDuration(writeDuration)}, profiles check {FormatDuration(total.Elapsed - readDuration - writeDuration)}).";
			engine.Log(timing);
			engine.GenerateInformation(timing);
		}

		private static string FormatDuration(TimeSpan duration)
		{
			return $"{(int)duration.TotalHours:D2}:{duration.Minutes:D2}:{duration.Seconds:D2}.{duration.Milliseconds:D3}";
		}

		/// <summary>
		/// Writes, for each exported profile, whether it is reusable and which services/specifications use it (orphans are not linked to any) to a text file:
		/// first the profiles with "Packager" in their name, then all profiles. Read-only check.
		/// </summary>
		/// <returns>The path of the written file.</returns>
		private static string WritePackagerProfiles(MigrationExport export, string directory, TimeSpan readDuration, TimeSpan writeDuration, Stopwatch total)
		{
			var usage = new ProfileUsageIndex(export, ProfileDefinitionId, ProfileNameFieldId);
			var allProfiles = export.Modules
				.SelectMany(m => m.Instances)
				.Where(i => i.DefinitionId == ProfileDefinitionId)
				.Select(i =>
				{
					var fields = i.Sections.SelectMany(s => s.Fields).ToList();
					string name = fields.FirstOrDefault(f => f.FieldId == ProfileNameFieldId)?.Values.FirstOrDefault() ?? i.Name;
					string reusable = fields.FirstOrDefault(f => f.FieldId == ProfileIsReusableFieldId)?.Values.FirstOrDefault();
					return new ProfileCheck(i.Id, name, String.Equals(reusable, "true", StringComparison.OrdinalIgnoreCase), usage.GetUsage(i.Id));
				})
				.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
				.ToList();
			var packagerProfiles = allProfiles.Where(p => (p.Name ?? String.Empty).IndexOf("Packager", StringComparison.OrdinalIgnoreCase) >= 0).ToList();

			var sb = new StringBuilder();
			sb.AppendLine($"Profiles on {export.SourceHost}, exported at {export.ExportedAtUtc:o} (UTC)");
			sb.AppendLine($"Export duration: reading DOM data {FormatDuration(readDuration)}, writing export file {FormatDuration(writeDuration)}");
			sb.AppendLine("Usage: 'Service' = used by a service (directly or nested in another profile), 'Specification only' = only used by a specification, 'Orphan' = not used by any service or specification.");
			AppendProfiles(sb, "PACKAGER PROFILES (name contains 'Packager')", packagerProfiles);
			AppendProfiles(sb, "ALL PROFILES", allProfiles);
			sb.AppendLine();
			sb.AppendLine($"Total export duration: {FormatDuration(total.Elapsed)}");

			string file = SecurePath.ConstructSecurePath(directory, PackagerProfilesFileName);
			File.WriteAllText(file, sb.ToString());
			return file;
		}

		private static void AppendProfiles(StringBuilder sb, string title, List<ProfileCheck> profiles)
		{
			int reusableCount = profiles.Count(p => p.IsReusable);
			sb.AppendLine();
			sb.AppendLine(title);
			sb.AppendLine($"Total: {profiles.Count}, reusable: {reusableCount}, not reusable: {profiles.Count - reusableCount}");
			sb.AppendLine($"Used by a service: {profiles.Count(p => p.Usage.Services.Count > 0)}, specification only: {profiles.Count(p => p.Usage.Services.Count == 0 && !p.Usage.IsOrphan)}, orphan: {profiles.Count(p => p.Usage.IsOrphan)}");
			sb.AppendLine("Reusable\tUsage\tName\tID\tServices\tSpecifications");
			foreach (var profile in profiles)
			{
				sb.AppendLine($"{(profile.IsReusable ? "Yes" : "No")}\t{profile.Usage.Status}\t{profile.Name}\t{profile.Id}\t{String.Join(", ", profile.Usage.Services)}\t{String.Join(", ", profile.Usage.Specifications)}");
			}
		}

		private sealed class ProfileCheck
		{
			public ProfileCheck(Guid id, string name, bool isReusable, ProfileUsage usage)
			{
				Id = id;
				Name = name;
				IsReusable = isReusable;
				Usage = usage;
			}

			public Guid Id { get; }

			public string Name { get; }

			public bool IsReusable { get; }

			public ProfileUsage Usage { get; }
		}
	}
}
