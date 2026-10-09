namespace SLC_SM_UDAPI_ServiceInventory.Helpers
{
	using System;
	using System.Runtime.CompilerServices;

	using Microsoft.Extensions.DependencyInjection;

	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ApiHelpers;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.Configurations;
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

			// Register ApiHelpers as scoped services. Controllers inject these directly,
			// keeping constructor parameter counts flat as new repos are needed.
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

			return new ServiceManagementApiHelper(engine.GetUserConnection(), "Service Inventory UDAPI");
		}

		/// <summary>
		/// Ensures that static constructors are called and exposers are registered,
		/// so the OData translators can filter and order by the entities' properties.
		/// </summary>
		private static void RegisterExposers()
		{
			RuntimeHelpers.RunClassConstructor(typeof(ConfigurationParameterExposers).TypeHandle);
			RuntimeHelpers.RunClassConstructor(typeof(ConfigurationParameterValueExposers).TypeHandle);
			RuntimeHelpers.RunClassConstructor(typeof(ConfigurationUnitExposers).TypeHandle);
			RuntimeHelpers.RunClassConstructor(typeof(DiscreteParameterOptionsExposers).TypeHandle);
			RuntimeHelpers.RunClassConstructor(typeof(DiscreteValueExposers).TypeHandle);
			RuntimeHelpers.RunClassConstructor(typeof(NumberParameterOptionsExposers).TypeHandle);
			RuntimeHelpers.RunClassConstructor(typeof(ProfileDefinitionExposers).TypeHandle);
			RuntimeHelpers.RunClassConstructor(typeof(ProfileExposers).TypeHandle);
			RuntimeHelpers.RunClassConstructor(typeof(ProtocolTestExposers).TypeHandle);
			RuntimeHelpers.RunClassConstructor(typeof(ReferencedConfigurationParameterExposers).TypeHandle);
			RuntimeHelpers.RunClassConstructor(typeof(ReferencedProfileDefinitionExposers).TypeHandle);
			RuntimeHelpers.RunClassConstructor(typeof(ScriptExposers).TypeHandle);
			RuntimeHelpers.RunClassConstructor(typeof(ServiceCategoryExposers).TypeHandle);
			RuntimeHelpers.RunClassConstructor(typeof(ServiceConfigurationValueExposers).TypeHandle);
			RuntimeHelpers.RunClassConstructor(typeof(ServiceConfigurationVersionExposers).TypeHandle);
			RuntimeHelpers.RunClassConstructor(typeof(ServiceExposers).TypeHandle);
			RuntimeHelpers.RunClassConstructor(typeof(ServiceOrderExposers).TypeHandle);
			RuntimeHelpers.RunClassConstructor(typeof(ServiceOrderItemConfigurationValueExposers).TypeHandle);
			RuntimeHelpers.RunClassConstructor(typeof(ServiceOrderItemExposers).TypeHandle);
			RuntimeHelpers.RunClassConstructor(typeof(ServiceProfileExposers).TypeHandle);
			RuntimeHelpers.RunClassConstructor(typeof(ServicePropertyExposers).TypeHandle);
			RuntimeHelpers.RunClassConstructor(typeof(ServicePropertyValueExposers).TypeHandle);
			RuntimeHelpers.RunClassConstructor(typeof(ServiceSpecificationConfigurationValueExposers).TypeHandle);
			RuntimeHelpers.RunClassConstructor(typeof(ServiceSpecificationExposers).TypeHandle);
			RuntimeHelpers.RunClassConstructor(typeof(ServiceSpecificationProfileExposers).TypeHandle);
			RuntimeHelpers.RunClassConstructor(typeof(TextParameterOptionsExposers).TypeHandle);
		}
	}
}