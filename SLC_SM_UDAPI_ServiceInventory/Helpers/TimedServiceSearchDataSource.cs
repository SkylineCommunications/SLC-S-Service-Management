namespace SLC_SM_UDAPI_ServiceInventory.Helpers
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;

	using Microsoft.Extensions.Logging;

	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.Configurations;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;

	using SLC_SM_UDAPI_ServiceInventory.Controllers;

	/// <summary>
	/// Decorator for <see cref="IServiceSearchDataSource"/> that logs the duration and item count of every read.
	/// </summary>
	public sealed class TimedServiceSearchDataSource : IServiceSearchDataSource
	{
		private readonly IServiceSearchDataSource inner;
		private readonly ILogger<ServiceInventoryController> _logger;

		/// <summary>
		/// Initializes a new instance of the <see cref="TimedServiceSearchDataSource"/> class.
		/// </summary>
		/// <param name="inner">The data source to measure.</param>
		/// <param name="log">The log callback.</param>
		public TimedServiceSearchDataSource(IServiceSearchDataSource inner, ILogger<ServiceInventoryController> log)
		{
			this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
			this._logger = log ?? throw new ArgumentNullException(nameof(log));
		}

		/// <inheritdoc/>
		public IReadOnlyList<Service> GetServices()
		{
			return Measure(nameof(GetServices), inner.GetServices);
		}

		/// <inheritdoc/>
		public IReadOnlyList<Service> GetServicesByConfigurationVersion(IReadOnlyCollection<string> versionIds)
		{
			return Measure(nameof(GetServicesByConfigurationVersion), () => inner.GetServicesByConfigurationVersion(versionIds));
		}

		/// <inheritdoc/>
		public IReadOnlyList<ServiceConfigurationVersion> GetServiceConfigurationVersions(IReadOnlyCollection<string> ids)
		{
			return Measure(nameof(GetServiceConfigurationVersions), () => inner.GetServiceConfigurationVersions(ids));
		}

		/// <inheritdoc/>
		public IReadOnlyList<ServiceProfile> GetServiceProfiles(IReadOnlyCollection<string> ids)
		{
			return Measure(nameof(GetServiceProfiles), () => inner.GetServiceProfiles(ids));
		}

		/// <inheritdoc/>
		public IReadOnlyList<Profile> GetProfiles(IReadOnlyCollection<string> ids)
		{
			return Measure(nameof(GetProfiles), () => inner.GetProfiles(ids));
		}

		/// <inheritdoc/>
		public IReadOnlyList<ProfileDefinition> GetProfileDefinitions()
		{
			return Measure(nameof(GetProfileDefinitions), inner.GetProfileDefinitions);
		}

		/// <inheritdoc/>
		public IReadOnlyList<ConfigurationParameterValue> GetConfigurationParameterValues(IReadOnlyCollection<string> labels, IReadOnlyCollection<string> parameterIds)
		{
			return Measure(nameof(GetConfigurationParameterValues), () => inner.GetConfigurationParameterValues(labels, parameterIds));
		}

		/// <inheritdoc/>
		public IReadOnlyList<string> GetProfileIdsContainingValues(IReadOnlyCollection<string> parameterValueIds)
		{
			return Measure(nameof(GetProfileIdsContainingValues), () => inner.GetProfileIdsContainingValues(parameterValueIds));
		}

		/// <inheritdoc/>
		public IReadOnlyList<string> GetProfileIdsByDefinition(IReadOnlyCollection<string> profileDefinitionIds)
		{
			return Measure(nameof(GetProfileIdsByDefinition), () => inner.GetProfileIdsByDefinition(profileDefinitionIds));
		}

		/// <inheritdoc/>
		public IReadOnlyList<string> GetParentProfileIds(IReadOnlyCollection<string> childProfileIds)
		{
			return Measure(nameof(GetParentProfileIds), () => inner.GetParentProfileIds(childProfileIds));
		}

		/// <inheritdoc/>
		public IReadOnlyList<string> GetServiceConfigurationValueIds(IReadOnlyCollection<string> parameterValueIds)
		{
			return Measure(nameof(GetServiceConfigurationValueIds), () => inner.GetServiceConfigurationValueIds(parameterValueIds));
		}

		/// <inheritdoc/>
		public IReadOnlyList<string> GetServiceProfileIds(IReadOnlyCollection<string> profileIds)
		{
			return Measure(nameof(GetServiceProfileIds), () => inner.GetServiceProfileIds(profileIds));
		}

		/// <inheritdoc/>
		public IReadOnlyList<string> GetServiceConfigurationVersionIds(IReadOnlyCollection<string> serviceProfileIds, IReadOnlyCollection<string> serviceConfigurationValueIds)
		{
			return Measure(nameof(GetServiceConfigurationVersionIds), () => inner.GetServiceConfigurationVersionIds(serviceProfileIds, serviceConfigurationValueIds));
		}

		private IReadOnlyList<T> Measure<T>(string name, Func<IReadOnlyList<T>> read)
		{
			var stopwatch = Stopwatch.StartNew();
			var result = read();
			stopwatch.Stop();

			_logger.LogInformation("ServiceSearch|{Name}: {Count} item(s) in {ElapsedMilliseconds} ms.", name, result?.Count ?? 0, stopwatch.ElapsedMilliseconds);
			return result;
		}
	}
}
