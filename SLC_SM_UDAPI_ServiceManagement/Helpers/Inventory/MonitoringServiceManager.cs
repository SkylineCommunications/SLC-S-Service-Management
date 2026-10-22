namespace SLC_SM_UDAPI_ServiceManagement.Helpers.Inventory
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Linq;
	using System.Threading;
	using Skyline.DataMiner.Core.DataMinerSystem.Automation;
	using Skyline.DataMiner.Core.DataMinerSystem.Common;
	using SLC_SM_Common.Extensions;
	using IEngine = Skyline.DataMiner.Automation.IEngine;

	/// <summary>
	/// Manages the DataMiner service associated with a Service Management service.
	/// </summary>
	public sealed class MonitoringServiceManager : IMonitoringServiceManager
	{
		private const string LogoProperty = "Logo";
		private const int CreationTimeoutMs = 5000;

		private readonly IEngine engine;

		public MonitoringServiceManager(IEngine engine)
		{
			this.engine = engine ?? throw new ArgumentNullException(nameof(engine));
		}

		/// <inheritdoc/>
		public IReadOnlyList<MonitoringServiceInfo> List()
		{
			return engine.GetDms().GetServices()
				.Where(service => service != null && !String.IsNullOrWhiteSpace(service.Name))
				.OrderBy(service => service.Name, StringComparer.OrdinalIgnoreCase)
				.Select(service => new MonitoringServiceInfo
				{
					Id = service.DmsServiceId.Value,
					Name = service.Name,
				})
				.ToList();
		}

		/// <inheritdoc/>
		public bool Exists(string serviceName)
		{
			return engine.GetDms().ServiceExistsSafe(serviceName, out IDmsService _);
		}

		/// <inheritdoc/>
		public void Create(string serviceName, string icon)
		{
			var dms = engine.GetDms();
			if (dms.ServiceExistsSafe(serviceName, out IDmsService _))
			{
				throw new InvalidOperationException($"A DataMiner service with name {serviceName} already exists.");
			}

			var serviceId = dms.GetAgents().First().CreateService(new ServiceConfiguration(dms, serviceName));
			SetLogo(dms, serviceId, icon);
		}

		private void SetLogo(IDms dms, DmsServiceId serviceId, string icon)
		{
			if (!dms.PropertyExists(LogoProperty, PropertyType.Service))
			{
				dms.CreateProperty(LogoProperty, PropertyType.Service, false, false, false);
			}

			WaitUntilCreated(serviceId);
			var service = dms.GetService(serviceId);

			var property = service.Properties.SingleOrDefault(p => p.Definition.Name == LogoProperty).AsWritable();
			property.Value = icon;
			service.Update();
		}

		private void WaitUntilCreated(DmsServiceId serviceId)
		{
			var stopwatch = Stopwatch.StartNew();
			while (engine.FindServiceByKey(serviceId.Value) == null)
			{
				if (stopwatch.ElapsedMilliseconds > CreationTimeoutMs)
				{
					throw new TimeoutException($"Service {serviceId} was not created within {CreationTimeoutMs} ms.");
				}

				Thread.Sleep(250);
			}
		}
	}
}
