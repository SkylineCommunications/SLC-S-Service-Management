using System;
using System.IO;
using System.Linq;

using Skyline.AppInstaller;
using Skyline.DataMiner.Automation;
using Skyline.DataMiner.Net.AppPackages;
using Skyline.DataMiner.Net.Exceptions;
using Skyline.DataMiner.Net.Messages;
using Skyline.DataMiner.Net.Messages.Advanced;

/// <summary>
///     DataMiner Script Class.
/// </summary>
internal class Script
{
	private const string DomImportExportScriptName = "DOM ImportExport";
	private const string RegisterSolutionScriptName = "SLC_SM_AS_RegisterSolution";
	private const string SolutionCatalogId = "97599e37-6da8-4c0a-9c59-11838d130a77";
	private const string SolutionDisplayName = "Service Management";
	private const string MediaOpsCatalogId = "1b67a623-4ca6-4d25-8b3d-ed4e39496a75";
	private const string MediaOpsCatalogName = "MediaOps";
	private const string FallbackMediaOpsSourceFolder = @"Dependencies\MediaOps";
	private const string DllImportFolder = @"C:\Skyline DataMiner\ProtocolScripts\DllImport";
	private const string SolutionLibrariesRootFolder = @"C:\Skyline DataMiner\ProtocolScripts\DllImport\SolutionLibraries";

	private static readonly string[] RequiredMediaOpsRuntimeDlls =
	{
		"Skyline.DataMiner.Utils.MediaOps.Temp.Common.dll",
	};

	private static readonly FallbackDependency[] SolutionLibraryFallbackDependencies =
	{
		new FallbackDependency("Solutions.MediaOps.Live", @"Dependencies\Solutions.MediaOps.Live", "Skyline.DataMiner.Dev.Utils.Solutions.MediaOps.Live.dll"),
		new FallbackDependency("Solutions.MediaOps.Plan", @"Dependencies\Solutions.MediaOps.Plan", "Skyline.DataMiner.Dev.Utils.Solutions.MediaOps.Plan.dll"),
	};

	private static string setupContentPath;

	/// <summary>
	///     The script entry point.
	/// </summary>
	/// <param name="engine">Provides access to the Automation engine.</param>
	/// <param name="context">Provides access to the installation context.</param>
	[AutomationEntryPoint(AutomationEntryPointType.Types.InstallAppPackage)]
	public static void Install(IEngine engine, AppInstallContext context)
	{
		try
		{
			engine.Timeout = new TimeSpan(0, 10, 0);
			engine.GenerateInformation("Starting installation");
			var installer = new AppInstaller(Engine.SLNetRaw, context);
			setupContentPath = installer.GetSetupContentDirectory();

			installer.InstallDefaultContent();
			EnsureMediaOpsDependencies(engine, installer);

			installer.Log("Importing DOM...");
			ImportDom(engine, installer);

			RegisterSolution(engine, installer, context);
		}
		catch (Exception e)
		{
			engine.ExitFail($"Exception encountered during installation: {e}");
		}
	}

	private static void RegisterSolution(IEngine engine, AppInstaller installer, AppInstallContext context)
	{
		installer.Log($"Registering Solution {SolutionDisplayName} [{SolutionCatalogId}] with version {context.AppInfo.Version} in SDM...");

		var subscript = engine.PrepareSubScript(RegisterSolutionScriptName);
		subscript.SelectScriptParam("Id", SolutionCatalogId);
		subscript.SelectScriptParam("DisplayName", SolutionDisplayName);
		subscript.SelectScriptParam("Version", context.AppInfo.Version);

		subscript.Synchronous = true;
		subscript.StartScript();

		if (subscript.HadError)
		{
			throw new InvalidOperationException($"Run Subscript '{RegisterSolutionScriptName}' failed: {String.Join(" -> ", subscript.GetErrorMessages())}");
		}
	}

	private static void EnsureSolutionLibraryFallbackDependencies(IEngine engine, AppInstaller installer)
	{
		foreach (var dependency in SolutionLibraryFallbackDependencies)
		{
			EnsureSolutionLibraryFallbackDependency(engine, installer, dependency);
		}
	}

	private static void EnsureSolutionLibraryFallbackDependency(IEngine engine, AppInstaller installer, FallbackDependency dependency)
	{
		var targetFolder = Path.Combine(SolutionLibrariesRootFolder, dependency.SolutionLibraryName);
		var targetDllPath = Path.Combine(targetFolder, dependency.DllName);
		if (File.Exists(targetDllPath))
		{
			installer.Log($"{dependency.SolutionLibraryName} runtime DLL already available. Fallback deployment skipped.");
			return;
		}

		var sourceDirectory = Path.Combine(setupContentPath, dependency.SourceSubFolder);
		var sourcePath = Path.Combine(sourceDirectory, dependency.DllName);
		if (!File.Exists(sourcePath))
		{
			installer.Log($"Fallback {dependency.SolutionLibraryName} DLL is not present at '{sourcePath}'. Skipping fallback deployment.");
			return;
		}

		Directory.CreateDirectory(targetFolder);
		File.Copy(sourcePath, targetDllPath, overwrite: false);
		SyncFile(engine, targetDllPath);
		installer.Log($"Deployed fallback {dependency.SolutionLibraryName} DLL to '{targetFolder}'.");
	}

	private static void ImportDom(IEngine engine, AppInstaller installer)
	{
		var path = Path.Combine(setupContentPath, "DOMImportExport");
		engine.GenerateInformation($"setupContentPath for DOM: {path}");

		if (!Directory.Exists(path))
		{
			throw new DirectoryNotFoundException($"DOM import folder was not found: '{path}'.");
		}

		RunDomImportSubScript(engine, path);
		installer.Log("DOM import completed successfully.");
	}

