/*
****************************************************************************
*  Copyright (c),  Skyline Communications NV  All Rights Reserved.    *
****************************************************************************

Revision History:

DATE		VERSION		AUTHOR			COMMENTS

26/10/2026	1.0.0.1		SKA, Skyline	Initial version
****************************************************************************
*/

namespace SLCSMMigrationImport
{
	using System;
	using System.Diagnostics;
	using System.Globalization;
	using System.IO;
	using System.Linq;

	using Newtonsoft.Json;
	using Newtonsoft.Json.Converters;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.SecureCoding.SecureIO;

	using SLCSMMigrationImport.Migration;

	/// <summary>
	/// Imports Service Management (SMG) data, exported from a legacy system by SLC_SM_Migration_Export, into the new DOM model.
	/// </summary>
	public class Script
	{
		public const string DefaultFolder = @"C:\Skyline DataMiner\Documents\DMA_COMMON_DOCUMENTS";
		public const string DefaultFileName = "SMG_Migration_Export.json";

		private const string ParamInputFile = "Input File";
		private const string ParamDryRun = "Dry Run";

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
				engine.Log($"SMG migration import failed: {e}");
				engine.ExitFail($"SMG migration import failed: {e.Message}");
			}
		}

		/// <summary>
		/// Reads a script parameter, also accepting the JSON array notation (["value"]) used by apps.
		/// </summary>
		private static string NormalizeParam(string raw)
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

			return value.Trim().Trim('"').Trim();
		}

		private static bool ParseDryRun(string raw)
		{
			string value = NormalizeParam(raw);
			if (String.IsNullOrEmpty(value))
			{
				return true;
			}

			switch (value.ToUpperInvariant())
			{
				case "TRUE":
				case "YES":
				case "1":
					return true;
				case "FALSE":
				case "NO":
				case "0":
					return false;
				default:
					throw new ArgumentException($"Invalid value '{value}' for '{ParamDryRun}'. Use true or false.");
			}
		}

		private static string ResolvePath(string raw, string defaultFileName)
		{
			string value = NormalizeParam(raw);
			if (String.IsNullOrEmpty(value))
			{
				value = defaultFileName;
			}

			return Path.IsPathRooted(value)
				? SecurePath.CreateSecurePath(value)
				: SecurePath.ConstructSecurePath(DefaultFolder, value);
		}

		private static void RunSafe(IEngine engine)
		{
			engine.Timeout = TimeSpan.FromHours(3);
			DateTime startedAtUtc = DateTime.UtcNow;
			var total = Stopwatch.StartNew();

			string inputFile = ResolvePath(engine.GetScriptParam(ParamInputFile)?.Value, DefaultFileName);
			bool dryRun = ParseDryRun(engine.GetScriptParam(ParamDryRun)?.Value);
			engine.Log($"SMG migration import started (dry run: {dryRun}) from '{inputFile}'.");

			if (!File.Exists(inputFile))
			{
				engine.ExitFail($"Input file '{inputFile}' does not exist. Run SLC_SM_Migration_Export on the legacy system and copy the file to this system.");
				return;
			}

			var export = ReadExport(inputFile);
			string sourceHost = export?.SourceHost;
			DateTime? exportedAtUtc = export?.ExportedAtUtc;
			string exportDuration = export?.ExportDuration;
			TimeSpan readDuration = total.Elapsed;

			var target = new TargetSystem(engine);
			if (!ObsoleteDomCleanup.IsNewModelInstalled(target.GetHelper(KnownIds.NewModelMarker.ModuleId)))
			{
				engine.ExitFail("The new SMG DOM model is not installed on this system. Install the Service Management package first, then re-run the import. Nothing was changed.");
				return;
			}

			// Removes the legacy DOM model parts left behind by an in-place upgrade (only reported in a dry run).
			var cleanup = new ObsoleteDomCleanup(line => engine.Log("SMG migration cleanup: " + line), dryRun);
			foreach (string module in KnownIds.ModulesInScope)
			{
				cleanup.Clean(target.GetHelper(module));
			}

			TimeSpan cleanupDuration = total.Elapsed - readDuration;

			var schemas = KnownIds.ModulesInScope.ToDictionary(m => m, m => TargetModuleSchema.Load(target.GetHelper(m)), StringComparer.OrdinalIgnoreCase);

			var plan = new MigrationPlanner(schemas, target, target).Plan(export);
			TimeSpan planDuration = total.Elapsed - readDuration - cleanupDuration;

			// Free the exported data and the prefetched target instances; the plan holds everything needed from here on.
			export = null;
			target.ClearCache();

			var report = new MigrationExecutor(target, line => engine.Log("SMG migration " + line)).Execute(plan, dryRun);
			report.ModelCleanup.AddRange(cleanup.Results);
			report.GlobalWarnings.AddRange(cleanup.Failures);
			report.SourceFile = inputFile;
			report.SourceHost = sourceHost;
			report.ExportedAtUtc = exportedAtUtc;
			report.ExportDuration = exportDuration;
			report.StartedAtUtc = startedAtUtc;
			report.ReadDuration = readDuration;
			report.CleanupDuration = cleanupDuration;
			report.PlanDuration = planDuration;
			report.ExecuteDuration = total.Elapsed - readDuration - cleanupDuration - planDuration;
			report.TotalDuration = total.Elapsed;

			string reportFile = WriteReport(report);
			string summary = report.BuildSummary();
			engine.Log(summary);
			engine.GenerateInformation(summary);
			engine.AddScriptOutput("Summary", summary);
			engine.AddScriptOutput("ReportFile", reportFile);
			engine.AddScriptOutput("Duration", MigrationReport.FormatDuration(report.TotalDuration));

			string timing = "SMG migration import duration: " + report.BuildTiming();
			engine.Log(timing);
			engine.GenerateInformation(timing);

			if (report.HasFailures)
			{
				engine.ExitFail($"{summary} See report '{reportFile}'.");
			}
		}

		/// <summary>
		/// Streams the export file instead of loading it as one string, so large exports do not run out of memory.
		/// </summary>
		private static MigrationExport ReadExport(string inputFile)
		{
			var serializer = JsonSerializer.Create(new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.None });
			using (var reader = new StreamReader(inputFile))
			using (var json = new JsonTextReader(reader))
			{
				return serializer.Deserialize<MigrationExport>(json);
			}
		}

		private static string WriteReport(MigrationReport report)
		{
			string baseName = "SMG_Migration_Report_" + (report.DryRun ? "DryRun_" : String.Empty) + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
			string textFile = SecurePath.ConstructSecurePath(DefaultFolder, baseName + ".txt");
			string jsonFile = SecurePath.ConstructSecurePath(DefaultFolder, baseName + ".json");

			Directory.CreateDirectory(DefaultFolder);
			using (var writer = new StreamWriter(textFile))
			{
				report.WriteText(writer);
			}

			var serializer = JsonSerializer.Create(new JsonSerializerSettings { Formatting = Formatting.Indented });
			serializer.Converters.Add(new StringEnumConverter());
			using (var writer = new StreamWriter(jsonFile))
			{
				serializer.Serialize(writer, report);
			}

			return textFile;
		}
	}
}
