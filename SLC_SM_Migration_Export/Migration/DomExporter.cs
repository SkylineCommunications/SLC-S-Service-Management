namespace SLCSMMigrationExport.Migration
{
	using System;
	using System.Collections;
	using System.Collections.Generic;
	using System.Globalization;
	using System.Linq;

	using Skyline.DataMiner.Net.Apps.DataMinerObjectModel;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.Net.Sections;

	/// <summary>
	/// Reads the DOM data of a module and converts it to the culture-invariant export model. Read-only.
	/// </summary>
	public static class DomExporter
	{
		private const int PageSize = 500;

		public static ExportedModule ExportModule(DomHelper helper)
		{
			var module = new ExportedModule { ModuleId = helper.ModuleId };

			module.Definitions = helper.DomDefinitions.Read(new TRUEFilterElement<DomDefinition>())
				.Select(d => new ExportedDefinitionInfo { Id = d.ID.Id, Name = d.Name })
				.ToList();

			module.SectionDefinitions = helper.SectionDefinitions.Read(new TRUEFilterElement<SectionDefinition>())
				.Select(s => new ExportedSectionDefinitionInfo
				{
					Id = s.GetID().Id,
					Name = s.GetName(),
					Fields = s.GetAllFieldDescriptors()
						.Select(f => new ExportedFieldInfo { Id = f.ID.Id, Name = f.Name, Type = f.FieldType?.ToString() })
						.ToList(),
				})
				.ToList();

			var paging = helper.DomInstances.PreparePaging(new TRUEFilterElement<DomInstance>(), PageSize);
			while (paging.MoveToNextPage())
			{
				module.Instances.AddRange(paging.GetCurrentPage().Select(ToExported));
			}

			return module;
		}

		public static ExportedInstance ToExported(DomInstance instance)
		{
			return new ExportedInstance
			{
				Id = instance.ID.Id,
				DefinitionId = instance.DomDefinitionId?.Id ?? Guid.Empty,
				Name = instance.Name,
				StatusId = instance.StatusId,
				Sections = (instance.Sections ?? new List<Section>())
					.Where(s => s?.SectionDefinitionID != null)
					.Select(s => new ExportedSection
					{
						SectionId = s.ID?.Id ?? Guid.Empty,
						SectionDefinitionId = s.SectionDefinitionID.Id,
						Fields = s.FieldValues
							.Select(f => Encode(f.FieldDescriptorID.Id, f.Value))
							.Where(f => f != null)
							.ToList(),
					})
					.ToList(),
			};
		}

		public static ExportedField Encode(Guid fieldId, IValueWrapper wrapper)
		{
			object value = wrapper?.Value;
			if (value == null)
			{
				return null;
			}

			var field = new ExportedField { FieldId = fieldId };
			if (value is IList list)
			{
				var type = value.GetType();
				field.IsList = true;
				field.ValueType = TypeKey(type.IsGenericType ? type.GetGenericArguments()[0] : list.Cast<object>().FirstOrDefault(x => x != null)?.GetType());
				field.Values.AddRange(list.Cast<object>().Select(EncodeScalar));
			}
			else
			{
				field.ValueType = TypeKey(value.GetType());
				field.Values.Add(EncodeScalar(value));
			}

			return field;
		}

		public static string EncodeScalar(object value)
		{
			switch (value)
			{
				case null:
					return null;
				case string s:
					return s;
				case double d:
					return d.ToString("R", CultureInfo.InvariantCulture);
				case float f:
					return f.ToString("R", CultureInfo.InvariantCulture);
				case bool b:
					return b ? "true" : "false";
				case DateTime dt:
					return dt.ToString("o", CultureInfo.InvariantCulture);
				case TimeSpan ts:
					return ts.ToString("c", CultureInfo.InvariantCulture);
				case Guid g:
					return g.ToString("D");
				case Enum e:
					return Convert.ToInt64(e, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
				case IFormattable formattable:
					return formattable.ToString(null, CultureInfo.InvariantCulture);
				default:
					return value.ToString();
			}
		}

		private static string TypeKey(Type type)
		{
			if (type == null)
			{
				return "String";
			}

			if (type.IsGenericType && type.Name.StartsWith("GenericEnum", StringComparison.Ordinal))
			{
				type = type.GetGenericArguments()[0];
			}

			return type.Name;
		}
	}
}
