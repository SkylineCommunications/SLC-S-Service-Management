namespace SLCSMDSGetNodeEdgeServices
{
	using System;
	using Skyline.DataMiner.Analytics.GenericInterface;

	internal class Arguments : IGQIInputArguments
	{
		private readonly GQIStringArgument domIdArg = new GQIStringArgument("Service Dom Id") { IsRequired = false };
		private readonly GQIStringDropdownArgument nodeOrEdgeArg = new GQIStringDropdownArgument("Node or Edge", new[] { "Node", "Edge" });

		public Guid DomId { get; set; }

		public string NodeOrEdge { get; set; }

		public GQIArgument[] GetInputArguments()
		{
			return new GQIArgument[]
			{
				domIdArg,
				nodeOrEdgeArg,
			};
		}

		public OnArgumentsProcessedOutputArgs OnArgumentsProcessed(OnArgumentsProcessedInputArgs args)
		{
			DomId = Guid.TryParse(args.GetArgumentValue(domIdArg), out var domId)
				? domId
				: Guid.Empty;

			NodeOrEdge = args.GetArgumentValue(nodeOrEdgeArg);

			return new OnArgumentsProcessedOutputArgs();
		}
	}
}
