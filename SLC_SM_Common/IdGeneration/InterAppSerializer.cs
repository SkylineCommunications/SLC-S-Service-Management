namespace SLC_SM_Common.IdGeneration
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Reflection;

	using Newtonsoft.Json;
	using Newtonsoft.Json.Serialization;

	using Skyline.DataMiner.Core.InterAppCalls.Common.Serializing;

	/// <summary>
	/// Produces the same JSON as the default InterApp serializer of Skyline.DataMiner.Core.InterAppCalls.Common.
	/// </summary>
	/// <remarks>
	/// The default serializer's binder needs the unsigned Skyline.DataMiner.Utils.SecureCoding 2.2.1, which cannot load
	/// next to the signed 2.2.3 required by the Service Management API. This serializer has no such dependency and only
	/// binds the given message types and the InterApp envelope types, so no arbitrary type can be deserialized.
	/// </remarks>
	internal sealed class InterAppSerializer : ISerializer
	{
		private readonly JsonSerializerSettings settings;

		public InterAppSerializer(IEnumerable<Type> knownTypes)
		{
			settings = new JsonSerializerSettings
			{
				SerializationBinder = new KnownTypesBinder(knownTypes),
				TypeNameHandling = TypeNameHandling.All,
				TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Full,
				MetadataPropertyHandling = MetadataPropertyHandling.ReadAhead,
				ObjectCreationHandling = ObjectCreationHandling.Replace,
				MissingMemberHandling = MissingMemberHandling.Ignore,
				ContractResolver = new ContractResolverWithPrivates(),
				PreserveReferencesHandling = PreserveReferencesHandling.Objects,
			};
		}

		public T DeserializeFromString<T>(string input)
		{
			try
			{
				return JsonConvert.DeserializeObject<T>(input, settings);
			}
			catch (Exception e)
			{
				throw new ArgumentException("Cannot deserialize the InterApp data.", nameof(input), e);
			}
		}

		public string SerializeToString(object input)
		{
			return JsonConvert.SerializeObject(input, settings);
		}

		private sealed class KnownTypesBinder : ISerializationBinder
		{
			private static readonly Assembly InterAppAssembly = typeof(ISerializer).Assembly;
			private readonly List<Type> knownTypes;

			public KnownTypesBinder(IEnumerable<Type> knownTypes)
			{
				this.knownTypes = (knownTypes ?? throw new ArgumentNullException(nameof(knownTypes))).ToList();
				if (this.knownTypes.GroupBy(t => t.Name).Any(g => g.Count() > 1))
				{
					throw new ArgumentException("Known type names must be unique.", nameof(knownTypes));
				}
			}

			public void BindToName(Type serializedType, out string assemblyName, out string typeName)
			{
				assemblyName = String.Empty;
				typeName = knownTypes.Contains(serializedType) ? serializedType.Name : serializedType.ToString();
			}

			public Type BindToType(string assemblyName, string typeName)
			{
				Type type = knownTypes.FirstOrDefault(t => t.Name == typeName || t.ToString() == typeName)
					?? InterAppAssembly.GetTypes().FirstOrDefault(t => t.FullName == typeName);

				return type ?? throw new InvalidOperationException($"InterApp type '{typeName}' is not allowed.");
			}
		}

		private sealed class ContractResolverWithPrivates : CamelCasePropertyNamesContractResolver
		{
			protected override JsonDictionaryContract CreateDictionaryContract(Type objectType)
			{
				JsonDictionaryContract contract = base.CreateDictionaryContract(objectType);
				contract.DictionaryKeyResolver = key => key;
				return contract;
			}

			protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
			{
				return base.CreateProperties(type, memberSerialization).OrderBy(p => p.PropertyName).ToList();
			}

			protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
			{
				JsonProperty property = base.CreateProperty(member, memberSerialization);
				if (!property.Writable && member is PropertyInfo propertyInfo)
				{
					property.Writable = propertyInfo.GetSetMethod(nonPublic: true) != null;
				}

				return property;
			}
		}
	}
}
