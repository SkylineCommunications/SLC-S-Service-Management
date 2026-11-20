namespace SLC_SM_Common.IdGeneration
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Globalization;
	using System.Linq;
	using System.Threading;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Core.DataMinerSystem.Automation;
	using Skyline.DataMiner.Core.DataMinerSystem.Common;
	using Skyline.DataMiner.Core.InterAppCalls.Common.CallBulk;
	using Skyline.DataMiner.Core.InterAppCalls.Common.Shared;
	using Skyline.DataMiner.Net.Exceptions;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ApiHelpers;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;
	using Skyline.DataMiner.Utils.ServiceManagement.Common.Extensions;
	using ServiceManagementService = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement.Service;

	public interface IServiceManagementIdGenerator
	{
		string AllocateServiceId(IEngine engine);

		string AllocateServiceOrderId(IEngine engine);

		/// <summary>Returns the Service ID the next allocation would most likely return, without consuming it.</summary>
		string PeekNextServiceId(IEngine engine);

		/// <summary>Returns the Service Order ID the next allocation would most likely return, without consuming it.</summary>
		string PeekNextServiceOrderId(IEngine engine);

		void EnsureCounterElement(IEngine engine);

		void SynchronizeCounters(IEngine engine);

		void InitializeCounters(IEngine engine);
	}

	/// <summary>
	/// Allocates Service and Service Order IDs from the counters on the single Service Management Mediation element.
	/// </summary>
	/// <remarks>
	/// The element owns the counters (parameter value = next number to issue) and reserves numbers atomically through an
	/// InterApp <see cref="IdReservationRequest"/>, so concurrent scripts and UDAPI calls on any agent never get the same ID.
	/// Counters only ever move forward: deleting a Service or Service Order never lowers them, so IDs are never reused.
	/// If the element was deleted, stopped, or lost its counter values, it is recreated/started and the counter is raised
	/// past the highest existing ID. An allocated ID that already exists is skipped.
	/// </remarks>
	public sealed class ServiceManagementIdGenerator : IServiceManagementIdGenerator
	{
		private const string ProtocolName = "Skyline Service Management Mediation";
		private const string CounterElementName = "Service Management Mediation";
		private const string ElementCreationLockName = @"Global\SLC_SM_ServiceManagementCounterElement";
		private const int InterAppReceiveParameterId = 9000000;
		private const int InterAppReturnParameterId = 9000001;
		private const int ReadPageSize = 500;
		private const int MaxAllocationAttempts = 5;

		private static readonly TimeSpan ReservationTimeout = TimeSpan.FromSeconds(30);
		private static readonly InterAppSerializer Serializer = new InterAppSerializer(new[] { typeof(IdReservationRequest), typeof(IdReservationResponse) });

		private static readonly Counter ServiceCounter = new Counter("Service", "Service", "SERVICE-", "00000", parameterId: 101);
		private static readonly Counter ServiceOrderCounter = new Counter("Service Order", "ServiceOrder", "ORDER-", "000000", parameterId: 100);

		private static string cachedElementId;

		public string AllocateServiceId(IEngine engine)
		{
			ValidateEngine(engine);
			var inventory = engine.GetUserConnection().GetServiceManagementApiHelper("Service Inventory");

			return Allocate(
				engine,
				ServiceCounter,
				id => inventory.ServiceInventory.Services.Read(ServiceExposers.ServiceID.Equal(id)).Any(),
				() => HighestExistingServiceSuffix(inventory));
		}

		public string AllocateServiceOrderId(IEngine engine)
		{
			ValidateEngine(engine);
			var ordering = engine.GetUserConnection().GetServiceManagementApiHelper("Service Ordering");

			return Allocate(
				engine,
				ServiceOrderCounter,
				id => ordering.ServiceOrder.ServiceOrders.Read(ServiceOrderExposers.OrderId.Equal(id)).Any(),
				() => HighestExistingServiceOrderSuffix(ordering));
		}

		public string PeekNextServiceId(IEngine engine)
		{
			ValidateEngine(engine);
			var inventory = engine.GetUserConnection().GetServiceManagementApiHelper("Service Inventory");

			return Peek(engine, ServiceCounter, () => HighestExistingServiceSuffix(inventory));
		}

		public string PeekNextServiceOrderId(IEngine engine)
		{
			ValidateEngine(engine);
			var ordering = engine.GetUserConnection().GetServiceManagementApiHelper("Service Ordering");

			return Peek(engine, ServiceOrderCounter, () => HighestExistingServiceOrderSuffix(ordering));
		}

		public void EnsureCounterElement(IEngine engine)
		{
			ValidateEngine(engine);
			EnsureCounterElementCore(engine);
		}

		public void SynchronizeCounters(IEngine engine)
		{
			ValidateEngine(engine);

			var connection = engine.GetUserConnection();
			var inventory = connection.GetServiceManagementApiHelper("Service Inventory");
			var ordering = connection.GetServiceManagementApiHelper("Service Ordering");

			long highestServiceId = HighestExistingServiceSuffix(inventory);
			long highestServiceOrderId = HighestExistingServiceOrderSuffix(ordering);
			long serviceFloor = checked(highestServiceId + 1L);
			long serviceOrderFloor = checked(highestServiceOrderId + 1L);

			IDmsElement element = GetCounterElement(engine);
			long nextServiceId = Reserve(engine, element, ServiceCounter, serviceFloor, allocate: false);
			long nextServiceOrderId = Reserve(engine, element, ServiceOrderCounter, serviceOrderFloor, allocate: false);

			engine.Log(
				$"SynchronizeCounters|Highest existing Service suffix: {highestServiceId}, next: {nextServiceId}. " +
				$"Highest existing Service Order suffix: {highestServiceOrderId}, next: {nextServiceOrderId}.");
		}

		public void InitializeCounters(IEngine engine)
		{
			EnsureCounterElement(engine);
			SynchronizeCounters(engine);
		}

		/// <summary>Returns the initial reservation floor, recovering missing or reset counters from existing records.</summary>
		/// <param name="next">The counter's current next value, if available.</param>
		/// <param name="highestExistingSuffix">Reads the highest suffix currently stored.</param>
		/// <returns>The minimum number the first atomic reservation may issue.</returns>
		internal static long GetInitialReservationFloor(long? next, Func<long> highestExistingSuffix)
		{
			// Seed a missing/reset counter before reserving, even when its first ID was deleted.
			return !next.HasValue || next.Value <= 1
				? Math.Max(1L, checked(highestExistingSuffix() + 1L))
				: 1L;
		}

		/// <summary>Finds the highest Int64 suffix among IDs with the specified prefix.</summary>
		/// <param name="ids">The existing IDs.</param>
		/// <param name="prefix">The ID prefix to match.</param>
		/// <returns>The highest valid suffix, or zero when none exists.</returns>
		internal static long HighestSuffix(IEnumerable<string> ids, string prefix)
		{
			return (ids ?? Enumerable.Empty<string>())
				.Where(id => !String.IsNullOrWhiteSpace(id)
					&& id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
				.Select(id => Int64.TryParse(id.Substring(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out long value) ? value : 0L)
				.DefaultIfEmpty(0L)
				.Max();
		}

		/// <summary>Discovers the counter or creates it on the lowest-ID Agent, recovering a competing creation.</summary>
		/// <param name="dms">The system containing the counter.</param>
		/// <param name="log">Writes recovery diagnostics.</param>
		/// <param name="creationTimeout">The maximum time to discover the created element.</param>
		/// <returns>The running counter element.</returns>
		internal static IDmsElement FindOrCreateCounterElement(IDms dms, Action<string> log, TimeSpan creationTimeout)
		{
			var protocol = dms.GetProtocol(ProtocolName, "Production");
			var elements = dms.GetElements().Where(IsCounterElement).ToList();

			if (elements.Count > 1)
			{
				throw new InvalidOperationException($"Expected one '{ProtocolName}' element, but found {elements.Count}. Remove the duplicate elements and retry.");
			}

			IDmsElement element = elements.SingleOrDefault();
			if (element == null)
			{
				// Every caller targets the same Agent and name; never fail over creation to another Agent.
				var owner = dms.GetAgents().OrderBy(agent => agent.Id).FirstOrDefault()
					?? throw new InvalidOperationException($"No DataMiner Agent is available to host the '{CounterElementName}' element.");
				element = CreateOrDiscoverCounterElement(dms, owner, protocol, log, creationTimeout);
			}

			string productionVersion = protocol.ReferencedVersion;
			if (!IsCounterElement(element)
				|| (!String.Equals(element.Protocol.Version, "Production", StringComparison.OrdinalIgnoreCase)
					&& !String.Equals(element.Protocol.Version, productionVersion, StringComparison.OrdinalIgnoreCase)))
			{
				throw new InvalidOperationException($"The existing '{CounterElementName}' element is not using the production '{ProtocolName}' protocol.");
			}

			if (!IsRunning(element))
			{
				element.Start();
				DmsElementId elementId = element.DmsElementId;
				if (!WaitFor(() => IsRunning(dms.GetElement(elementId)), TimeSpan.FromMinutes(2)))
				{
					throw new TimeoutException($"The '{CounterElementName}' element did not become active.");
				}

				element = dms.GetElement(elementId);
			}

			return element;
		}

		private static string Allocate(IEngine engine, Counter counter, Func<string, bool> idExists, Func<long> highestExistingSuffix)
		{
			IDmsElement element = GetCounterElement(engine);
			long floor = GetInitialReservationFloor(ReadCounter(element, counter.ParameterId), highestExistingSuffix);

			for (int attempt = 0; attempt < MaxAllocationAttempts; attempt++)
			{
				long number = Reserve(engine, element, counter, floor, allocate: true);
				string id = counter.Format(number);
				if (!idExists(id))
				{
					return id;
				}

				// The counter is behind the stored records (e.g. the element was recreated): skip past them.
				engine.Log($"Allocate|{counter.Name} ID '{id}' already exists; raising the counter above the existing records.");
				floor = checked(Math.Max(number, highestExistingSuffix()) + 1L);
			}

			throw new InvalidOperationException($"Could not allocate a unique {counter.Name} ID after {MaxAllocationAttempts} attempts.");
		}

		// Preview only: nothing is reserved, so the ID returned by the real allocation on save can differ
		// when another user allocates in between.
		private static string Peek(IEngine engine, Counter counter, Func<long> highestExistingSuffix)
		{
			IDmsElement element = GetCounterElement(engine);
			long next = ReadCounter(element, counter.ParameterId) ?? 1;

			next = Math.Max(next, GetInitialReservationFloor(next, highestExistingSuffix));

			return counter.Format(next);
		}

		private static long Reserve(IEngine engine, IDmsElement element, Counter counter, long floor, bool allocate)
		{
			int agentId = element.DmsElementId.AgentId;
			int elementId = element.DmsElementId.ElementId;

			IInterAppCall call = InterAppCallFactory.CreateNew();
			call.Source = new Source("SLC_SM_Common.ServiceManagementIdGenerator");
			call.ReturnAddress = new ReturnAddress(agentId, elementId, InterAppReturnParameterId);
			call.Messages.Add(new IdReservationRequest
			{
				Kind = counter.Kind,
				Floor = floor,
				Allocate = allocate,
			});

			IdReservationResponse response = call
				.Send(engine.GetUserConnection(), agentId, elementId, InterAppReceiveParameterId, ReservationTimeout, Serializer)
				?.OfType<IdReservationResponse>()
				.FirstOrDefault()
				?? throw new TimeoutException($"The '{CounterElementName}' element did not answer the {counter.Name} ID reservation within {ReservationTimeout.TotalSeconds} seconds.");

			if (!String.IsNullOrEmpty(response.Error))
			{
				throw new InvalidOperationException($"The '{CounterElementName}' element rejected the {counter.Name} ID reservation: {response.Error}");
			}

			if (response.Value <= 0)
			{
				throw new InvalidOperationException($"The '{CounterElementName}' element returned an invalid {counter.Name} ID reservation.");
			}

			return response.Value;
		}

		private static IDmsElement GetCounterElement(IEngine engine)
		{
			IDms dms = engine.GetDms();
			string elementId = cachedElementId;
			if (elementId != null)
			{
				try
				{
					IDmsElement cached = dms.GetElement(new DmsElementId(elementId));
					if (IsCounterElement(cached) && IsRunning(cached))
					{
						return cached;
					}
				}
				catch (ElementNotFoundException)
				{
					// Element was deleted; fall through and recreate it.
				}

				cachedElementId = null;
			}

			IDmsElement element = EnsureCounterElementCore(engine);
			cachedElementId = element.DmsElementId.Value;
			return element;
		}

		private static IDmsElement EnsureCounterElementCore(IEngine engine)
		{
			// This mutex only reduces local contention; cross-Agent recovery uses one creation owner and name.
			using (ElementCreationLock.Acquire())
			{
				return FindOrCreateCounterElement(engine.GetDms(), message => engine.Log(message), TimeSpan.FromMinutes(2));
			}
		}

		private static IDmsElement CreateOrDiscoverCounterElement(
			IDms dms,
			IDma owner,
			IDmsProtocol protocol,
			Action<string> log,
			TimeSpan creationTimeout)
		{
			IDmsElement element = DiscoverCounterElement(owner);
			if (element != null)
			{
				return element;
			}

			try
			{
				owner.CreateElement(new ElementConfiguration(dms, CounterElementName, protocol));
			}
			catch (Exception ex) when (ex is DmsException || ex is DataMinerException || ex is TimeoutException)
			{
				if (WaitForCounterElement(owner, creationTimeout, out element))
				{
					log($"CreateOrDiscoverCounterElement|Creation failed on Agent {owner.Id}, but the named element is now available; using the competing creation. {ex.GetType().Name}");
					return element;
				}

				throw;
			}

			if (!WaitForCounterElement(owner, creationTimeout, out element))
			{
				throw new TimeoutException($"The '{CounterElementName}' element was not available on Agent {owner.Id} after creation.");
			}

			return element;
		}

		private static bool WaitForCounterElement(IDma owner, TimeSpan timeout, out IDmsElement element)
		{
			IDmsElement discovered = DiscoverCounterElement(owner);
			bool found = discovered != null || WaitFor(() => (discovered = DiscoverCounterElement(owner)) != null, timeout);
			element = discovered;
			return found;
		}

		private static IDmsElement DiscoverCounterElement(IDma owner)
		{
			try
			{
				return owner.ElementExists(CounterElementName) ? owner.GetElement(CounterElementName) : null;
			}
			catch (ElementNotFoundException)
			{
				// Deleted between the existence check and read; keep waiting for the replacement.
				return null;
			}
		}

		private static bool IsCounterElement(IDmsElement element)
		{
			return String.Equals(element.Protocol.Name, ProtocolName, StringComparison.OrdinalIgnoreCase);
		}

		private static bool IsRunning(IDmsElement element)
		{
			return element.State == ElementState.Active && element.IsStartupComplete();
		}

		private static long? ReadCounter(IDmsElement element, int parameterId)
		{
			double? value = element.GetStandaloneParameter<double?>(parameterId).GetValue();
			return value.HasValue ? (long)value.Value : (long?)null;
		}

		private static long HighestExistingServiceSuffix(IServiceManagementApiHelper inventory)
		{
			var ids = new List<string>();
			foreach (var page in inventory.ServiceInventory.Services.ReadPaged(new TRUEFilterElement<ServiceManagementService>(), ReadPageSize))
			{
				ids.AddRange(page.Select(service => service.ServiceID));
				if (!page.HasNextPage)
				{
					break;
				}
			}

			return HighestSuffix(ids, ServiceCounter.Prefix);
		}

		private static long HighestExistingServiceOrderSuffix(IServiceManagementApiHelper ordering)
		{
			var ids = new List<string>();
			foreach (var page in ordering.ServiceOrder.ServiceOrders.ReadPaged(new TRUEFilterElement<ServiceOrder>(), ReadPageSize))
			{
				ids.AddRange(page.Select(order => order.OrderId));
				if (!page.HasNextPage)
				{
					break;
				}
			}

			return HighestSuffix(ids, ServiceOrderCounter.Prefix);
		}

		private static bool WaitFor(Func<bool> condition, TimeSpan timeout)
		{
			var stopwatch = Stopwatch.StartNew();
			while (stopwatch.Elapsed < timeout)
			{
				if (condition())
				{
					return true;
				}

				Thread.Sleep(TimeSpan.FromMilliseconds(500));
			}

			return false;
		}

		private static void ValidateEngine(IEngine engine)
		{
			if (engine == null)
			{
				throw new ArgumentNullException(nameof(engine));
			}
		}

		private sealed class Counter
		{
			private readonly string numberFormat;

			public Counter(string name, string kind, string prefix, string numberFormat, int parameterId)
			{
				Name = name;
				Kind = kind;
				Prefix = prefix;
				this.numberFormat = numberFormat;
				ParameterId = parameterId;
			}

			public string Name { get; }

			/// <summary>Gets the counter name understood by the connector's <see cref="IdReservationRequest.Kind"/>.</summary>
			public string Kind { get; }

			public string Prefix { get; }

			public int ParameterId { get; }

			public string Format(long value) => Prefix + value.ToString(numberFormat, CultureInfo.InvariantCulture);
		}

		/// <summary>
		/// Prevents scripts and UDAPI calls on this agent from creating duplicate counter elements at the same time.
		/// </summary>
		private sealed class ElementCreationLock : IDisposable
		{
			private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(3);
			private readonly Mutex mutex;

			private ElementCreationLock(Mutex mutex)
			{
				this.mutex = mutex;
			}

			public static ElementCreationLock Acquire()
			{
				var mutex = new Mutex(false, ElementCreationLockName);
				try
				{
					if (!mutex.WaitOne(Timeout))
					{
						throw new TimeoutException("Timed out waiting for the Service Management counter element lock.");
					}
				}
				catch (AbandonedMutexException)
				{
					// The previous owner terminated without releasing; ownership has been transferred to this thread.
				}
				catch
				{
					mutex.Dispose();
					throw;
				}

				return new ElementCreationLock(mutex);
			}

			public void Dispose()
			{
				mutex.ReleaseMutex();
				mutex.Dispose();
			}
		}
	}
}
