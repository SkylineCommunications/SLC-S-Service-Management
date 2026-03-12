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
    Tel.    : +32 51 31 35 69
    Fax.    : +32 51 31 01 29
    E-mail    : info@skyline.be
    Web        : www.skyline.be
    Contact    : Ben Vandenberghe

****************************************************************************
Revision History:

DATE        VERSION        AUTHOR            COMMENTS

09/09/2025    1.0.0.1        RCA, Skyline    Initial version
****************************************************************************
*/

namespace SLCSMDSGetServiceDetails
{
	using System;
	using System.Linq;
	using DomHelpers.SlcServicemanagement;
	using Skyline.DataMiner.Analytics.GenericInterface;
	using Skyline.DataMiner.Core.DataMinerSystem.Common;
	using Skyline.DataMiner.Net.Apps.DataMinerObjectModel;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.ServiceManagement;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM;
	using AlarmLevel = Skyline.DataMiner.Core.DataMinerSystem.Common.AlarmLevel;

	/// <summary>
	/// Represents a data source.
	/// See: https://aka.dataminer.services/gqi-external-data-source for a complete example.
	/// </summary>
	[GQIMetaData(Name = "SLC_SM_DS_GetServiceDetails")]
	public sealed class SLCSMDSGetServiceDetails : IGQIDataSource
		, IGQIOnInit
		, IGQIInputArguments
	{
		private Arguments _arguments = new Arguments();
		private GQIDMS _gqiDms;
		private IDms _dms;
		private IDma _agent;

		public OnInitOutputArgs OnInit(OnInitInputArgs args)
		{
			// Initialize the data source
			// See: https://aka.dataminer.services/igqioninit-oninit
			_gqiDms = args.DMS;
			_dms = _gqiDms.GetConnection().GetDms();
			_agent = _dms.GetAgents().SingleOrDefault();
			if (_agent == null)
			{
				throw new InvalidOperationException("This operation is only supported in single-agent dataminer systems");
			}

			return new OnInitOutputArgs();
		}

		public GQIArgument[] GetInputArguments()
		{
			// Define data source input arguments
			// See: https://aka.dataminer.services/igqiinputarguments-getinputarguments
			return _arguments.GetInputArguments();
		}

		public OnArgumentsProcessedOutputArgs OnArgumentsProcessed(OnArgumentsProcessedInputArgs args)
		{
			// Process input argument values
			// See: https://aka.dataminer.services/igqiinputarguments-onargumentsprocessed
			return _arguments.OnArgumentsProcessed(args);
		}

		public GQIColumn[] GetColumns()
		{
			// Define data source columns
			// See: https://aka.dataminer.services/igqidatasource-getcolumns
			return new GQIColumn[]
			{
				new GQIStringColumn("Service Id"),
				new GQIStringColumn("Name"),
				new GQIStringColumn("Icon"),
				new GQIBooleanColumn("Monitored"),
				new GQIDateTimeColumn("Start"),
				new GQIDateTimeColumn("End"),
				new GQIStringColumn("Category"),
				new GQIStringColumn("Specification"),
				new GQIIntColumn("Alarm Level"),
			};
		}

		public GQIPage GetNextPage(GetNextPageInputArgs args)
		{
			var serviceDataHelper = new DataHelperService(_gqiDms.GetConnection());
			var service = serviceDataHelper.Read(ServiceExposers.Guid.Equal(_arguments.DomId)).SingleOrDefault();
			if (service == null)
			{
				return new GQIPage(Array.Empty<GQIRow>());
			}

			return new GQIPage(new[] { BuildRow(service) });
		}

		private GQIRow BuildRow(Models.Service service)
		{
			return new GQIRow(new[]
			{
				new GQICell { Value = service.ID.ToString() },
				new GQICell { Value = service.Name },
				new GQICell { Value = service.Icon },
				new GQICell { Value = service.GenerateMonitoringService ?? false },
				new GQICell { Value = service.StartTime?.ToUniversalTime() },
				new GQICell { Value = service.EndTime?.ToUniversalTime() },
				new GQICell { Value = service?.Category?.Name ?? string.Empty },
				new GQICell { Value = GetServiceSpecification(service.ServiceSpecificationId) },
				new GQICell { Value = (int) TryGetAlarmLevel(service) },
			});
		}

		private string GetServiceSpecification(Guid? serviceSpecificationId)
		{
			var helper = new DataHelperServiceSpecification(_gqiDms.GetConnection());
			var specification = helper.Read(ServiceSpecificationExposers.Guid.Equal(serviceSpecificationId.HasValue ? serviceSpecificationId.Value : Guid.Empty)).SingleOrDefault();
			return specification != null ?
				specification.Name
				: string.Empty;
		}

		private AlarmLevel TryGetAlarmLevel(Models.Service service)
		{
			try
			{
				if (_agent.ServiceExists(service.Name))
				{
					return _agent.GetService(service.Name).GetState().Level;
				}
			}
			catch (Exception)
			{
				// do nothing
			}

			return AlarmLevel.Undefined;
		}
	}
}