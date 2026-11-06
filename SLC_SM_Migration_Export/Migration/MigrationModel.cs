namespace SLCSMMigrationExport.Migration
{
	using System;
	using System.Collections.Generic;

	/// <summary>
	/// Root of the JSON file written by the SLC_SM_Migration_Export script on the legacy system.
	/// Must stay compatible with the model in SLC_SM_Migration_Import.
	/// </summary>
	public sealed class MigrationExport
	{
		public const int CurrentFormatVersion = 1;

		public int FormatVersion { get; set; }

		public DateTime ExportedAtUtc { get; set; }

		public string SourceHost { get; set; }

		/// <summary>
		/// Gets or sets how long reading the DOM data took (hh:mm:ss.fff), excluding writing this file.
		/// </summary>
		public string ExportDuration { get; set; }

		public List<ExportedModule> Modules { get; set; } = new List<ExportedModule>();
	}

	public sealed class ExportedModule
	{
		public string ModuleId { get; set; }

		public List<ExportedDefinitionInfo> Definitions { get; set; } = new List<ExportedDefinitionInfo>();

		public List<ExportedSectionDefinitionInfo> SectionDefinitions { get; set; } = new List<ExportedSectionDefinitionInfo>();

		public List<ExportedInstance> Instances { get; set; } = new List<ExportedInstance>();
	}

	public sealed class ExportedDefinitionInfo
	{
		public Guid Id { get; set; }

		public string Name { get; set; }
	}

	public sealed class ExportedSectionDefinitionInfo
	{
		public Guid Id { get; set; }

		public string Name { get; set; }

		public List<ExportedFieldInfo> Fields { get; set; } = new List<ExportedFieldInfo>();
	}

	public sealed class ExportedFieldInfo
	{
		public Guid Id { get; set; }

		public string Name { get; set; }

		public string Type { get; set; }
	}

	public sealed class ExportedInstance
	{
		public Guid Id { get; set; }

		public Guid DefinitionId { get; set; }

		public string Name { get; set; }

		public string StatusId { get; set; }

		public List<ExportedSection> Sections { get; set; } = new List<ExportedSection>();
	}

	public sealed class ExportedSection
	{
		public Guid SectionId { get; set; }

		public Guid SectionDefinitionId { get; set; }

		public List<ExportedField> Fields { get; set; } = new List<ExportedField>();
	}

	public sealed class ExportedField
	{
		public Guid FieldId { get; set; }

		/// <summary>
		/// Gets or sets the type key of the (element) value: String, Double, Int64, Int32, Boolean, DateTime, TimeSpan or Guid.
		/// </summary>
		public string ValueType { get; set; }

		public bool IsList { get; set; }

		/// <summary>
		/// Gets or sets the values encoded with the invariant culture (a single entry for non-list fields).
		/// </summary>
		public List<string> Values { get; set; } = new List<string>();
	}
}
