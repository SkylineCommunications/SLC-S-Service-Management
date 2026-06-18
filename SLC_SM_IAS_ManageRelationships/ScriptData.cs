/*
****************************************************************************
*  Copyright (c),  Skyline Communications NV  All Rights Reserved.    *
****************************************************************************

Revision History:

DATE		VERSION		AUTHOR			COMMENTS

30/05/2025	1.0.0.1		RCA, Skyline	Initial version
****************************************************************************
*/

namespace SLCSMIASManageRelationships
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.ServiceManagement.Common.Extensions;

	public class ScriptData
	{
		private readonly IEngine _engine;

		public ScriptData(IEngine engine)
		{
			_engine = engine;
			LoadScriptParameters();
		}

		public Guid DomId { get; set; }

		public HashSet<string> ServiceIds { get; set; }

		public string DefinitionReference { get; set; }

		public string Type { get; set; }

		public bool HasDefinitionReference => !String.IsNullOrEmpty(DefinitionReference);

		public void Validate()
		{
			if (String.IsNullOrEmpty(DefinitionReference) && ServiceIds.Count < 2)
				throw new InvalidOperationException("Select a minimum of 2 service items to make a connection");
		}

		public override string ToString()
		{
			return $@"Dom ID:			{DomId}
Service IDs:	{String.Join(", ", ServiceIds)}
Def Ref:		{DefinitionReference}
Has Def Ref:	{HasDefinitionReference}
Type:			{Type}";
		}

		private void LoadScriptParameters()
		{
			DomId = _engine.ReadScriptParamFromApp<Guid>("DomId");

			ServiceIds = _engine.ReadScriptParamsFromApp("ServiceItemIds").ToHashSet();

			DefinitionReference = _engine.ReadScriptParamFromApp("DefinitionReference");

			Type = _engine.ReadScriptParamFromApp("Type");
		}
	}
}