namespace SLC_ServiceManagement.SolutionInstallerHelper.Dom
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Reflection;

	using Newtonsoft.Json;
	using Newtonsoft.Json.Linq;
	using Newtonsoft.Json.Serialization;

	using Skyline.DataMiner.Net.Sections;

	/// <summary>
	/// JSON (de)serialization of DOM configuration objects (module settings, section, DOM and behavior definitions)
	/// in the format of the shipped <c>SetupContent\DomModules</c> files, and the canonical form used to detect changes.
	/// </summary>
	internal static class DomJson
	{
		// Server-maintained audit properties: they never count as a change.
		private static readonly HashSet<string> AuditProperties = new HashSet<string>(StringComparer.Ordinal)
		{
			"SyncLastModified",
			"TrackLastModified",
			"LastModified",
			"LastModifiedBy",
			"CreatedAt",
			"CreatedBy",
		};

		private static JsonSerializerSettings Settings => new JsonSerializerSettings
		{
			TypeNameHandling = TypeNameHandling.Auto,
			SerializationBinder = new SlNetTypesSerializationBinder(),
			MaxDepth = 128,
		};

		/// <summary>
		/// Deserializes a DOM configuration object. Only SLNet types (and lists/primitives of them) are accepted as <c>$type</c>.
		/// </summary>
		public static T Deserialize<T>(string json)
		{
			// Polymorphic DOM types (actions, conditions, field descriptors) require $type; the binder restricts it to SLNetTypes.
			return (T)JsonConvert.DeserializeObject(json, typeof(T), Settings);
		}

		/// <summary>
		/// Gets the canonical JSON of a DOM configuration object: audit properties and <c>null</c> values are left out.
		/// </summary>
		public static JToken ToCanonical<T>(T value)
		{
			var token = JToken.Parse(JsonConvert.SerializeObject(value, typeof(T), Settings));
			Normalize(token);
			return token;
		}

		/// <summary>
		/// Checks whether two DOM configuration objects have the same content, ignoring the audit properties.
		/// </summary>
		public static bool AreEquivalent<T>(T first, T second)
		{
			return JToken.DeepEquals(ToCanonical(first), ToCanonical(second));
		}

		/// <summary>
		/// Creates a deep copy of a DOM configuration object.
		/// </summary>
		public static T Clone<T>(T value)
		{
			return Deserialize<T>(JsonConvert.SerializeObject(value, typeof(T), Settings));
		}

		private static void Normalize(JToken token)
		{
			if (token is JObject obj)
			{
				foreach (var property in obj.Properties().ToList())
				{
					if (AuditProperties.Contains(property.Name) || property.Value.Type == JTokenType.Null)
					{
						property.Remove();
					}
					else
					{
						Normalize(property.Value);
					}
				}
			}
			else if (token is JArray array)
			{
				foreach (var item in array)
				{
					Normalize(item);
				}
			}
		}

		private sealed class SlNetTypesSerializationBinder : ISerializationBinder
		{
			private static readonly Assembly SlNetTypes = typeof(SectionDefinition).Assembly;

			private static readonly HashSet<Type> AllowedGenericTypes = new HashSet<Type> { typeof(List<>), typeof(Dictionary<,>), typeof(Nullable<>) };

			private static readonly HashSet<Type> AllowedSystemTypes = new HashSet<Type>
			{
				typeof(string),
				typeof(decimal),
				typeof(Guid),
				typeof(DateTime),
				typeof(TimeSpan),
			};

			private readonly DefaultSerializationBinder _inner = new DefaultSerializationBinder();

			public Type BindToType(string assemblyName, string typeName)
			{
				var type = _inner.BindToType(assemblyName, typeName);
				if (!IsAllowed(type))
				{
					throw new JsonSerializationException($"Type '{typeName}, {assemblyName}' is not allowed in a DOM module definition.");
				}

				return type;
			}

			public void BindToName(Type serializedType, out string assemblyName, out string typeName)
			{
				_inner.BindToName(serializedType, out assemblyName, out typeName);
			}

			private static bool IsAllowed(Type type)
			{
				if (type.IsArray)
				{
					return IsAllowed(type.GetElementType());
				}

				if (type.IsGenericType)
				{
					var definition = type.GetGenericTypeDefinition();
					return (definition.Assembly == SlNetTypes || AllowedGenericTypes.Contains(definition))
						&& type.GetGenericArguments().All(IsAllowed);
				}

				return type.Assembly == SlNetTypes || type.IsPrimitive || AllowedSystemTypes.Contains(type);
			}
		}
	}
}
