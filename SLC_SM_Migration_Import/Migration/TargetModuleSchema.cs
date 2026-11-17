namespace SLCSMMigrationImport.Migration
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	using Skyline.DataMiner.Net.Apps.DataMinerObjectModel;
	using Skyline.DataMiner.Net.Apps.DataMinerObjectModel.Status;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.Net.Sections;

	/// <summary>
	/// DOM definitions, section definitions and behaviors as they exist on the target (new) system.
	/// </summary>
	public sealed class TargetModuleSchema
	{
		private readonly Dictionary<Guid, DomDefinition> _definitions;
		private readonly Dictionary<Guid, SectionDefinition> _sections;
		private readonly Dictionary<Guid, DomBehaviorDefinition> _behaviors;

		public TargetModuleSchema(string moduleId, IEnumerable<DomDefinition> definitions, IEnumerable<SectionDefinition> sections, IEnumerable<DomBehaviorDefinition> behaviors)
		{
			ModuleId = moduleId ?? throw new ArgumentNullException(nameof(moduleId));

			// Legacy parts still present on an in-place upgraded system are ignored, so a dry run plans as if the cleanup already ran.
			_definitions = (definitions ?? Enumerable.Empty<DomDefinition>()).Where(d => !KnownIds.RemovedDefinitions.ContainsKey(d.ID.Id)).ToDictionary(d => d.ID.Id);
			_sections = (sections ?? Enumerable.Empty<SectionDefinition>()).Where(s => !KnownIds.RemovedSections.ContainsKey(s.GetID().Id)).ToDictionary(s => s.GetID().Id);
			_behaviors = (behaviors ?? Enumerable.Empty<DomBehaviorDefinition>()).ToDictionary(b => b.ID.Id);
		}

		public string ModuleId { get; }

		public bool IsEmpty => _definitions.Count == 0;

		public IEnumerable<DomDefinition> Definitions => _definitions.Values;

		public static TargetModuleSchema Load(DomHelper helper)
		{
			return new TargetModuleSchema(
				helper.ModuleId,
				helper.DomDefinitions.Read(new TRUEFilterElement<DomDefinition>()),
				helper.SectionDefinitions.Read(new TRUEFilterElement<SectionDefinition>()),
				helper.DomBehaviorDefinitions.Read(new TRUEFilterElement<DomBehaviorDefinition>()));
		}

		public DomDefinition GetDefinition(Guid id)
		{
			return _definitions.TryGetValue(id, out var definition) ? definition : null;
		}

		public SectionDefinition GetSectionDefinition(Guid id)
		{
			return _sections.TryGetValue(id, out var section) ? section : null;
		}

		public bool IsSectionLinked(DomDefinition definition, Guid sectionDefinitionId)
		{
			return definition?.SectionDefinitionLinks != null
				&& definition.SectionDefinitionLinks.Any(l => !l.IsSoftDeleted && l.SectionDefinitionID?.Id == sectionDefinitionId);
		}

		public FieldDescriptor GetField(Guid sectionDefinitionId, Guid fieldId)
		{
			var section = GetSectionDefinition(sectionDefinitionId);
			return section?.GetAllFieldDescriptors()
				.FirstOrDefault(f => !f.IsSoftDeleted && f.ID.Id == fieldId && !KnownIds.RemovedFields.ContainsKey(fieldId));
		}

		public IEnumerable<FieldDescriptor> GetFields(DomDefinition definition)
		{
			if (definition?.SectionDefinitionLinks == null)
			{
				return Enumerable.Empty<FieldDescriptor>();
			}

			return definition.SectionDefinitionLinks
				.Where(l => !l.IsSoftDeleted && l.SectionDefinitionID != null)
				.Select(l => GetSectionDefinition(l.SectionDefinitionID.Id))
				.Where(s => s != null)
				.SelectMany(s => s.GetAllFieldDescriptors())
				.Where(f => !f.IsSoftDeleted && !KnownIds.RemovedFields.ContainsKey(f.ID.Id));
		}

		/// <summary>
		/// Gets the behavior of a definition, including statuses and transitions inherited from parent behaviors.
		/// </summary>
		public BehaviorInfo GetBehavior(DomDefinition definition)
		{
			if (definition?.DomBehaviorDefinitionId == null || definition.DomBehaviorDefinitionId.Id == Guid.Empty)
			{
				return null;
			}

			var statuses = new List<DomStatus>();
			var transitions = new List<DomStatusTransition>();
			string initial = null;
			var visited = new HashSet<Guid>();
			Guid? current = definition.DomBehaviorDefinitionId.Id;

			while (current.HasValue && visited.Add(current.Value) && _behaviors.TryGetValue(current.Value, out var behavior))
			{
				initial = initial ?? behavior.InitialStatusId;
				statuses.AddRange((behavior.Statuses ?? new List<DomStatus>()).Where(s => statuses.All(x => x.Id != s.Id)));
				transitions.AddRange((behavior.StatusTransitions ?? new List<DomStatusTransition>()).Where(t => transitions.All(x => x.Id != t.Id)));
				current = behavior.ParentId?.Id;
			}

			if (visited.Count == 0 || !_behaviors.ContainsKey(definition.DomBehaviorDefinitionId.Id))
			{
				return null;
			}

			return new BehaviorInfo(initial, statuses, transitions);
		}
	}

	public sealed class BehaviorInfo
	{
		public BehaviorInfo(string initialStatusId, IReadOnlyList<DomStatus> statuses, IReadOnlyList<DomStatusTransition> transitions)
		{
			InitialStatusId = initialStatusId;
			Statuses = statuses;
			Transitions = transitions;
		}

		public string InitialStatusId { get; }

		public IReadOnlyList<DomStatus> Statuses { get; }

		public IReadOnlyList<DomStatusTransition> Transitions { get; }

		public bool HasStatus(string statusId)
		{
			return Statuses.Any(s => String.Equals(s.Id, statusId, StringComparison.Ordinal));
		}

		/// <summary>
		/// Finds the shortest chain of transitions between two statuses (breadth-first).
		/// </summary>
		/// <returns>The transitions to apply in order; an empty list when already in the target status; <see langword="null"/> when unreachable.</returns>
		public List<DomStatusTransition> FindPath(string fromStatusId, string toStatusId)
		{
			var visited = new HashSet<string>(StringComparer.Ordinal) { fromStatusId };
			var queue = new Queue<KeyValuePair<string, List<DomStatusTransition>>>();
			queue.Enqueue(new KeyValuePair<string, List<DomStatusTransition>>(fromStatusId, new List<DomStatusTransition>()));

			while (queue.Count > 0)
			{
				var current = queue.Dequeue();
				if (String.Equals(current.Key, toStatusId, StringComparison.Ordinal))
				{
					return current.Value;
				}

				foreach (var transition in Transitions.Where(t => t != null && String.Equals(t.FromStatusId, current.Key, StringComparison.Ordinal)))
				{
					if (visited.Add(transition.ToStatusId))
					{
						queue.Enqueue(new KeyValuePair<string, List<DomStatusTransition>>(transition.ToStatusId, new List<DomStatusTransition>(current.Value) { transition }));
					}
				}
			}

			return null;
		}
	}
}
