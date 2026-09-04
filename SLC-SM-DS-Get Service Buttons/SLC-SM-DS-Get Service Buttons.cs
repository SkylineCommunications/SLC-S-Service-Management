namespace SLCSMDSGetServiceButtons
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using Skyline.DataMiner.Analytics.GenericInterface;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ApiHelpers;
	using SLC_SM_Common.Extensions;
	using static DomHelpers.SlcServicemanagement.SlcServicemanagementIds.Behaviors.Service_Behavior;
	using Models = Skyline.DataMiner.ProjectApi.ServiceManagement.SDM.ServiceManagement;

	/// <summary>
	///     Represents a data source.
	///     See: https://aka.dataminer.services/gqi-external-data-source for a complete example.
	/// </summary>
	[GQIMetaData(Name = DataSourceName)]
	public sealed class SLCSMDSGetServiceButtons : IGQIDataSource, IGQIOnInit, IGQIInputArguments
	{
		private const string DataSourceName = "SLC-SM-DS-Get Service Buttons";

		private static readonly List<TransitionsEnum> UnHappyFlows = new List<TransitionsEnum>
		{
			TransitionsEnum.New_To_Retired,
			TransitionsEnum.Reserved_To_Retired,
			TransitionsEnum.Designed_To_Retired,
			TransitionsEnum.Terminated_To_Active,
		};

		private static readonly Dictionary<TransitionsEnum, string> ButtonNames = new Dictionary<TransitionsEnum, string>
		{
			{ TransitionsEnum.New_To_Designed, "Design" },
			{ TransitionsEnum.Designed_To_Reserved, "Reserve" },
			{ TransitionsEnum.Reserved_To_Active, "Activate" },
			{ TransitionsEnum.Active_To_Terminated, "Terminate" },
			{ TransitionsEnum.Terminated_To_Retired, "Retire" },
			{ TransitionsEnum.New_To_Retired, "Retire" },
			{ TransitionsEnum.Designed_To_Retired, "Retire" },
			{ TransitionsEnum.Reserved_To_Retired, "Retire" },
			{ TransitionsEnum.Terminated_To_Active, "Activate" },
		};

		private readonly GQIStringArgument serviceReferenceArg = new GQIStringArgument("ServiceReference") { IsRequired = true };
		private Guid serviceReference;
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
				serviceReferenceArg,
			};
		}

		public GQIPage GetNextPage(GetNextPageInputArgs args)
		{
			return _logger.PerformanceLogger(nameof(GetNextPage), BuildupRows);
		}

		public OnArgumentsProcessedOutputArgs OnArgumentsProcessed(OnArgumentsProcessedInputArgs args)
		{
			if (!Guid.TryParse(args.GetArgumentValue(serviceReferenceArg), out serviceReference))
			{
				serviceReference = Guid.Empty;
			}

			return new OnArgumentsProcessedOutputArgs();
		}

		public OnInitOutputArgs OnInit(OnInitInputArgs args)
		{
			_dms = args.DMS;
			_logger = args.Logger;
			_logger.MinimumLogLevel = GQILogLevel.Debug;
			_serviceManagementApiHelper = new ServiceManagementApiHelper(_dms.GetConnection(), "Service Inventory");
			return default;
		}

		private GQIPage BuildupRows()
		{
			try
			{
				if (serviceReference == Guid.Empty)
				{
					return new GQIPage(Array.Empty<GQIRow>()) { HasNextPage = false };
				}

				var service = _logger.PerformanceLogger(
					"Get Service",
					() =>
						_serviceManagementApiHelper.ServiceInventory.Services
							.Read(Models.ServiceExposers.Identifier.Equal(serviceReference.ToString()))
							.FirstOrDefault()
						?? throw new NotSupportedException($"Could not find a service with ID {serviceReference}"));
				StatusesEnum currentState = service.Status;

				return _logger.PerformanceLogger(
					"Build Rows",
					() =>
					{
						var transitions = Enum.GetValues(typeof(TransitionsEnum))
							.Cast<TransitionsEnum>()
							.Where(t => t.ToString().StartsWith($"{currentState}_to_", StringComparison.OrdinalIgnoreCase))
							.ToList();

						List<GQIRow> rows = transitions.Select(
								transition => new GQIRow(
									new[]
									{
										new GQICell { Value = ButtonNames.TryGetValue(transition, out string button) ? button : Transitions.ToValue(transition) },
										new GQICell { Value = "Service_StateTransitions" },
										new GQICell { Value = currentState.ToString() },
										new GQICell { Value = transition.ToString().Split('_').Last() },
										new GQICell { Value = !UnHappyFlows.Contains(transition) },
									}))
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