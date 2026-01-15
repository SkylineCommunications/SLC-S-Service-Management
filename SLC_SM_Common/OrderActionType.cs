namespace Library
{
	/// <summary>
	///     Defines the type of action that needs to be run for a given service order.
	/// </summary>
	public enum OrderActionType
	{
		/// <summary>
		///     Indicates an add action should be performed.
		/// </summary>
		Add,

		/// <summary>
		///     Indicates a modify action should be performed.
		/// </summary>
		Modify,

		/// <summary>
		///     Indicates a delete action should be performed.
		/// </summary>
		Delete,

		/// <summary>
		///     Indicates no action should be performed.
		/// </summary>
		NoChange,

		/// <summary>
		///     Indicates the action type is undefined.
		/// </summary>
		Undefined,
	}
}