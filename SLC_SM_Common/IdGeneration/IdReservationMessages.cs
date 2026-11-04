namespace SLC_SM_Common.IdGeneration
{
	using Skyline.DataMiner.Core.InterAppCalls.Common.CallSingle;

	// InterApp resolves message types by class name, so these classes must keep the same names and properties as
	// IdReservationRequest/IdReservationResponse in the Skyline Service Management Mediation connector.

	/// <summary>
	/// Asks the Service Management Mediation element to reserve or raise an identifier counter.
	/// </summary>
	public class IdReservationRequest : Message
	{
		/// <summary>Gets or sets the counter: <c>Service</c> or <c>ServiceOrder</c>.</summary>
		public string Kind { get; set; }

		/// <summary>Gets or sets the lowest number that may be issued.</summary>
		public long Floor { get; set; }

		/// <summary>Gets or sets a value indicating whether to allocate the next number (<see langword="true"/>) or reset the counter to <see cref="Floor"/> (<see langword="false"/>).</summary>
		public bool Allocate { get; set; }
	}

	/// <summary>
	/// The reserved identifier number (or current counter value when not allocating).
	/// </summary>
	public class IdReservationResponse : Message
	{
		/// <summary>Gets or sets the allocated number (or current counter value when <see cref="IdReservationRequest.Allocate"/> is <see langword="false"/>).</summary>
		public long Value { get; set; }

		/// <summary>Gets or sets the error, or <see langword="null"/> on success.</summary>
		public string Error { get; set; }
	}
}
