namespace SLCSMMigrationImport.Migration
{
	using System;
	using System.Collections;
	using System.Collections.Generic;
	using System.Globalization;
	using System.Linq;

	using Skyline.DataMiner.Net.Sections;

	/// <summary>
	/// Converts DOM field values to and from the culture-invariant representation used in the export file.
	/// </summary>
	public static class ValueCodec
	{
		/// <summary>
		/// Decodes an exported value into a value wrapper matching the type of the target field descriptor.
		/// </summary>
		/// <returns><see langword="true"/> when decoding succeeded; <paramref name="wrapper"/> is <see langword="null"/> when there is no value to store.</returns>
		public static bool TryDecode(ExportedField field, FieldDescriptor target, out IValueWrapper wrapper, out string error)
		{
			wrapper = null;
			error = null;

			if (field == null || target == null || target.FieldType == null)
			{
				error = "Missing field or target field descriptor.";
				return false;
			}

			Type fieldType = target.FieldType;
			bool expectList = TryGetListElementType(fieldType, out Type elementType);
			Type scalarType = UnwrapGenericEnum(expectList ? elementType : fieldType);
			List<string> values = field.Values ?? new List<string>();

			try
			{
				if (expectList)
				{
					var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(scalarType));
					foreach (string raw in values.Where(v => v != null))
					{
						list.Add(ParseScalar(raw, scalarType));
					}

					wrapper = ValueWrapperFactory.Create(list);
				}
				else
				{
					var nonNull = values.Where(v => v != null).ToList();
					if (nonNull.Count == 0)
					{
						return true;
					}

					if (nonNull.Count > 1)
					{
						error = $"Field holds {nonNull.Count} values but the new field only accepts a single value.";
						return false;
					}

					wrapper = ValueWrapperFactory.Create(ParseScalar(nonNull[0], scalarType));
				}
			}
			catch (Exception ex) when (ex is FormatException || ex is OverflowException || ex is InvalidCastException || ex is NotSupportedException)
			{
				error = $"Value '{String.Join(", ", values)}' ({field.ValueType}) cannot be converted to {Describe(fieldType)}: {ex.Message}";
				return false;
			}

			if (!IsValid(target, wrapper))
			{
				error = target is GenericEnumFieldDescriptor
					? $"Value '{String.Join(", ", values)}' is not a valid option of the new enum field."
					: $"Value '{String.Join(", ", values)}' is not valid for the new field type {Describe(fieldType)}.";
				wrapper = null;
				return false;
			}

			return true;
		}

		/// <summary>
		/// Builds a canonical string for a field value, used to detect unchanged instances.
		/// </summary>
		public static string Canonical(IValueWrapper wrapper)
		{
			object value = wrapper?.Value;
			if (value == null)
			{
				return String.Empty;
			}

			if (value is IList list)
			{
				return "[" + String.Join("|", list.Cast<object>().Select(EncodeScalar)) + "]";
			}

			return EncodeScalar(value);
		}

		private static string EncodeScalar(object value)
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
			return type == null ? "String" : UnwrapGenericEnum(type).Name;
		}

		private static bool TryGetListElementType(Type type, out Type elementType)
		{
			elementType = null;
			if (type != null && type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
			{
				elementType = type.GetGenericArguments()[0];
				return true;
			}

			return false;
		}

		private static Type UnwrapGenericEnum(Type type)
		{
			// GenericEnum<T> fields store the underlying T value.
			if (type != null && type.IsGenericType && type.Name.StartsWith("GenericEnum", StringComparison.Ordinal))
			{
				return type.GetGenericArguments()[0];
			}

			return type;
		}

		private static string Describe(Type type)
		{
			if (type == null)
			{
				return "unknown";
			}

			if (TryGetListElementType(type, out Type element))
			{
				return $"List<{TypeKey(element)}>";
			}

			return TypeKey(type);
		}

		private static bool IsValid(FieldDescriptor target, IValueWrapper wrapper)
		{
			if (wrapper == null)
			{
				return true;
			}

			if (target is GenericEnumFieldDescriptor)
			{
				return target.IsValueOfValidType(wrapper);
			}

			// The base validation only knows the generic supported types, so also accept an exact type match.
			return wrapper.Type == target.FieldType || target.IsValueOfValidType(wrapper);
		}

		private static object ParseScalar(string raw, Type type)
		{
			if (type == typeof(string))
			{
				return raw;
			}

			string s = raw.Trim();
			if (type == typeof(Guid))
			{
				return Guid.Parse(s);
			}

			if (type == typeof(double))
			{
				return Double.Parse(s, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture);
			}

			if (type == typeof(long) || type == typeof(int))
			{
				if (!Int64.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out long l))
				{
					double d = Double.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);
					if (Math.Abs(d % 1) > Double.Epsilon)
					{
						throw new FormatException($"'{s}' is not a whole number.");
					}

					l = Convert.ToInt64(d);
				}

				return type == typeof(int) ? (object)checked((int)l) : l;
			}

			if (type == typeof(bool))
			{
				if (s == "1")
				{
					return true;
				}

				if (s == "0")
				{
					return false;
				}

				return Boolean.Parse(s);
			}

			if (type == typeof(DateTime))
			{
				return DateTime.Parse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
			}

			if (type == typeof(TimeSpan))
			{
				return TimeSpan.Parse(s, CultureInfo.InvariantCulture);
			}

			throw new NotSupportedException($"Field type {type.Name} is not supported by the migration.");
		}
	}
}
