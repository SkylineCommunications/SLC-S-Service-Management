namespace SLC_SM_RT_ServiceOrderItemRejection.TestCases
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	/// <summary>
	/// The outcome of a sub script run.
	/// </summary>
	internal sealed class SubScriptOutcome
	{
		public SubScriptOutcome(bool hadError, IEnumerable<string> errorMessages)
		{
			HadError = hadError;
			ErrorMessages = (errorMessages ?? Enumerable.Empty<string>()).Where(m => !String.IsNullOrWhiteSpace(m)).ToList();
		}

		public bool HadError { get; }

		public IReadOnlyList<string> ErrorMessages { get; }

		/// <summary>
		/// Gets a one-line description of the outcome, for use in failure messages.
		/// </summary>
		/// <returns>The description.</returns>
		public string Describe()
		{
			return HadError
				? $"script reported an error: [{String.Join(" | ", ErrorMessages)}]"
				: "script reported no error";
		}
	}
}
