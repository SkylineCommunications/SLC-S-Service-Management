/*
****************************************************************************
*  Copyright (c),  Skyline Communications NV  All Rights Reserved.    *
****************************************************************************

Revision History:

DATE		VERSION		AUTHOR			COMMENTS

20/06/2025	1.0.0.1		RCA, Skyline	Initial version
****************************************************************************
*/
namespace SLCSMCOGetWorkflowIcon
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using Skyline.DataMiner.Analytics.GenericInterface;
	using Skyline.DataMiner.Net;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.API.Configurations;
	using Skyline.DataMiner.ProjectApi.ServiceManagement.SDM;

	/// <summary>
	///     Represents a data source.
	///     See: https://aka.dataminer.services/gqi-external-data-source for a complete example.
	/// </summary>
	[GQIMetaData(Name = "SLC_SM_CO_GetCharacteristics")]
	public class SLCSMCOGetWorkflowIcon : IGQIColumnOperator, IGQIRowOperator, IGQIOnInit, IGQIInputArguments
	{
		private readonly GQIStringArgument _characteristicsNamesArg = new GQIStringArgument("Characteristics") { IsRequired = true };
		private readonly string _domIdColumnName = "DOM ID";
		private readonly List<GQIStringColumn> _newColumnsList = new List<GQIStringColumn>();
		private List<string> _characteristicNamesList = new List<string>();
		private IConnection _connection;
		private GQIDMS _dms;
		private DataHelpersServiceManagement _serviceHelper;

		public GQIArgument[] GetInputArguments()
		{
			return new GQIArgument[] { _characteristicsNamesArg };
		}

		public void HandleColumns(GQIEditableHeader header)
		{
			// add columns for the characterics indicated by the user
			// column references are stored in separate list _newColumnList
			foreach (var characteristic in _characteristicNamesList)
			{
				GQIStringColumn newColumn = new GQIStringColumn(characteristic);
				_newColumnsList.Add(newColumn);
				header.AddColumns(newColumn);
			}
		}

		public void HandleRow(GQIEditableRow row)
		{
			// fetch the servcie, the characterisctics and add the characteristic values to the columns
			if (!Guid.TryParse(row.GetValue(_domIdColumnName)?.ToString(), out Guid domId))
			{
				return;
			}

			// fetch the service
			FilterElement<Skyline.DataMiner.ProjectApi.ServiceManagement.API.ServiceManagement.Models.Service> filter = ServiceExposers.Guid.Equal(domId);
			var service = _serviceHelper.Services.Read(filter).FirstOrDefault();

			if (service == null)
			{
				return;
			}

			var configs = service.ServiceConfiguration?.Parameters;

			var configValues = configs.Select(c => c.ConfigurationParameter);

			// foreach characteristic, try to get the value and set in the column
			for (int i = 0; i < _characteristicNamesList.Count; i++)
			{
				string characteristicName = _characteristicNamesList[i];
				Guid characteristicId = GetCharacteristicId(characteristicName);

				if (characteristicId == Guid.Empty)
				{
					continue;
				}

				// Set column with value on the service for the particular service ID
				GQIStringColumn characteristicColumn = _newColumnsList[i];
				string characteristicValue = configValues.FirstOrDefault(c => c.ConfigurationParameterId == characteristicId)?.StringValue ?? String.Empty;
				row.SetValue(characteristicColumn, characteristicValue);
			}
		}

		public OnArgumentsProcessedOutputArgs OnArgumentsProcessed(OnArgumentsProcessedInputArgs args)
		{
			// get names of the characteristics to be added and convert into list
			var characteristicsNames = args.GetArgumentValue(_characteristicsNamesArg);

			_characteristicNamesList = characteristicsNames.Split(',').ToList();

			return new OnArgumentsProcessedOutputArgs();
		}

		public OnInitOutputArgs OnInit(OnInitInputArgs args)
		{
			_dms = args.DMS;

			_connection = args.DMS.GetConnection();

			_serviceHelper = new DataHelpersServiceManagement(_connection);

			return default;
		}

		private Guid GetCharacteristicId(string characteristicName)
		{
			// get characteristic ID
			Models.ConfigurationParameter characteric = new DataHelperConfigurationParameter(_connection).Read(ConfigurationParameterExposers.Name.Equal(characteristicName)).FirstOrDefault();

			if (characteric != null)
			{
				return characteric.ID;
			}
			else
			{
				return Guid.Empty;
			}
		}
	}
}