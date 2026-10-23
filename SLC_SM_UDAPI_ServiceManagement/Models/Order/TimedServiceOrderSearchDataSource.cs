namespace SLC_SM_UDAPI_ServiceManagement.Helpers.Order
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;

	using Microsoft.Extensions.Logging;

	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;

	using SLC_SM_UDAPI_ServiceManagement.Controllers;

	/// <summary>
	/// Decorator for <see cref="IServiceOrderSearchDataSource"/> that logs the duration and item count of every read.
	/// </summary>
	public sealed class TimedServiceOrderSearchDataSource : IServiceOrderSearchDataSource
	{
		private readonly ILogger<ServiceOrderController> _logger;
		private readonly IServiceOrderSearchDataSource inner;

		/// <summary>
		/// Initializes a new instance of the <see cref="TimedServiceOrderSearchDataSource"/> class.
		/// </summary>
		/// <param name="inner">The data source to measure.</param>
		/// <param name="log">The logger.</param>
		public TimedServiceOrderSearchDataSource(IServiceOrderSearchDataSource inner, ILogger<ServiceOrderController> log)
		{
			this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
			_logger = log ?? throw new ArgumentNullException(nameof(log));
		}

		/// <inheritdoc/>
		public IReadOnlyList<ServiceOrderItem> GetServiceOrderItems(IReadOnlyCollection<string> ids)
		{
			return Measure(nameof(GetServiceOrderItems), () => inner.GetServiceOrderItems(ids));
		}

		/// <inheritdoc/>
		public IReadOnlyList<ServiceOrder> GetServiceOrders()
		{
			return Measure(nameof(GetServiceOrders), inner.GetServiceOrders);
		}

		private IReadOnlyList<T> Measure<T>(string name, Func<IReadOnlyList<T>> read)
		{
			var stopwatch = Stopwatch.StartNew();
			var result = read();
			stopwatch.Stop();

			_logger.LogInformation("ServiceOrderSearch|{Name}: {Count} item(s) in {ElapsedMilliseconds} ms.", name, result?.Count ?? 0, stopwatch.ElapsedMilliseconds);
			return result;
		}
	}
}