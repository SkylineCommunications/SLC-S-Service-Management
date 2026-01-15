/*
****************************************************************************
*  Copyright (c) 2025,  Skyline Communications NV  All Rights Reserved.    *
****************************************************************************

By using this script, you expressly agree with the usage terms and
conditions set out below.
This script and all related materials are protected by copyrights and
other intellectual property rights that exclusively belong
to Skyline Communications.

A user license granted for this script is strictly for personal use only.
This script may not be used in any way by anyone without the prior
written consent of Skyline Communications. Any sublicensing of this
script is forbidden.

Any modifications to this script by the user are only allowed for
personal use and within the intended purpose of the script,
and will remain the sole responsibility of the user.
Skyline Communications will not be responsible for any damages or
malfunctions whatsoever of the script resulting from a modification
or adaptation by the user.

The content of this script is confidential information.
The user hereby agrees to keep this confidential information strictly
secret and confidential and not to disclose or reveal it, in whole
or in part, directly or indirectly to any person, entity, organization
or administration without the prior written consent of
Skyline Communications.

Any inquiries can be addressed to:

	Skyline Communications NV
	Ambachtenstraat 33
	B-8870 Izegem
	Belgium
	Tel.	: +32 51 31 35 69
	Fax.	: +32 51 31 01 29
	E-mail	: info@skyline.be
	Web		: www.skyline.be
	Contact	: Ben Vandenberghe

****************************************************************************
Revision History:

DATE		VERSION		AUTHOR			COMMENTS

11/09/2025	1.0.0.1		RCA, Skyline	Initial version
****************************************************************************
*/

