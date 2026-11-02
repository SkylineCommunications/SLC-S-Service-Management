namespace SLCSMDSGetServiceOrderButtons
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using Skyline.DataMiner.Analytics.GenericInterface;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ApiHelpers;
	using SLC_SM_Common.Extensions;
	using static DomHelpers.SlcServicemanagement.SlcServicemanagementIds.Behaviors.Serviceorder_Behavior;
	using Models = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;

	/// <summary>
	///     Represents a data source.
	///     See: https://aka.dataminer.services/gqi-external-data-source for a complete example.
	/// </summary>
	[GQIMetaData(Name = DataSourceName)]
	public sealed class SLCSMDSGetServiceOrderButtons : IGQIDataSource, IGQIOnInit, IGQIInputArguments
	{
		private const string DataSourceName = "SLC-SM-DS-Get Service Order Buttons";

		private static readonly List<TransitionsEnum> UnHappyFlows = new List<TransitionsEnum>
		{
			TransitionsEnum.New_To_Rejected,
			TransitionsEnum.Acknowledged_To_Rejected,
			TransitionsEnum.Inprogress_To_Failed,
			TransitionsEnum.Inprogress_To_Partial,
			TransitionsEnum.Inprogress_To_Held,
			TransitionsEnum.Inprogress_To_Pending,
			TransitionsEnum.Pending_To_Assesscancellation,
			TransitionsEnum.Assesscancellation_To_Pendingcancellation,
			TransitionsEnum.Pendingcancellation_To_Cancelled,
			TransitionsEnum.Held_To_Inprogress,
			TransitionsEnum.Assesscancellation_To_Held,
			TransitionsEnum.Assesscancellation_To_Pending,
			TransitionsEnum.Held_To_Assesscancellation,
			TransitionsEnum.Pending_To_Inprogress,
		};

		private static readonly Dictionary<TransitionsEnum, string> ButtonNames = new Dictionary<TransitionsEnum, string>
		{
			{ TransitionsEnum.New_To_Acknowledged, "Acknowledge" },
			{ TransitionsEnum.New_To_Rejected, "Reject" },
			{ TransitionsEnum.Acknowledged_To_Rejected, "Reject" },
			{ TransitionsEnum.Acknowledged_To_Inprogress, "Initialize" },
			{ TransitionsEnum.Inprogress_To_Completed, "Complete" },
			{ TransitionsEnum.Inprogress_To_Failed, "Failed" },
			{ TransitionsEnum.Inprogress_To_Partial, "Partially Failed" },
			{ TransitionsEnum.Inprogress_To_Held, "Issue" },
			{ TransitionsEnum.Inprogress_To_Pending, "Information Missing" },
			{ TransitionsEnum.Pending_To_Assesscancellation, "Request Cancellation" },
			{ TransitionsEnum.Assesscancellation_To_Pendingcancellation, "Confirm Cancellation" },
			{ TransitionsEnum.Pendingcancellation_To_Cancelled, "Cancel" },
			{ TransitionsEnum.Held_To_Inprogress, "In Progress" },
			{ TransitionsEnum.Assesscancellation_To_Held, "Issue" },
			{ TransitionsEnum.Assesscancellation_To_Pending, "Information Missing" },
			{ TransitionsEnum.Held_To_Assesscancellation, "Request Cancellation" },
			{ TransitionsEnum.Pending_To_Inprogress, "In Progress" },
		};

		private readonly GQIStringArgument serviceOrderReferenceArg = new GQIStringArgument("ServiceOrderReference") { IsRequired = true };
		private Guid serviceOrderReference;
		private GQIDMS _dms;
		private IGQILogger _logger;
		private IServiceManagementApiHelper _serviceManagementApiHelper;

		public GQIColumn[] GetColumns()
		{
			return new GQIColumn[]
			{
				new GQIStringColumn("Button Label"),
				new GQIStringColumn("Script"),
				new GQIStringColumn("PreviousState"),
				new GQIStringColumn("NextState"),
				new GQIBooleanColumn("IsHappyFlow"),
			};
		}

		public GQIArgument[] GetInputArguments()
		{
			return new GQIArgument[]
			{
				serviceOrderReferenceArg,
			};
		}

		public GQIPage GetNextPage(GetNextPageInputArgs args)
		{
			return _logger.PerformanceLogger(nameof(GetNextPage), BuildupRows);
		}

		public OnArgumentsProcessedOutputArgs OnArgumentsProcessed(OnArgumentsProcessedInputArgs args)
		{
			if (!Guid.TryParse(args.GetArgumentValue(serviceOrderReferenceArg), out serviceOrderReference))
			{
				serviceOrderReference = Guid.Empty;
			}

			return new OnArgumentsProcessedOutputArgs();
		}

		public OnInitOutputArgs OnInit(OnInitInputArgs args)
		{
			_dms = args.DMS;
			_logger = args.Logger;
			_logger.MinimumLogLevel = GQILogLevel.Debug;
			_serviceManagementApiHelper = new ServiceManagementApiHelper(_dms.GetConnection(), "Service Ordering");
			return default;
		}

		private GQIPage BuildupRows()
		{
			try
			{
				if (serviceOrderReference == Guid.Empty)
				{
					return new GQIPage(Array.Empty<GQIRow>()) { HasNextPage = false };
				}

				var order = _logger.PerformanceLogger(
					"Get Service Order",
					() =>
						_serviceManagementApiHelper.ServiceOrder.ServiceOrders
							.Read(Models.ServiceOrderExposers.Identifier.Equal(serviceOrderReference.ToString()))
							.FirstOrDefault()
						?? throw new NotSupportedException($"Could not find a service order with ID {serviceOrderReference}"));
				StatusesEnum currentState = order.Status;

				return _logger.PerformanceLogger(
					"Build Rows",
					() =>
					{
						var transitions = Enum.GetValues(typeof(TransitionsEnum))
							.Cast<TransitionsEnum>()
							.Where(t => t.ToString().StartsWith($"{currentState}_to_", StringComparison.OrdinalIgnoreCase))
							.ToList();

						List<GQIRow> rows = transitions.Select(
								transition =>
								{
									string nextState = transition.ToString().Split('_').Last();
									return new GQIRow(
										new[]
										{
											new GQICell { Value = ButtonNames.TryGetValue(transition, out string button) ? button : Transitions.ToValue(transition) },
											new GQICell { Value = "ServiceOrderItem_StateTransitions" },
											new GQICell { Value = currentState.ToString() },
											new GQICell { Value = nextState },
											new GQICell { Value = !UnHappyFlows.Contains(transition) },
										});
								})
							.ToList();

						return new GQIPage(rows.ToArray())
						{
							HasNextPage = false,
						};
					});
			}
			catch (Exception e)
			{
				_dms.GenerateInformationMessage($"GQIDS|{DataSourceName}|Exception: {e}");
				_logger.Error($"GQIDS|{DataSourceName}|Exception: {e}");
				return new GQIPage(Array.Empty<GQIRow>());
			}
		}
	}
}