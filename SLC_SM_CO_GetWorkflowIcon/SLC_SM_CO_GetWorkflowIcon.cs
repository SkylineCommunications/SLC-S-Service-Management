/*
****************************************************************************
*  Copyright (c),  Skyline Communications NV  All Rights Reserved.    *
****************************************************************************

Revision History:

DATE		VERSION		AUTHOR			COMMENTS

20/06/2025	1.0.0.1		, Skyline	Initial version
****************************************************************************
*/

namespace SLCSMCOGetWorkflowIcon
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using DomHelpers.SlcProperties;
	using DomHelpers.SlcWorkflow;
	using Library.Dom;
	using Skyline.DataMiner.Analytics.GenericInterface;
	using Skyline.DataMiner.Net.Apps.DataMinerObjectModel;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using SLC_SM_Common.Extensions;

	/// <summary>
	/// Represents a data source.
	/// See: https://aka.dataminer.services/gqi-external-data-source for a complete example.
	/// </summary>
	[GQIMetaData(Name = DataSourceName)]
	public class SLCSMCOGetWorkflowIcon : IGQIColumnOperator, IGQIRowOperator, IGQIOnInit, IGQIInputArguments
	{
		private const string DataSourceName = "SLC_SM_CO_GetWorkflowIcon";
		private readonly GQIStringColumn _iconColumn = new GQIStringColumn("Icon");

		private readonly GQIStringArgument _argWorkflowIdColumnName = new GQIStringArgument("Workflow ID Column Name") { IsRequired = true };
		private string _workflowIdColumnName = String.Empty;

		private GQIDMS _dms;
		private DomHelper _wfDomHelper;

		private ICollection<PropertyValuesInstance> _propertyValues;

		public GQIArgument[] GetInputArguments()
		{
			return new GQIArgument[] { _argWorkflowIdColumnName };
		}

		public void HandleColumns(GQIEditableHeader header)
		{
			header.AddColumns(_iconColumn);
		}

		public void HandleRow(GQIEditableRow row)
		{
			try
			{
				var workflowId = Guid.Parse(row.GetValue(_workflowIdColumnName).ToString());

				var workflow = _wfDomHelper.DomInstances
					.Read(DomInstanceExposers.Id.Equal(workflowId))
					.FirstOrDefault();

				var icon = workflow != null
					? FetchWorkflowCategory(new WorkflowsInstance(workflow))
					: String.Empty;

				row.SetValue(_iconColumn, icon);
			}
			catch (Exception ex)
			{
				_dms.GenerateInformationMessage($"{DataSourceName}|Could not fetch icon for workflow ID '{_workflowIdColumnName}' due to: {ex}");
				row.SetValue(_iconColumn, String.Empty);
			}
		}

		public OnArgumentsProcessedOutputArgs OnArgumentsProcessed(OnArgumentsProcessedInputArgs args)
		{
			_workflowIdColumnName = args.GetArgumentValue(_argWorkflowIdColumnName);
			return new OnArgumentsProcessedOutputArgs();
		}

		public OnInitOutputArgs OnInit(OnInitInputArgs args)
		{
			_dms = args.DMS;
			_wfDomHelper = new DomHelper(_dms.SendMessages, SlcWorkflowIds.ModuleId);

			_propertyValues = PropertyExtensions.GetIcons(_dms.SendMessages);

			return default;
		}

		private string FetchWorkflowCategory(WorkflowsInstance workflow)
		{
			return _propertyValues
				.FirstOrDefault(p => p.PropertyValueInfo.LinkedObjectID == workflow.ID.Id.ToString())?
				.PropertyValues.FirstOrDefault(v => v.PropertyName == "Icon")?.Value ?? String.Empty;
		}
	}
}
