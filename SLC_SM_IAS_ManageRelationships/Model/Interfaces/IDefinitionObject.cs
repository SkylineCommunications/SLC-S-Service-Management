namespace SLC_SM_IAS_ManageRelationships.Controller
{
	using System.Collections.Generic;
	using DomHelpers.SlcWorkflow;

	internal interface IDefinitionObject
	{
		IEnumerable<NodesSection> GetAvailableInputs();

		IEnumerable<NodesSection> GetAvailableOutputs();
	}
}
