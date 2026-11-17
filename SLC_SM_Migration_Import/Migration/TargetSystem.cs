namespace SLCSMMigrationImport.Migration
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Net.Apps.DataMinerObjectModel;
	using Skyline.DataMiner.Net.Messages;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;

	/// <summary>
	/// Read access to the DOM instances on the target (new) system.
	/// </summary>
	public interface ITargetLookup
	{
		/// <summary>
		/// Loads the given instances in bulk so later <see cref="GetExisting"/> calls are served from cache.
		/// </summary>
		void Prefetch(string moduleId, IEnumerable<Guid> ids);

		/// <returns>The existing instance, or <see langword="null"/> when it does not exist.</returns>
		DomInstance GetExisting(string moduleId, Guid id);

		bool Exists(string moduleId, Guid id);
	}

	/// <summary>
	/// Checks for non-DOM objects referenced by SMG data (services and Automation scripts).
	/// </summary>
	public interface IEnvironmentChecks
	{
		bool ServiceExists(string serviceKey);

		bool ScriptExists(string scriptName);
	}

	/// <summary>
	/// Write access to the DOM instances on the target (new) system.
	/// </summary>
	public interface IDomWriter
	{
		DomInstance Create(DomInstance instance);

		DomInstance Update(DomInstance instance);

		void Delete(DomInstance instance);

		DomInstance DoStatusTransition(DomInstance instance, string transitionId);
	}

	/// <summary>
	/// The target DataMiner System: DOM read/write access and service/script checks, all cached.
	/// </summary>
	public sealed class TargetSystem : ITargetLookup, IDomWriter, IEnvironmentChecks
	{
		private const int BatchSize = 100;

		private readonly IEngine _engine;
		private readonly Dictionary<string, DomHelper> _helpers = new Dictionary<string, DomHelper>(StringComparer.OrdinalIgnoreCase);
		private readonly Dictionary<InstanceKey, DomInstance> _instances = new Dictionary<InstanceKey, DomInstance>();
		private readonly Dictionary<string, bool> _services = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
		private readonly Dictionary<string, bool> _scripts = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

		public TargetSystem(IEngine engine)
		{
			_engine = engine ?? throw new ArgumentNullException(nameof(engine));
		}

		public DomHelper GetHelper(string moduleId)
		{
			if (!_helpers.TryGetValue(moduleId, out var helper))
			{
				helper = new DomHelper(_engine.SendSLNetMessages, moduleId);
				_helpers[moduleId] = helper;
			}

			return helper;
		}

		public void Prefetch(string moduleId, IEnumerable<Guid> ids)
		{
			var toRead = ids.Distinct().Where(id => !_instances.ContainsKey(new InstanceKey(moduleId, id))).ToList();
			for (int i = 0; i < toRead.Count; i += BatchSize)
			{
				var batch = toRead.Skip(i).Take(BatchSize).ToList();
				var filter = new ORFilterElement<DomInstance>(batch.Select(id => (FilterElement<DomInstance>)DomInstanceExposers.Id.Equal(id)).ToArray());
				var found = GetHelper(moduleId).DomInstances.Read(filter).ToDictionary(d => d.ID.Id);
				foreach (Guid id in batch)
				{
					found.TryGetValue(id, out var instance);
					_instances[new InstanceKey(moduleId, id)] = instance;
				}
			}
		}

		public DomInstance GetExisting(string moduleId, Guid id)
		{
			var key = new InstanceKey(moduleId, id);
			if (!_instances.TryGetValue(key, out var instance))
			{
				instance = GetHelper(moduleId).DomInstances.Read(DomInstanceExposers.Id.Equal(id)).FirstOrDefault();
				_instances[key] = instance;
			}

			return instance;
		}

		public bool Exists(string moduleId, Guid id) => GetExisting(moduleId, id) != null;

		/// <summary>
		/// Drops the cached target instances to free memory; later lookups read from the system again.
		/// </summary>
		public void ClearCache() => _instances.Clear();

		public DomInstance Create(DomInstance instance) => Track(GetHelper(instance.ID.ModuleId).DomInstances.Create(instance));

		public DomInstance Update(DomInstance instance) => Track(GetHelper(instance.ID.ModuleId).DomInstances.Update(instance));

		public void Delete(DomInstance instance)
		{
			GetHelper(instance.ID.ModuleId).DomInstances.Delete(instance);
			_instances[new InstanceKey(instance.ID.ModuleId, instance.ID.Id)] = null;
		}

		public DomInstance DoStatusTransition(DomInstance instance, string transitionId)
		{
			return Track(GetHelper(instance.ID.ModuleId).DomInstances.DoStatusTransition(instance.ID, transitionId));
		}

		public bool ServiceExists(string serviceKey)
		{
			return Cached(_services, serviceKey, () => _engine.FindServiceByKey(serviceKey) != null);
		}

		public bool ScriptExists(string scriptName)
		{
			return Cached(_scripts, scriptName, () => _engine.SendSLNetSingleResponseMessage(new GetScriptInfoMessage(scriptName)) is GetScriptInfoResponseMessage);
		}

		private static bool Cached(Dictionary<string, bool> cache, string key, Func<bool> check)
		{
			if (!cache.TryGetValue(key, out bool exists))
			{
				try
				{
					exists = check();
				}
				catch (Exception)
				{
					exists = false;
				}

				cache[key] = exists;
			}

			return exists;
		}

		private DomInstance Track(DomInstance instance)
		{
			if (instance?.ID != null)
			{
				// Only invalidate: keeping every written instance in memory makes large imports run out of memory.
				_instances.Remove(new InstanceKey(instance.ID.ModuleId, instance.ID.Id));
			}

			return instance;
		}
	}
}
