/*
****************************************************************************
*  Copyright (c),  Skyline Communications NV  All Rights Reserved.    *
****************************************************************************

Revision History:

DATE        VERSION        AUTHOR            COMMENTS

02/09/2026  1.0.0.1        SKA               Initial version
04/09/2026  1.0.0.2        SKA               Removed Plan API dependency and resolved Resource Pools through ResourceStudioHelper.

This script creates Workflow templates as DOM instances in the '(slc)workflow'
module using the public MediaOps Workflow helper (Skyline.DataMiner.Utils.
MediaOps.Helpers.Workflows.WorkflowHelper). This is the same proven DOM-based
creation path used by the 'Workflows_General_DemoSetup' demo script; it does
NOT use the MediaOps Plan API to create the workflows (the Plan-API workflow
creation does not work on a real DataMiner Agent).

Each workflow template from workflows.json is created with one Resource Pool
node per template node (the pool is resolved by name to its DOM instance id
through the Plan API) and with the template connections. Existing workflows
(matched by name) are skipped.
****************************************************************************
*/
namespace SLC_SM_Import_Workflows
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.IO;
	using System.Linq;

	using Newtonsoft.Json;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.SecureCoding.SecureIO;
	using Skyline.DataMiner.Utils.SecureCoding.SecureSerialization.Json.Newtonsoft;

	using Rs = Skyline.DataMiner.Utils.MediaOps.Helpers.ResourceStudio;
	using Wf = Skyline.DataMiner.Utils.MediaOps.Helpers.Workflows;

	/// <summary>
	/// Represents a DataMiner Automation script.
	/// </summary>
	public class Script
	{
		private const string DefaultJsonPath = @"C:\Skyline DataMiner\Documents\DMA_COMMON_DOCUMENTS\workflows.json";

		private const string DefaultResourceStudioJsonPath = @"C:\Skyline DataMiner\Documents\DMA_COMMON_DOCUMENTS\resource-studio.json";

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
			string jsonPath = ReadScriptParam(engine, "JSON File Path");
			if (String.IsNullOrWhiteSpace(jsonPath))
			{
				jsonPath = DefaultJsonPath;
			}

			if (!jsonPath.IsPathValid())
			{
				throw new ArgumentException($"Workflows import path is not a valid path: '{jsonPath}'");
			}

			if (!File.Exists(jsonPath))
			{
				throw new FileNotFoundException($"JSON import file was not found: '{jsonPath}'");
			}

			var root = SecureNewtonsoftDeserialization.DeserializeObject<WorkflowsRoot>(File.ReadAllText(jsonPath));
			if (root == null)
			{
				throw new InvalidOperationException($"Could not deserialize workflows payload from '{jsonPath}'.");
			}

			string resourceStudioPath = ReadScriptParam(engine, "Resource Studio JSON File Path");
			if (String.IsNullOrWhiteSpace(resourceStudioPath))
			{
				resourceStudioPath = DefaultResourceStudioJsonPath;
			}

			ResourceStudioRoot resourceStudio = LoadResourceStudio(engine, resourceStudioPath);
			ImportData importData = Transform(engine, root, resourceStudio);

			var importer = new WorkflowImporter(engine);
			int imported = importer.Import(importData);

			int nodeCount = importData.Workflows.Sum(w => w.Nodes.Count);
			int connectionCount = importData.Workflows.Sum(w => w.Connections.Count);

			engine.GenerateInformation(
				"[Import Workflows] Completed. " +
				$"Workflows imported: {imported}. " +
				$"Nodes: {nodeCount}. " +
				$"Connections: {connectionCount}. " +
				$"Source file: {jsonPath}");

			if (importer.Warnings.Count > 0)
			{
				engine.GenerateInformation($"[Import Workflows] Warnings ({importer.Warnings.Count}): {String.Join(" | ", importer.Warnings.Take(25))}");
			}
		}

		private static ResourceStudioRoot LoadResourceStudio(IEngine engine, string path)
		{
			if (String.IsNullOrWhiteSpace(path) || !path.IsPathValid() || !File.Exists(path))
			{
				engine.GenerateInformation(
					$"[Import Workflows] WRN: Resource Studio JSON not found at '{path}'. Resource pool references will be skipped.");
				return new ResourceStudioRoot();
			}

			var studio = SecureNewtonsoftDeserialization.DeserializeObject<ResourceStudioRoot>(File.ReadAllText(path));
			if (studio == null)
			{
				engine.GenerateInformation(
					$"[Import Workflows] WRN: could not deserialize Resource Studio JSON at '{path}'. References will be skipped.");
				return new ResourceStudioRoot();
			}

			return studio;
		}

		private static ImportData Transform(IEngine engine, WorkflowsRoot root, ResourceStudioRoot studio)
		{
			var poolNamesById = BuildNameMap(studio.ResourcePools?.Select(p => (p?.Id, p?.Name)));

			var importData = new ImportData
			{
				Workflows = new List<ImportWorkflow>(),
			};

			foreach (var template in root.WorkflowTemplates ?? Enumerable.Empty<WorkflowTemplate>())
			{
				if (template == null || String.IsNullOrWhiteSpace(template.Name))
				{
					continue;
				}

				var nodes = (template.Nodes ?? new List<WorkflowNodeImport>()).Where(n => n != null).ToList();
				var nodeNamesById = BuildNameMap(nodes.Select(n => (n.Id, n.Name)));

				var workflow = new ImportWorkflow
				{
					Name = template.Name,
					Nodes = new List<ImportNode>(),
					Connections = new List<ImportConnection>(),
				};

				foreach (var node in nodes)
				{
					var importNode = new ImportNode
					{
						OptionalAlias = node.Name,
					};

					if (!String.IsNullOrWhiteSpace(node.ResourcePoolId))
					{
						if (poolNamesById.TryGetValue(node.ResourcePoolId, out string poolName))
						{
							importNode.ResourcePool = poolName;
						}
						else
						{
							engine.GenerateInformation(
								$"[Import Workflows] WRN: unresolved resource pool id '{node.ResourcePoolId}' on node '{node.Name}' in workflow '{template.Name}'.");
						}
					}

					workflow.Nodes.Add(importNode);
				}

				foreach (var connection in template.Connections ?? Enumerable.Empty<WorkflowConnection>())
				{
					if (connection == null)
					{
						continue;
					}

					if (!nodeNamesById.TryGetValue(connection.FromNodeId ?? String.Empty, out string source))
					{
						engine.GenerateInformation(
							$"[Import Workflows] WRN: unresolved connection source node id '{connection.FromNodeId}' in workflow '{template.Name}'. Skipping.");
						continue;
					}

					if (!nodeNamesById.TryGetValue(connection.ToNodeId ?? String.Empty, out string destination))
					{
						engine.GenerateInformation(
							$"[Import Workflows] WRN: unresolved connection destination node id '{connection.ToNodeId}' in workflow '{template.Name}'. Skipping.");
						continue;
					}

					workflow.Connections.Add(new ImportConnection
					{
						SourceNode = source,
						DestinationNode = destination,
					});
				}

				importData.Workflows.Add(workflow);
			}

			return importData;
		}

		private static Dictionary<string, string> BuildNameMap(IEnumerable<(string Id, string Name)> definitions)
		{
			var map = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
			foreach (var definition in definitions ?? Enumerable.Empty<(string, string)>())
			{
				if (!String.IsNullOrWhiteSpace(definition.Id) && !map.ContainsKey(definition.Id))
				{
					map[definition.Id] = definition.Name;
				}
			}

			return map;
		}

		/// <summary>
		/// Creates Workflow DOM instances through the public MediaOps Workflow helper, mirroring the
		/// 'Workflows_General_DemoSetup' demo creation logic (GetOrAddWorkflow / AddNodeToWorkflow /
		/// AddConnectionToWorkflow). Resource pools are resolved by name through the public
		/// ResourceStudioHelper API.
		/// </summary>
		private sealed class WorkflowImporter
		{
			private readonly Wf.WorkflowHelper workflowHelper;
			private readonly Dictionary<string, Guid> resourcePoolIdsByName;
			private readonly HashSet<string> existingWorkflowNames;

			public WorkflowImporter(IEngine engine)
			{
				if (engine == null)
				{
					throw new ArgumentNullException(nameof(engine));
				}

				workflowHelper = new Wf.WorkflowHelper(engine);

				resourcePoolIdsByName = new Dictionary<string, Guid>(StringComparer.InvariantCultureIgnoreCase);
				try
				{
					var resourceStudioHelper = new Rs.ResourceStudioHelper(engine);
					foreach (var pool in resourceStudioHelper.GetAllResourcePools())
					{
						if (pool != null && !String.IsNullOrWhiteSpace(pool.Name) && !resourcePoolIdsByName.ContainsKey(pool.Name))
						{
							resourcePoolIdsByName[pool.Name] = pool.Id;
						}
					}
				}
				catch (Exception e)
				{
					Warnings.Add($"Could not read resource pools from Resource Studio: {e.Message}. Node pool references will be empty.");
				}

				existingWorkflowNames = new HashSet<string>(
					workflowHelper.GetAllWorkflows()
						.Where(w => w != null && !String.IsNullOrWhiteSpace(w.Name))
						.Select(w => w.Name),
					StringComparer.InvariantCultureIgnoreCase);
			}

			public List<string> Warnings { get; } = new List<string>();

			public int Import(ImportData importData)
			{
				int imported = 0;
				foreach (var source in importData?.Workflows ?? new List<ImportWorkflow>())
				{
					if (source == null || String.IsNullOrWhiteSpace(source.Name))
					{
						continue;
					}

					if (existingWorkflowNames.Contains(source.Name))
					{
						continue;
					}

					try
					{
						CreateWorkflow(source);
						existingWorkflowNames.Add(source.Name);
						imported++;
					}
					catch (Exception e)
					{
						Warnings.Add($"Workflow '{source.Name}' was skipped: {e.Message}");
					}
				}

				return imported;
			}

			private void CreateWorkflow(ImportWorkflow source)
			{
				var configuration = new Wf.WorkflowConfiguration
				{
					Name = source.Name,
					Nodes = new List<Wf.NodeConfiguration>(),
					Connections = new List<Wf.ConnectionConfiguration>(),
				};

				var nodeIdsByAlias = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
				int nodeIndex = 0;
				foreach (var node in source.Nodes ?? new List<ImportNode>())
				{
					nodeIndex++;
					string nodeId = nodeIndex.ToString(CultureInfo.InvariantCulture);

					var nodeConfiguration = new Wf.NodeConfiguration
					{
						NodeId = nodeId,
						Alias = node.OptionalAlias,
						NodeType = Wf.NodeType.ResourcePool,
					};

					if (!String.IsNullOrWhiteSpace(node.ResourcePool)
						&& resourcePoolIdsByName.TryGetValue(node.ResourcePool, out Guid poolId))
					{
						nodeConfiguration.ReferenceId = poolId;
					}
					else if (!String.IsNullOrWhiteSpace(node.ResourcePool))
					{
						Warnings.Add($"Workflow '{source.Name}': resource pool '{node.ResourcePool}' was not found; node '{node.OptionalAlias}' has no pool reference.");
					}

					configuration.Nodes.Add(nodeConfiguration);

					if (!String.IsNullOrWhiteSpace(node.OptionalAlias) && !nodeIdsByAlias.ContainsKey(node.OptionalAlias))
					{
						nodeIdsByAlias[node.OptionalAlias] = nodeId;
					}
				}

				int connectionIndex = 0;
				foreach (var connection in source.Connections ?? new List<ImportConnection>())
				{
					if (connection == null
						|| String.IsNullOrWhiteSpace(connection.SourceNode)
						|| String.IsNullOrWhiteSpace(connection.DestinationNode))
					{
						continue;
					}

					if (!nodeIdsByAlias.TryGetValue(connection.SourceNode, out string sourceNodeId)
						|| !nodeIdsByAlias.TryGetValue(connection.DestinationNode, out string destinationNodeId))
					{
						continue;
					}

					connectionIndex++;
					configuration.Connections.Add(new Wf.ConnectionConfiguration
					{
						ConnectionId = connectionIndex.ToString(CultureInfo.InvariantCulture),
						SourceNodeId = sourceNodeId,
						DestinationNodeId = destinationNodeId,
					});
				}

				workflowHelper.CreateWorkflow(configuration);
			}
		}

		private sealed class ImportData
		{
			public List<ImportWorkflow> Workflows { get; set; }
		}

		private sealed class ImportWorkflow
		{
			public string Name { get; set; }

			public List<ImportNode> Nodes { get; set; }

			public List<ImportConnection> Connections { get; set; }
		}

		private sealed class ImportNode
		{
			public string OptionalAlias { get; set; }

			public string ResourcePool { get; set; }
		}

		private sealed class ImportConnection
		{
			public string SourceNode { get; set; }

			public string DestinationNode { get; set; }
		}

		private sealed class WorkflowsRoot
		{
			[JsonProperty("workflowTemplates")]
			public List<WorkflowTemplate> WorkflowTemplates { get; set; }
		}

		private sealed class WorkflowTemplate
		{
			[JsonProperty("id")]
			public string Id { get; set; }

			[JsonProperty("name")]
			public string Name { get; set; }

			[JsonProperty("nodes")]
			public List<WorkflowNodeImport> Nodes { get; set; }

			[JsonProperty("connections")]
			public List<WorkflowConnection> Connections { get; set; }
		}

		private sealed class WorkflowNodeImport
		{
			[JsonProperty("id")]
			public string Id { get; set; }

			[JsonProperty("name")]
			public string Name { get; set; }

			[JsonProperty("resourcePoolId")]
			public string ResourcePoolId { get; set; }

			[JsonProperty("requiredCapabilities")]
			public List<RequiredCapability> RequiredCapabilities { get; set; }

			[JsonProperty("capacityRequirements")]
			public List<CapacityRequirement> CapacityRequirements { get; set; }
		}

		private sealed class RequiredCapability
		{
			[JsonProperty("capabilityId")]
			public string CapabilityId { get; set; }

			[JsonProperty("value")]
			public string Value { get; set; }

			[JsonProperty("derivedFromParameterId")]
			public string DerivedFromParameterId { get; set; }
		}

		private sealed class CapacityRequirement
		{
			[JsonProperty("capacityId")]
			public string CapacityId { get; set; }

			[JsonProperty("amount")]
			public double? Amount { get; set; }

			[JsonProperty("derivedFromParameterId")]
			public string DerivedFromParameterId { get; set; }
		}

		private sealed class WorkflowConnection
		{
			[JsonProperty("fromNodeId")]
			public string FromNodeId { get; set; }

			[JsonProperty("toNodeId")]
			public string ToNodeId { get; set; }
		}

		private sealed class ResourceStudioRoot
		{
			[JsonProperty("resourcePools")]
			public List<ResourcePoolModel> ResourcePools { get; set; }
		}

		private sealed class ResourcePoolModel
		{
			[JsonProperty("id")]
			public string Id { get; set; }

			[JsonProperty("name")]
			public string Name { get; set; }
		}
	}
}
