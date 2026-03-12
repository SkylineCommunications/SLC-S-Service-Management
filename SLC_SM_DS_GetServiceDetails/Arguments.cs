namespace SLCSMDSGetServiceDetails
{
	using System;
	using Skyline.DataMiner.Analytics.GenericInterface;

	internal class Arguments : IGQIInputArguments
	{
		private readonly GQIStringArgument domIdArg = new GQIStringArgument("Service Dom Id") { IsRequired = false };

		public Guid DomId { get; set; }

		public GQIArgument[] GetInputArguments()
		{
			return new GQIArgument[]
			{
				domIdArg,
			};
		}

		public OnArgumentsProcessedOutputArgs OnArgumentsProcessed(OnArgumentsProcessedInputArgs args)
		{
			DomId = Guid.TryParse(args.GetArgumentValue(domIdArg), out var domId)
				? domId
				: Guid.Empty;

			return new OnArgumentsProcessedOutputArgs();
		}
	}
}