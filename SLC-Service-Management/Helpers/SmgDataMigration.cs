using System;
using System.IO;
using System.Linq;

using Skyline.AppInstaller;
using Skyline.DataMiner.Automation;
using Skyline.DataMiner.Net.Apps.DataMinerObjectModel;
using Skyline.DataMiner.Net.Apps.Modules;
using Skyline.DataMiner.Net.Messages.SLDataGateway;
using Skyline.DataMiner.Net.Sections;

/// <summary>
///     Migrates the SMG data of a system running the legacy DOM model to the new DOM model during the package install.
///     Call <see cref="ExportIfLegacy"/> before the new DOM model is imported and <see cref="MigrateExportedData"/> after.
/// </summary>
internal sealed class SmgDataMigration
{
	private const string MigrationExportScriptName = "SLC_SM_Migration_Export";
	private const string MigrationImportScriptName = "SLC_SM_Migration_Import";
	private const string MigrationReportFolder = @"C:\Skyline DataMiner\Documents\DMA_COMMON_DOCUMENTS";
	private const string ConfigurationsModuleId = "(slc)configurations";
	private const string ServiceManagementModuleId = "(slc)servicemanagement";

	// Covers the export (max 30 min) and import (max 30 min) sub-scripts, which run synchronously during the install.
	private static readonly TimeSpan MigrationTimeout = TimeSpan.FromHours(1);

	private static readonly Guid ConfigurationParameterValueSectionId = new Guid("32879046-c83c-49d8-b6bc-39c77bc7b3b0");

	private static readonly Guid[] LegacyConfigurationParameterValueFieldIds =
	{
		new Guid("438b7699-9be3-4737-bb4f-f680efb7b7ee"),
		new Guid("429acf15-69ab-4ee5-8f34-fec675b09b17"),
	};

	private static readonly Guid[] LegacyServiceManagementDefinitionIds =
	{
		new Guid("689844f0-bbc3-430b-998a-33a0a8da7358"),
		new Guid("3f3ba92f-45dc-45ef-b6ed-5b98e2d17a33"),
		new Guid("f384fc4f-708b-46a1-9474-bf9fbbe358a7"),
		new Guid("402f8f25-7712-4f6b-b124-58b901b53ccc"),
		new Guid("b678f80b-4e71-42b1-ae03-f41d42ea07ef"),
		new Guid("1ad00086-e6e7-499f-a878-2ab856fb7930"),
	};

	private readonly IEngine engine;
	private readonly AppInstaller installer;
	private string migrationFile;

	public SmgDataMigration(IEngine engine, AppInstaller installer)
	{
		this.engine = engine ?? throw new ArgumentNullException(nameof(engine));
		this.installer = installer ?? throw new ArgumentNullException(nameof(installer));
	}

	/// <summary>
	///     Exports the SMG data when the system still runs the legacy DOM model. Must run before the new DOM model is imported.
	/// </summary>
	public void ExportIfLegacy()
	{
		if (!IsLegacyDomModel())
		{
			return;
		}

		engine.Timeout = MigrationTimeout;
		migrationFile = ExportLegacyData();
	}

	/// <summary>
	///     Imports the exported legacy data. The import script also removes the legacy DOM model parts. Does nothing when nothing was exported.
	/// </summary>
	public void MigrateExportedData()
	{
		if (migrationFile == null)
		{
			return;
		}

		ImportLegacyData();
	}

	private bool IsLegacyDomModel()
	{
		var moduleSettings = new ModuleSettingsHelper(engine.SendSLNetMessages).ModuleSettings;

		if (moduleSettings.Read(ModuleSettingsExposers.ModuleId.Equal(ConfigurationsModuleId)).Any())
		{
			var section = new DomHelper(engine.SendSLNetMessages, ConfigurationsModuleId).SectionDefinitions
				.Read(new TRUEFilterElement<SectionDefinition>())
				.FirstOrDefault(s => s.GetID()?.Id == ConfigurationParameterValueSectionId);

			if (section != null && section.GetAllFieldDescriptors().Any(f => LegacyConfigurationParameterValueFieldIds.Contains(f.ID.Id)))
			{
				installer.Log("Legacy SMG DOM model detected (legacy link fields on Configuration Parameter Value). The SMG data will be migrated.");
				return true;
			}
		}

		if (moduleSettings.Read(ModuleSettingsExposers.ModuleId.Equal(ServiceManagementModuleId)).Any())
		{
			var legacyDefinition = new DomHelper(engine.SendSLNetMessages, ServiceManagementModuleId).DomDefinitions
				.Read(new TRUEFilterElement<DomDefinition>())
				.FirstOrDefault(d => LegacyServiceManagementDefinitionIds.Contains(d.ID.Id));

			if (legacyDefinition != null)
			{
				installer.Log($"Legacy SMG DOM model detected (definition '{legacyDefinition.Name}'). The SMG data will be migrated.");
				return true;
			}
		}

		installer.Log("No legacy SMG DOM model detected. Data migration skipped.");
		return false;
	}

	private string ExportLegacyData()
	{
		string fileName = $"SMG_Migration_Export_Install_{DateTime.Now:yyyyMMdd_HHmmss}.json";
		installer.Log($"Exporting legacy SMG data to '{Path.Combine(MigrationReportFolder, fileName)}'...");

		var subScript = engine.PrepareSubScript(MigrationExportScriptName);
		subScript.SelectScriptParam("Output File", fileName);
		subScript.Synchronous = true;
		subScript.ExtendedErrorInfo = true;
		subScript.StartScript();

		if (subScript.HadError)
		{
			throw new InvalidOperationException($"Exporting the legacy SMG data failed, the DOM model was not changed: {String.Join(" -> ", subScript.GetErrorMessages())}");
		}

		installer.Log("Legacy SMG data exported.");
		return fileName;
	}

	private void ImportLegacyData()
	{
		installer.Log($"Importing the legacy SMG data from '{migrationFile}' into the new DOM model...");

		var subScript = engine.PrepareSubScript(MigrationImportScriptName);
		subScript.SelectScriptParam("Input File", migrationFile);
		subScript.SelectScriptParam("Dry Run", "false");
		subScript.Synchronous = true;
		subScript.ExtendedErrorInfo = true;
		subScript.StartScript();

		if (subScript.HadError)
		{
			Warn($"WARNING: the SMG data migration did not complete for all items: {String.Join(" -> ", subScript.GetErrorMessages())}. "
				+ $"See the SMG_Migration_Report_*.txt in '{MigrationReportFolder}', fix the issues and re-run '{MigrationImportScriptName}' with Input File '{migrationFile}'.");
			return;
		}

		installer.Log($"SMG data migration completed. The report is available in '{MigrationReportFolder}'.");
	}

	private void Warn(string warning)
	{
		installer.Log(warning);
		engine.GenerateInformation(warning);
	}
}
