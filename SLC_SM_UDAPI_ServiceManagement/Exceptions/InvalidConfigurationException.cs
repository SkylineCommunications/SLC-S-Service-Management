namespace SLC_SM_UDAPI_ServiceManagement.Exceptions
{
	using System;

	/// <summary>
	/// Thrown when a specification configuration request is invalid (unknown reference, invalid number, missing mandatory value).
	/// The message is safe to show to the caller.
	/// </summary>
	[Serializable]
	public sealed class InvalidConfigurationException : Exception
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="InvalidConfigurationException"/> class.
		/// </summary>
		public InvalidConfigurationException()
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="InvalidConfigurationException"/> class.
		/// </summary>
		/// <param name="message">The caller-safe message.</param>
		public InvalidConfigurationException(string message) : base(message)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="InvalidConfigurationException"/> class.
		/// </summary>
		/// <param name="message">The caller-safe message.</param>
		/// <param name="innerException">The inner exception.</param>
		public InvalidConfigurationException(string message, Exception innerException) : base(message, innerException)
		{
		}

		private InvalidConfigurationException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context) : base(info, context)
		{
		}
	}
}
