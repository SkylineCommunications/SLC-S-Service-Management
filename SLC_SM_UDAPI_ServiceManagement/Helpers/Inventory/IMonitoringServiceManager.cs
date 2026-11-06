namespace SLC_SM_UDAPI_ServiceManagement.Helpers.Inventory
{
	using System.Collections.Generic;

	/// <summary>
	/// Manages the DataMiner (monitoring) services of inventory services.
	/// </summary>
	public interface IMonitoringServiceManager
	{
		/// <summary>Lists the named DataMiner services.</summary>
		/// <returns>The services, sorted by name.</returns>
		IReadOnlyList<MonitoringServiceInfo> List();

		/// <summary>Checks whether a DataMiner service with the given name exists.</summary>
		/// <param name="serviceName">The name of the DataMiner service.</param>
		/// <returns><see langword="true"/> when the service exists.</returns>
		bool Exists(string serviceName);

		/// <summary>Creates a DataMiner service with the given name and logo.</summary>
		/// <param name="serviceName">The name of the DataMiner service.</param>
		/// <param name="icon">The logo of the service.</param>
		void Create(string serviceName, string icon);
	}
}