	private static void RunDomImportSubScript(IEngine engine, string path)
	{
		var subScript = engine.PrepareSubScript(DomImportExportScriptName);
		subScript.ExtendedErrorInfo = true;
		subScript.SelectScriptParam("Action", "Import");
		subScript.SelectScriptParam("Path", path);
		subScript.SelectScriptParam("ModuleNames", "-1");
		subScript.Synchronous = true;
		subScript.StartScript();

		if (subScript.HadError)
		{
			throw new InvalidOperationException($"Run Subscript 'DOM ImportExport' failed: {String.Join(" -> ", subScript.GetErrorMessages())}");
		}
	}

	private static void EnsureMediaOpsDependencies(IEngine engine, AppInstaller installer)
	{
		installer.Log("Validating MediaOps dependency for Service Management installation (SDM registry + fallback DLL checks)...");
		bool mediaOpsInstalled = IsMediaOpsInstalledInSdmRegistry(engine, installer);
		installer.Log(mediaOpsInstalled
			? "MediaOps is installed according to the SDM registry."
			: "MediaOps is not installed according to the SDM registry.");

		EnsureSolutionLibraryFallbackDependencies(engine, installer);

		var missingDllsInRuntime = RequiredMediaOpsRuntimeDlls.Where(dllName => !File.Exists(Path.Combine(DllImportFolder, dllName))).ToList();
		if (!missingDllsInRuntime.Any())
		{
			installer.Log("MediaOps runtime DLLs already available. Fallback DLL deployment skipped.");
			return;
		}

		installer.Log($"MediaOps runtime DLLs missing ({String.Join(", ", missingDllsInRuntime)}). Deploying fallback MediaOps DLLs required by Service Management scripts.");
		DeployMissingMediaOpsDlls(engine, installer);
	}

	private static bool IsMediaOpsInstalledInSdmRegistry(IEngine engine, AppInstaller installer)
	{
		try
		{
			var helper = new AppPackageHelper(engine.SendSLNetMessage);
			var installedApps = helper.GetInstalledApps();
			var mediaOpsApp = installedApps.FirstOrDefault(app =>
			{
				var appInfo = app?.AppInfo;
				var appIdName = appInfo?.AppID?.Name;
				return String.Equals(appIdName, MediaOpsCatalogId, StringComparison.OrdinalIgnoreCase)
					|| String.Equals(appInfo?.DisplayName, MediaOpsCatalogName, StringComparison.OrdinalIgnoreCase)
					|| String.Equals(appInfo?.Name, MediaOpsCatalogName, StringComparison.OrdinalIgnoreCase);
			});

			if (mediaOpsApp == null)
			{
				return false;
			}

			installer.Log($"MediaOps detected in SDM registry: Name='{mediaOpsApp.AppInfo?.Name}', DisplayName='{mediaOpsApp.AppInfo?.DisplayName}', Version='{mediaOpsApp.AppInfo?.Version}'.");
			return true;
		}
		catch (Exception e)
		{
			installer.Log($"Failed to query SDM registry for MediaOps detection. Falling back to file-based dependency validation. Reason: {e.Message}");
			return false;
		}
	}

	private static void DeployMissingMediaOpsDlls(IEngine engine, AppInstaller installer)
	{
		var sourceDirectory = Path.Combine(setupContentPath, FallbackMediaOpsSourceFolder);
		if (!Directory.Exists(sourceDirectory))
		{
			throw new DirectoryNotFoundException($"Fallback MediaOps dependency directory was not found: '{sourceDirectory}'.");
		}

		var targetDirectory = DllImportFolder;
		Directory.CreateDirectory(targetDirectory);

		foreach (var dllName in RequiredMediaOpsRuntimeDlls)
		{
			var targetPath = Path.Combine(targetDirectory, dllName);
			if (File.Exists(targetPath))
			{
				installer.Log($"MediaOps fallback DLL '{dllName}' already exists in '{targetDirectory}'. Existing file is preserved.");
				continue;
			}

			var sourcePath = Path.Combine(sourceDirectory, dllName);
			if (!File.Exists(sourcePath))
			{
				throw new FileNotFoundException($"Required fallback MediaOps DLL '{dllName}' is missing from setup content.", sourcePath);
			}

			File.Copy(sourcePath, targetPath, overwrite: false);
			SyncFile(engine, targetPath);
			installer.Log($"Deployed MediaOps fallback DLL '{dllName}' to '{targetDirectory}'.");
		}
	}

	private static void SyncFile(IEngine engine, string path)
	{
		var message = new SetDataMinerInfoMessage
		{
			What = 41,
			StrInfo1 = path,
			IInfo2 = 34,
		};

		var response = engine.SendSLNetSingleResponseMessage(message) ?? throw new DataMinerException("Server response was null while syncing fallback dependency.");
		if (response is CreateProtocolFileResponse createProtocolFileResponse && createProtocolFileResponse.ErrorCode != 0)
		{
			throw new DataMinerException($"Syncing fallback dependency failed with error code: {createProtocolFileResponse.ErrorCode}.");
		}
	}

	private sealed class FallbackDependency
	{
		public FallbackDependency(string solutionLibraryName, string sourceSubFolder, string dllName)
		{
			SolutionLibraryName = solutionLibraryName;
			SourceSubFolder = sourceSubFolder;
			DllName = dllName;
		}

		public string SolutionLibraryName { get; }

		public string SourceSubFolder { get; }

		public string DllName { get; }
	}
}