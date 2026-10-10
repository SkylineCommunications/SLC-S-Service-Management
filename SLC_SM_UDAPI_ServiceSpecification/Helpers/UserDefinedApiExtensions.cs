namespace SLC_SM_UDAPI_ServiceSpecification.Helpers
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

			// Exposers are registered in static constructors; filters on them need that to have run.
			RuntimeHelpers.RunClassConstructor(typeof(ServiceSpecificationExposers).TypeHandle);
			RuntimeHelpers.RunClassConstructor(typeof(ServiceCategoryExposers).TypeHandle);

			return builder.ConfigureServices(services =>
			{
				services.AddScoped<IServiceManagementApiHelper>(
					sp => new ServiceManagementApiHelper(sp.GetRequiredService<IAccessor<IEngine>>().Value.GetUserConnection(), "Service Specification UDAPI"));
			});
		}
	}
}
