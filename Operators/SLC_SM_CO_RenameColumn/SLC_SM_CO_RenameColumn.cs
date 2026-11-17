namespace RenameOperator
{
    using Skyline.DataMiner.Analytics.GenericInterface;

    [GQIMetaData(Name = "Rename column")]
    public class RenameColumnOperator : IGQIColumnOperator, IGQIInputArguments
    {
        // Arguments
        private readonly GQIColumnDropdownArgument _columnArg;
        private readonly GQIStringArgument _newNameArg;

        // Argument values
        private GQIColumn _column;
        private string _newName;

        public RenameColumnOperator()
        {
            _columnArg = new GQIColumnDropdownArgument("Column") { IsRequired = true };
            _newNameArg = new GQIStringArgument("New name") { IsRequired = true };
        }

        public GQIArgument[] GetInputArguments()
        {
            return new GQIArgument[]
            {
                _columnArg,
                _newNameArg,
            };
        }

        public OnArgumentsProcessedOutputArgs OnArgumentsProcessed(OnArgumentsProcessedInputArgs args)
        {
            _column = args.GetArgumentValue(_columnArg);
            _newName = args.GetArgumentValue(_newNameArg);

            return default;
        }

        public void HandleColumns(GQIEditableHeader header)
        {
            header.RenameColumn(_column, _newName);
        }
    }
}
