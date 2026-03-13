/*
****************************************************************************
*  Copyright (c),  Skyline Communications NV  All Rights Reserved.    *
****************************************************************************

Revision History:

DATE        VERSION        AUTHOR            COMMENTS

dd/mm/2025    1.0.0.1        RCA, Skyline    Initial version
****************************************************************************
*/
using System;
using Skyline.DataMiner.Analytics.GenericInterface;

[GQIMetaData(Name = "Service-Management-GQI-CustomOperator-HasValue")]
public class MyCustomOperator : IGQIColumnOperator, IGQIRowOperator, IGQIInputArguments
{
	private readonly GQIStringArgument _argument = new GQIStringArgument("Column Name") { IsRequired = true };
	private readonly GQIBooleanColumn _isEmptyColumn = new GQIBooleanColumn("Has Value");
	private string _columnName = String.Empty;

	public GQIArgument[] GetInputArguments()
	{
		return new GQIArgument[] { _argument };
	}

	public void HandleColumns(GQIEditableHeader header)
	{
		header.AddColumns(_isEmptyColumn);
	}

	public void HandleRow(GQIEditableRow row)
	{
		row.SetValue(_isEmptyColumn, !String.IsNullOrEmpty(row.GetValue(_columnName)?.ToString()));
	}

	public OnArgumentsProcessedOutputArgs OnArgumentsProcessed(OnArgumentsProcessedInputArgs args)
	{
		_columnName = args.GetArgumentValue(_argument);
		return new OnArgumentsProcessedOutputArgs();
	}
}