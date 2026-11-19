namespace Skyline.DataMiner.Utils.ServiceManagement.Testing.Tests
{
	using System;

	/// <summary>
	/// Exception that carries a functional (expected) regression test failure.
	/// Only the message is reported, without stack trace, because the failure is a verdict and not a crash.
	/// </summary>
	[Serializable]
	public class TestCaseException : Exception
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="TestCaseException"/> class.
		/// </summary>
		public TestCaseException()
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="TestCaseException"/> class.
		/// </summary>
		/// <param name="message">The failure description that is reported as the test case result info.</param>
		public TestCaseException(string message)
			: base(message)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="TestCaseException"/> class.
		/// </summary>
		/// <param name="message">The failure description that is reported as the test case result info.</param>
		/// <param name="innerException">The exception that caused the failure.</param>
		public TestCaseException(string message, Exception innerException)
			: base(message, innerException)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="TestCaseException"/> class.
		/// </summary>
		/// <param name="info">The serialization info.</param>
		/// <param name="context">The streaming context.</param>
		protected TestCaseException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context)
			: base(info, context)
		{
		}
	}
}
