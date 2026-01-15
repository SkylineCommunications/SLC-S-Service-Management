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