namespace SLCSMDSGetServiceByServiceType
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using Skyline.DataMiner.Analytics.GenericInterface;
	using Skyline.DataMiner.Core.DataMinerSystem.Common;
	using Skyline.DataMiner.Net.Apps.DataMinerObjectModel;
	using Skyline.DataMiner.Net.Helper;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.ServiceManagement;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM;
	using SLC_SM_Common.Extensions;
	using Models = Skyline.DataMiner.ProjectApi.ServiceManagement.API.ServiceManagement.Models;

	/// <summary>
	///     Represents a data source.
	///     See: https://aka.dataminer.services/gqi-external-data-source for a complete example.
	/// </summary>
	[GQIMetaData(Name = DataSourceName)]
	public sealed class SLCSMDSGetServiceByServiceType : IGQIDataSource, IGQIOnInit
	{
		private const string DataSourceName = "SLC_SM_DS_GetServiceByServiceType";

		private static readonly string ConfigParamNameServiceType = "Service Type";
		private static readonly string ConfigParamNameReceptionType = "Reception Type";
		private static readonly string ConfigParamNameChannelId = "Channel ID";
		private static readonly string ConfigParamNameVideoFormat = "Video Format";
		private static readonly string ConfigParamNameDistributionType = "Distribution Type";
		private static readonly string ConfigParamNameRegion = "Region";
		private static readonly string[] ConfigurationParameterNames = new[]
		{
			ConfigParamNameServiceType,
			ConfigParamNameReceptionType,
			ConfigParamNameChannelId,
			ConfigParamNameVideoFormat,
			ConfigParamNameDistributionType,
			ConfigParamNameRegion,
		};

		private IGQILogger _logger;
		private Skyline.DataMiner.Net.IConnection _connection;
		private IDms _dms;
		private GQIDMS _gqiDms;

		private Guid configID_ServiceType;
		private Guid configID_ReceptionType;
		private Guid configID_ChannelId;
		private Guid configID_VideoFormat;
		private Guid configID_DistType;
		private Guid configID_Region;

		public GQIColumn[] GetColumns()
		{
			return new GQIColumn[]
			{
				new GQIStringColumn("Service Id"),
				new GQIStringColumn("Service Name"),
				new GQIStringColumn("Icon"),
				new GQIStringColumn("Status"),
				new GQIIntColumn("Alarm Level"),
				new GQIStringColumn("Service Type"),
				new GQIStringColumn("Reception Type"),
				new GQIStringColumn("Channel ID"),
				new GQIStringColumn("Video Format"),
				new GQIStringColumn("Distribution Type"),
				new GQIStringColumn("Region"),
			};
		}

		public GQIPage GetNextPage(GetNextPageInputArgs args)
		{
			return _logger.PerformanceLogger(nameof(GetNextPage), BuildupRows);
		}

		public OnInitOutputArgs OnInit(OnInitInputArgs args)
		{
			_logger = args.Logger;
			_logger.MinimumLogLevel = GQILogLevel.Debug;

			_gqiDms = args.DMS;
			_connection = _gqiDms.GetConnection();
			_dms = _connection.GetDms();

			return new OnInitOutputArgs();
		}

		private GQIPage BuildupRows()
		{
			try
			{
				FilterElement<Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.ConfigurationParameter> filterConfigParams = new ORFilterElement<Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations.Models.ConfigurationParameter>();
				foreach (string configurationParameterName in ConfigurationParameterNames)
				{
					filterConfigParams = filterConfigParams.OR(ConfigurationParameterExposers.Name.Equal(configurationParameterName));
				}

				var configurationHelper = new DataHelperConfigurationParameter(_gqiDms.GetConnection());
				var configurationParameters = configurationHelper.Read(filterConfigParams).ToDictionary(p => p.Name, p => p.ID);

				configurationParameters.TryGetValue(ConfigParamNameServiceType, out configID_ServiceType);
				configurationParameters.TryGetValue(ConfigParamNameReceptionType, out configID_ReceptionType);
				configurationParameters.TryGetValue(ConfigParamNameChannelId, out configID_ChannelId);
				configurationParameters.TryGetValue(ConfigParamNameVideoFormat, out configID_VideoFormat);
				configurationParameters.TryGetValue(ConfigParamNameDistributionType, out configID_DistType);
				configurationParameters.TryGetValue(ConfigParamNameRegion, out configID_Region);

				var serviceHelper = new DataHelperService(_gqiDms.GetConnection());
				var services = configurationParameters
					.SelectMany(cp => serviceHelper.GetServicesByCharacteristic(cp.Key))
					.GroupBy(s => s.ID)
					.Select(g => g.First())
					.ToList();

				return new GQIPage(
					services
						.Select(service => BuildRow(service))
						.ToArray());
			}
			catch (Exception e)
			{
				_gqiDms.GenerateInformationMessage($"GQIDS|{DataSourceName}|Exception: {e}");
				_logger.Error($"GQIDS|{DataSourceName}|Exception: {e}");
				return new GQIPage(Enumerable.Empty<GQIRow>().ToArray());
			}
		}

		private GQIRow BuildRow(Models.Service service)
		{
			int alarmLevel = _logger.PerformanceLogger("Get Alarm Level", () => (int)TryGetAlarmLevel(service));

			var configs = service.ServiceConfiguration.Parameters
				.GroupBy(p => p.ConfigurationParameter.ConfigurationParameterId)
				.ToDictionary(
					g => g.Key,
					g => g.First().ConfigurationParameter.StringValue); // If multiples of same parameter found, assume the first.

			return new GQIRow(
				new[]
				{
					new GQICell { Value = service.ID.ToString() },
					new GQICell { Value = service.Name },
					new GQICell { Value = service.Icon ?? String.Empty },
					new GQICell { Value = service.Status.ToString() },
					new GQICell { Value = alarmLevel },
					new GQICell { Value = configs.TryGetValue(configID_ServiceType, out string st) ? st : String.Empty },
					new GQICell { Value = configs.TryGetValue(configID_ReceptionType, out string rt) ? rt : String.Empty },
					new GQICell { Value = configs.TryGetValue(configID_ChannelId, out string ci) ? ci : String.Empty },
					new GQICell { Value = configs.TryGetValue(configID_VideoFormat, out string vf) ? vf : String.Empty },
					new GQICell { Value = configs.TryGetValue(configID_DistType, out string dt) ? dt : String.Empty },
					new GQICell { Value = configs.TryGetValue(configID_Region, out string r) ? r : String.Empty },
				});
		}

		private AlarmLevel TryGetAlarmLevel(Skyline.DataMiner.ProjectApi.ServiceManagement.API.ServiceManagement.Models.Service service)
		{
			if (_dms.ServiceExistsSafe(service.Name, out IDmsService srv))
			{
				return srv.GetState().Level;
			}

			return AlarmLevel.Undefined;
		}
	}
}