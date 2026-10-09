namespace SLC_SM_UDAPI_ServiceInventory.Helpers
{
	using System.Collections.Generic;

	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.Configurations;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;

	/// <summary>
	/// Provides the server-side filtered reads needed by <see cref="ServiceParameterSearch"/>.
	/// The search works bottom-up: parameter values, then profiles/standalones, service profiles, versions and services.
	/// </summary>
	public interface IServiceSearchDataSource
	{
		/// <summary>Gets all services.</summary>
		/// <returns>The services.</returns>
		IReadOnlyList<Service> GetServices();

		/// <summary>Gets the services whose current configuration version is one of the given IDs.</summary>
		/// <param name="versionIds">The service configuration version IDs.</param>
		/// <returns>The services.</returns>
		IReadOnlyList<Service> GetServicesByConfigurationVersion(IReadOnlyCollection<string> versionIds);

		/// <summary>Gets service configuration versions by ID.</summary>
		/// <param name="ids">The IDs.</param>
		/// <returns>The versions.</returns>
		IReadOnlyList<ServiceConfigurationVersion> GetServiceConfigurationVersions(IReadOnlyCollection<string> ids);

		/// <summary>Gets service profiles by ID.</summary>
		/// <param name="ids">The IDs.</param>
		/// <returns>The service profiles.</returns>
		IReadOnlyList<ServiceProfile> GetServiceProfiles(IReadOnlyCollection<string> ids);

		/// <summary>Gets profiles by ID.</summary>
		/// <param name="ids">The IDs.</param>
		/// <returns>The profiles.</returns>
		IReadOnlyList<Profile> GetProfiles(IReadOnlyCollection<string> ids);

		/// <summary>Gets all profile definitions.</summary>
		/// <returns>The profile definitions.</returns>
		IReadOnlyList<ProfileDefinition> GetProfileDefinitions();

		/// <summary>Gets the configuration parameter values with one of the given labels or configuration parameter IDs.</summary>
		/// <param name="labels">The labels.</param>
		/// <param name="parameterIds">The configuration parameter IDs.</param>
		/// <returns>The configuration parameter values.</returns>
		IReadOnlyList<ConfigurationParameterValue> GetConfigurationParameterValues(IReadOnlyCollection<string> labels, IReadOnlyCollection<string> parameterIds);

		/// <summary>Gets the IDs of the profiles that directly reference one of the given configuration parameter values.</summary>
		/// <param name="parameterValueIds">The configuration parameter value IDs.</param>
		/// <returns>The profile IDs.</returns>
		IReadOnlyList<string> GetProfileIdsContainingValues(IReadOnlyCollection<string> parameterValueIds);

		/// <summary>Gets the IDs of the profiles of one of the given profile definitions.</summary>
		/// <param name="profileDefinitionIds">The profile definition IDs.</param>
		/// <returns>The profile IDs.</returns>
		IReadOnlyList<string> GetProfileIdsByDefinition(IReadOnlyCollection<string> profileDefinitionIds);

		/// <summary>Gets the IDs of the profiles that directly contain one of the given child profiles.</summary>
		/// <param name="childProfileIds">The child profile IDs.</param>
		/// <returns>The parent profile IDs.</returns>
		IReadOnlyList<string> GetParentProfileIds(IReadOnlyCollection<string> childProfileIds);

		/// <summary>Gets the IDs of the service configuration values (standalones) referencing one of the given configuration parameter values.</summary>
		/// <param name="parameterValueIds">The configuration parameter value IDs.</param>
		/// <returns>The service configuration value IDs.</returns>
		IReadOnlyList<string> GetServiceConfigurationValueIds(IReadOnlyCollection<string> parameterValueIds);

		/// <summary>Gets the IDs of the service profiles referencing one of the given profiles.</summary>
		/// <param name="profileIds">The profile IDs.</param>
		/// <returns>The service profile IDs.</returns>
		IReadOnlyList<string> GetServiceProfileIds(IReadOnlyCollection<string> profileIds);

		/// <summary>Gets the IDs of the configuration versions containing one of the given service profiles or service configuration values.</summary>
		/// <param name="serviceProfileIds">The service profile IDs.</param>
		/// <param name="serviceConfigurationValueIds">The service configuration value IDs.</param>
		/// <returns>The service configuration version IDs.</returns>
		IReadOnlyList<string> GetServiceConfigurationVersionIds(IReadOnlyCollection<string> serviceProfileIds, IReadOnlyCollection<string> serviceConfigurationValueIds);
	}
}
