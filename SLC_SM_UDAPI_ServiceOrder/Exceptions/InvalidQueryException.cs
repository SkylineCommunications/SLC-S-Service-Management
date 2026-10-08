namespace SLC_SM_UDAPI_ServiceOrder.Exceptions
{
	using System;

	using SLC_SM_UDAPI_ServiceOrder.Models;

	/// <summary>
	/// Thrown when the <see cref="ListServiceOrdersQuery"/> input is invalid. The message is safe to show to the caller.
	/// </summary>
	[Serializable]
	public sealed class InvalidQueryException : Exception
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="InvalidQueryException"/> class.
		/// </summary>
		public InvalidQueryException()
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="InvalidQueryException"/> class.
		/// </summary>
		/// <param name="message">The caller-safe message.</param>
		public InvalidQueryException(string message) : base(message)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="InvalidQueryException"/> class.
		/// </summary>
		/// <param name="message">The caller-safe message.</param>
		/// <param name="innerException">The inner exception.</param>
		public InvalidQueryException(string message, Exception innerException) : base(message, innerException)
		{
		}

		private InvalidQueryException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context) : base(info, context)
		{
		}
	}
}
