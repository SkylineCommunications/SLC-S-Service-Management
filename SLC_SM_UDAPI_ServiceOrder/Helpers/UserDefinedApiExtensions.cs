namespace SLC_SM_UDAPI_ServiceOrder.Helpers
{
	using System;
	using System.Runtime.CompilerServices;

	using Microsoft.Extensions.DependencyInjection;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ApiHelpers;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;
	using Skyline.DataMiner.Utils.UserDefinedApiToolkit;

	internal static class UserDefinedApiExtensions
	{
		public static UserDefinedApiBuilder AddServices(this UserDefinedApiBuilder builder)
		{
			if (builder is null)
			{
				throw new ArgumentNullException(nameof(builder));
			}

			RegisterExposers();

			return builder.ConfigureServices(services =>
			{
				services.AddScoped<IServiceManagementApiHelper>(
					sp => sp.GetRequiredService<IAccessor<IEngine>>().Value.GetServiceManagementApiHelper());
			});
		}

		internal static IServiceManagementApiHelper GetServiceManagementApiHelper(this IEngine engine)
		{
			if (engine is null)
			{
				throw new ArgumentNullException(nameof(engine), "Engine cannot be null.");
			}

			return new ServiceManagementApiHelper(engine.GetUserConnection(), "Service Order UDAPI");
		}

		/// <summary>
		/// Ensures that static constructors are called and exposers are registered,
		/// so filters can be built on the entities' properties.
		/// </summary>
		private static void RegisterExposers()
		{
			RuntimeHelpers.RunClassConstructor(typeof(ServiceOrderExposers).TypeHandle);
			RuntimeHelpers.RunClassConstructor(typeof(ServiceOrderItemExposers).TypeHandle);
			RuntimeHelpers.RunClassConstructor(typeof(ServiceOrderItemConfigurationValueExposers).TypeHandle);
		}
	}
}
