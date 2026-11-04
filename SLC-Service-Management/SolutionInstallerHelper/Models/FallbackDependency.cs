namespace SLC_ServiceManagement.SolutionInstallerHelper.Models
{
	using Skyline.DataMiner.Utils.SecureCoding.SecureIO;

	public sealed class FallbackDependency
	{
		public FallbackDependency(string solutionLibraryName, string sourceDirectory, string targetDirectory, string dllName)
		{
			SolutionLibraryName = solutionLibraryName;
			DllName = dllName;
			SourceDirectory = SecurePath.ConstructSecurePath(sourceDirectory);
			TargetDirectory = SecurePath.ConstructSecurePath(targetDirectory);
		}

		public string DllName { get; }

		public string SolutionLibraryName { get; }

		public string SourceDirectory { get; }

		public string SourceDllPath => SecurePath.ConstructSecurePath(SourceDirectory, DllName);

		public string TargetDirectory { get; }

		public string TargetDllPath => SecurePath.ConstructSecurePath(TargetDirectory, DllName);
	}
}