namespace SLC_ServiceManagement.SolutionInstallerHelper
{
	using Skyline.DataMiner.Utils.SecureCoding.SecureIO;

	using SLC_ServiceManagement.SolutionInstallerHelper.Models;

	internal static class Constants
	{
		internal const string DllImportFolder = @"C:\Skyline DataMiner\ProtocolScripts\DllImport";
		internal const string DomImportExportScriptName = "DOM ImportExport";

		internal const string MediaOpsCatalogId = "1b67a623-4ca6-4d25-8b3d-ed4e39496a75";
		internal const string MediaOpsCatalogName = "MediaOps";

		internal const string SolutionAppId = "service-management";
		internal const string SolutionAppName = "Service Management";
		internal const string SolutionAppUrl = "/public/service-management";

		internal const string SolutionCatalogId = "97599e37-6da8-4c0a-9c59-11838d130a77";
		internal const string SolutionDisplayName = "Service Management";
		internal const string SolutionLibrariesRootFolder = @"C:\Skyline DataMiner\ProtocolScripts\DllImport\SolutionLibraries";
		internal const string SolutionRegisterScriptName = "SLC_SM_AS_RegisterSolution";

		internal static readonly FallbackDependency[] SolutionLibraryFallbackDependencies =
		{
			new FallbackDependency(
				"Solutions.MediaOps.Common",
				@"Dependencies\MediaOps",
				DllImportFolder,
				"Skyline.DataMiner.Utils.MediaOps.Temp.Common.dll"),

			new FallbackDependency(
				"Solutions.MediaOps.Live",
				@"Dependencies\Solutions.MediaOps.Live",
				SecurePath.ConstructSecurePath(Constants.SolutionLibrariesRootFolder, "Solutions.MediaOps.Live"),
				"Skyline.DataMiner.Dev.Utils.Solutions.MediaOps.Live.dll"),

			new FallbackDependency(
				"Solutions.MediaOps.Plan",
				@"Dependencies\Solutions.MediaOps.Plan",
				SecurePath.ConstructSecurePath(Constants.SolutionLibrariesRootFolder, "Solutions.MediaOps.Plan"),
				"Skyline.DataMiner.Dev.Utils.Solutions.MediaOps.Plan.dll"),
		};
	}
}