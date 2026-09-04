/*
****************************************************************************
*  Copyright (c),  Skyline Communications NV  All Rights Reserved.    *
****************************************************************************

Revision History:

DATE        VERSION        AUTHOR            COMMENTS

02/09/2026  1.0.0.1        SKA               Initial version
03/09/2026  1.0.0.2        SKA               Reworked to the DOM/SRM Resource Studio API (MediaOps.Temp.Helpers) to drop the SDM.Abstractions solution-library dependency.
04/09/2026  1.0.0.3        SKA               Switched to the public ResourceStudioHelper facade (MediaOps.Temp.Helpers 1.4.1-alpha19) - no SDM.Abstractions, no internal DOM/SRM types.
****************************************************************************
*/
namespace SLC_SM_Import_Resource_Studio
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;

	using Newtonsoft.Json;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.MediaOps.Helpers.ResourceStudio;
	using Skyline.DataMiner.Utils.SecureCoding.SecureIO;
	using Skyline.DataMiner.Utils.SecureCoding.SecureSerialization.Json.Newtonsoft;

	/// <summary>
	/// Represents a DataMiner Automation script.
	/// </summary>
	public class Script
	{
		private const string DefaultJsonPath = @"C:\Skyline DataMiner\Documents\DMA_COMMON_DOCUMENTS\resource-studio.json";

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
			}
			catch (ScriptForceAbortException)
			{
			}
			catch (ScriptTimeoutException)
			{
			}
			catch (InteractiveUserDetachedException)
			{
			}
			catch (Exception e)
			{
				engine.ExitFail($"Run|{e.Message}");
			}
		}

		private static string ReadScriptParam(IEngine engine, string name)
		{
			return engine.GetScriptParam(name)?.Value ?? String.Empty;
		}

		private static void RunSafe(IEngine engine)
		{
			TryRunResourceStudioGeneralSetup(engine);

			string jsonPath = ReadScriptParam(engine, "JSON File Path");
			if (String.IsNullOrWhiteSpace(jsonPath))
			{
				jsonPath = DefaultJsonPath;
			}

			if (!jsonPath.IsPathValid())
			{
				throw new ArgumentException($"JSON import path is not a valid path: '{jsonPath}'");
			}

			if (!File.Exists(jsonPath))
			{
				throw new FileNotFoundException($"JSON import file was not found: '{jsonPath}'");
			}

			var root = SecureNewtonsoftDeserialization.DeserializeObject<ResourceStudioRoot>(File.ReadAllText(jsonPath));
			if (root == null)
			{
				throw new InvalidOperationException($"Could not deserialize resource studio payload from '{jsonPath}'.");
			}

			var importer = new ResourceStudioImporter(engine);
			ImportResult result = importer.Import(root);

			engine.GenerateInformation(
				"[Import Resource Studio] Completed. " +
				$"Capabilities C/U: {result.CapabilitiesCreatedOrUpdated}. " +
				$"Capacities C/U: {result.CapacitiesCreatedOrUpdated}. " +
				$"Resources created: {result.ResourcesCreated}. " +
				$"Pools created: {result.PoolsCreated}. " +
				$"Source file: {jsonPath}.");

			if (result.Warnings != null && result.Warnings.Count > 0)
			{
				engine.GenerateInformation($"[Import Resource Studio] Warnings ({result.Warnings.Count}): {String.Join(" | ", result.Warnings.Take(25))}");
			}
		}

		private static void TryRunResourceStudioGeneralSetup(IEngine engine)
		{
			try
			{
				var subScript = engine.PrepareSubScript("ResourceStudio_General_Setup");
				subScript.Synchronous = true;
				subScript.InheritScriptOutput = true;
				subScript.StartScript();

				if (subScript.HadError)
				{
					engine.GenerateInformation(
						"[Import Resource Studio] WRN: 'ResourceStudio_General_Setup' reported an error. " +
						"The MediaOps base setup (module '(slc)standard_data_model') may not be fully configured, " +
						"which can cause resources/capabilities to be skipped. Details: " +
						String.Join(" | ", subScript.GetErrorMessages() ?? new string[0]));
				}
			}
			catch (Exception e)
			{
				engine.GenerateInformation(
					"[Import Resource Studio] WRN: Could not run 'ResourceStudio_General_Setup' (is MediaOps installed on this DMA?): " + e.Message);
			}
		}

		private sealed class ResourceStudioImporter
		{
			private readonly IEngine engine;
			private readonly ResourceStudioHelper helper;
			private readonly List<string> warnings = new List<string>();

			private readonly Dictionary<string, Capability> capabilityBySourceId = new Dictionary<string, Capability>(StringComparer.InvariantCultureIgnoreCase);
			private readonly Dictionary<string, Capacity> capacityBySourceId = new Dictionary<string, Capacity>(StringComparer.InvariantCultureIgnoreCase);
			private readonly Dictionary<string, Resource> resourcesByName = new Dictionary<string, Resource>(StringComparer.InvariantCultureIgnoreCase);

			private Dictionary<string, Capability> existingCapabilitiesByName = new Dictionary<string, Capability>(StringComparer.InvariantCultureIgnoreCase);
			private Dictionary<string, Capacity> existingCapacitiesByName = new Dictionary<string, Capacity>(StringComparer.InvariantCultureIgnoreCase);
			private Dictionary<string, Resource> existingResourcesByName = new Dictionary<string, Resource>(StringComparer.InvariantCultureIgnoreCase);
			private Dictionary<string, ResourcePool> existingPoolsByName = new Dictionary<string, ResourcePool>(StringComparer.InvariantCultureIgnoreCase);

			public ResourceStudioImporter(IEngine engine)
			{
				this.engine = engine ?? throw new ArgumentNullException(nameof(engine));
				helper = new ResourceStudioHelper(engine);
			}

			public ImportResult Import(ResourceStudioRoot root)
			{
				PreloadExisting();

				int capabilities = BuildCapabilities(root.CapabilityDefinitions);
				int capacities = BuildCapacities(root.CapacityDefinitions);
				int resources = BuildResources(root.ResourcePools);
				int pools = BuildResourcePools(root.ResourcePools);

				return new ImportResult
				{
					CapabilitiesCreatedOrUpdated = capabilities,
					CapacitiesCreatedOrUpdated = capacities,
					ResourcesCreated = resources,
					PoolsCreated = pools,
					Warnings = warnings,
				};
			}

			private void PreloadExisting()
			{
				existingCapabilitiesByName = DistinctByName(helper.GetAllCapabilities(), c => c.Name);
				existingCapacitiesByName = DistinctByName(helper.GetAllCapacities(), c => c.Name);
				existingResourcesByName = DistinctByName(helper.GetAllResources(), r => r.Name);
				existingPoolsByName = DistinctByName(helper.GetAllResourcePools(), p => p.Name);
			}

			private int BuildCapabilities(IEnumerable<CapabilityDefinition> definitions)
			{
				int count = 0;
				foreach (var definition in definitions ?? Enumerable.Empty<CapabilityDefinition>())
				{
					if (definition == null || String.IsNullOrWhiteSpace(definition.Name) || String.IsNullOrWhiteSpace(definition.Id))
					{
						continue;
					}

					try
					{
						var allowedValues = (definition.AllowedValues ?? new List<string>())
							.Where(v => !String.IsNullOrWhiteSpace(v))
							.Distinct(StringComparer.InvariantCultureIgnoreCase)
							.ToList();

						Capability capability;
						if (existingCapabilitiesByName.TryGetValue(definition.Name, out capability))
						{
							if (allowedValues.Count > 0)
							{
								capability = helper.UpdateCapabilityDiscretes(capability, allowedValues);
							}
						}
						else
						{
							var config = new CapabilityConfiguration { Name = definition.Name };
							foreach (var value in allowedValues)
							{
								config.Discretes.Add(value);
							}

							Guid id = helper.CreateCapability(config);
							capability = helper.GetCapability(id);
							existingCapabilitiesByName[definition.Name] = capability;
						}

						capabilityBySourceId[definition.Id] = capability;
						count++;
					}
					catch (Exception e)
					{
						warnings.Add($"Capability '{definition.Name}' was skipped: {e.Message}");
					}
				}

				return count;
			}

			private int BuildCapacities(IEnumerable<CapacityDefinition> definitions)
			{
				int count = 0;
				foreach (var definition in definitions ?? Enumerable.Empty<CapacityDefinition>())
				{
					if (definition == null || String.IsNullOrWhiteSpace(definition.Name) || String.IsNullOrWhiteSpace(definition.Id))
					{
						continue;
					}

					try
					{
						Capacity capacity;
						if (!existingCapacitiesByName.TryGetValue(definition.Name, out capacity))
						{
							var config = new CapacityConfiguration
							{
								Name = definition.Name,
								Units = definition.Unit,
							};

							Guid id = helper.CreateCapacity(config);
							capacity = helper.GetCapacity(id);
							existingCapacitiesByName[definition.Name] = capacity;
						}

						capacityBySourceId[definition.Id] = capacity;
						count++;
					}
					catch (Exception e)
					{
						warnings.Add($"Capacity '{definition.Name}' was skipped: {e.Message}");
					}
				}

				return count;
			}

			private int BuildResources(IEnumerable<ResourcePoolModel> sourcePools)
			{
				int count = 0;
				foreach (var pool in sourcePools ?? Enumerable.Empty<ResourcePoolModel>())
				{
					foreach (var sourceResource in pool?.Resources ?? Enumerable.Empty<ResourceModel>())
					{
						if (sourceResource == null || String.IsNullOrWhiteSpace(sourceResource.Name) || resourcesByName.ContainsKey(sourceResource.Name))
						{
							continue;
						}

						try
						{
							Resource resource;
							if (!existingResourcesByName.TryGetValue(sourceResource.Name, out resource))
							{
								var config = new ResourceConfiguration
								{
									Name = sourceResource.Name,
									Concurrency = 1,
								};

								Guid id = helper.CreateResource(config, new ObjectMetadata());
								resource = helper.GetResource(id);
								existingResourcesByName[sourceResource.Name] = resource;
							}

							AssignCapabilities(resource, sourceResource.Capabilities);
							AssignCapacities(resource, sourceResource.Capacities);

							resourcesByName[sourceResource.Name] = resource;
							count++;
						}
						catch (Exception e)
						{
							warnings.Add($"Resource '{sourceResource.Name}' was skipped: {e.Message}");
						}
					}
				}

				return count;
			}

			private void AssignCapabilities(Resource resource, IEnumerable<ResourceCapabilityModel> capabilities)
			{
				var resourceCapabilities = new List<ResourceCapability>();
				foreach (var capability in capabilities ?? Enumerable.Empty<ResourceCapabilityModel>())
				{
					Capability capabilityDefinition;
					if (capability == null || !capabilityBySourceId.TryGetValue(capability.CapabilityId ?? String.Empty, out capabilityDefinition))
					{
						continue;
					}

					var values = (capability.Values ?? new List<string>())
						.Where(v => !String.IsNullOrWhiteSpace(v))
						.Distinct(StringComparer.InvariantCultureIgnoreCase)
						.ToList();

					if (capabilityDefinition.Discretes != null && capabilityDefinition.Discretes.Count > 0)
					{
						values = values.Where(v => capabilityDefinition.Discretes.ContainsKey(v)).ToList();
					}

					if (values.Count == 0)
					{
						continue;
					}

					var resourceCapability = new ResourceCapability(capabilityDefinition);
					if (resourceCapability.Discretes != null)
					{
						foreach (var value in values)
						{
							resourceCapability.Discretes.Add(value);
						}
					}

					resourceCapabilities.Add(resourceCapability);
				}

				if (resourceCapabilities.Count > 0)
				{
					resource.SetCapabilities(resourceCapabilities);
				}
			}

			private void AssignCapacities(Resource resource, IEnumerable<ResourceCapacityModel> capacities)
			{
				var resourceCapacities = new List<ResourceCapacity>();
				foreach (var capacity in capacities ?? Enumerable.Empty<ResourceCapacityModel>())
				{
					Capacity capacityDefinition;
					if (capacity == null || !capacityBySourceId.TryGetValue(capacity.CapacityId ?? String.Empty, out capacityDefinition))
					{
						continue;
					}

					resourceCapacities.Add(new ResourceCapacity(capacityDefinition) { CapacityValue = capacity.TotalCapacity });
				}

				if (resourceCapacities.Count > 0)
				{
					resource.SetCapacities(resourceCapacities);
				}
			}

			private int BuildResourcePools(IEnumerable<ResourcePoolModel> sourcePools)
			{
				int count = 0;
				foreach (var sourcePool in sourcePools ?? Enumerable.Empty<ResourcePoolModel>())
				{
					if (sourcePool == null || String.IsNullOrWhiteSpace(sourcePool.Name))
					{
						continue;
					}

					try
					{
						ResourcePool resourcePool;
						if (!existingPoolsByName.TryGetValue(sourcePool.Name, out resourcePool))
						{
							var config = new ResourcePoolConfiguration
							{
								Name = sourcePool.Name,
								DesiredStatus = ResourcePoolStatus.Completed,
							};

							Guid id = helper.CreateResourcePool(config, new ObjectMetadata());
							resourcePool = helper.GetResourcePool(id);
							existingPoolsByName[sourcePool.Name] = resourcePool;
						}

						var toAssign = new List<Resource>();
						foreach (var sourceResource in sourcePool.Resources ?? Enumerable.Empty<ResourceModel>())
						{
							Resource resource;
							if (sourceResource != null && !String.IsNullOrWhiteSpace(sourceResource.Name) && resourcesByName.TryGetValue(sourceResource.Name, out resource))
							{
								toAssign.Add(resource);
							}
						}

						if (toAssign.Count > 0)
						{
							resourcePool.AssignResources(toAssign);
						}

						count++;
					}
					catch (Exception e)
					{
						warnings.Add($"Resource pool '{sourcePool.Name}' was skipped: {e.Message}");
					}
				}

				return count;
			}

			private static Dictionary<string, T> DistinctByName<T>(IEnumerable<T> source, Func<T, string> nameSelector)
			{
				var result = new Dictionary<string, T>(StringComparer.InvariantCultureIgnoreCase);
				foreach (var item in source ?? Enumerable.Empty<T>())
				{
					if (item == null)
					{
						continue;
					}

					string name = nameSelector(item);
					if (!String.IsNullOrWhiteSpace(name) && !result.ContainsKey(name))
					{
						result[name] = item;
					}
				}

				return result;
			}
		}

		private sealed class ImportResult
		{
			public int CapabilitiesCreatedOrUpdated { get; set; }

			public int CapacitiesCreatedOrUpdated { get; set; }

			public int ResourcesCreated { get; set; }

			public int PoolsCreated { get; set; }

			public IReadOnlyCollection<string> Warnings { get; set; }
		}

		private sealed class ResourceStudioRoot
		{
			[JsonProperty("capabilityDefinitions")]
			public List<CapabilityDefinition> CapabilityDefinitions { get; set; }

			[JsonProperty("capacityDefinitions")]
			public List<CapacityDefinition> CapacityDefinitions { get; set; }

			[JsonProperty("resourcePools")]
			public List<ResourcePoolModel> ResourcePools { get; set; }
		}

		private sealed class CapabilityDefinition
		{
			[JsonProperty("id")]
			public string Id { get; set; }

			[JsonProperty("name")]
			public string Name { get; set; }

			[JsonProperty("allowedValues")]
			public List<string> AllowedValues { get; set; }
		}

		private sealed class CapacityDefinition
		{
			[JsonProperty("id")]
			public string Id { get; set; }

			[JsonProperty("name")]
			public string Name { get; set; }

			[JsonProperty("unit")]
			public string Unit { get; set; }
		}

		private sealed class ResourcePoolModel
		{
			[JsonProperty("id")]
			public string Id { get; set; }

			[JsonProperty("name")]
			public string Name { get; set; }

			[JsonProperty("resources")]
			public List<ResourceModel> Resources { get; set; }
		}

		private sealed class ResourceModel
		{
			[JsonProperty("id")]
			public string Id { get; set; }

			[JsonProperty("name")]
			public string Name { get; set; }

			[JsonProperty("capabilities")]
			public List<ResourceCapabilityModel> Capabilities { get; set; }

			[JsonProperty("capacities")]
			public List<ResourceCapacityModel> Capacities { get; set; }
		}

		private sealed class ResourceCapabilityModel
		{
			[JsonProperty("capabilityId")]
			public string CapabilityId { get; set; }

			[JsonProperty("values")]
			public List<string> Values { get; set; }
		}

		private sealed class ResourceCapacityModel
		{
			[JsonProperty("capacityId")]
			public string CapacityId { get; set; }

			[JsonProperty("totalCapacity")]
			public double TotalCapacity { get; set; }
		}
	}
}
