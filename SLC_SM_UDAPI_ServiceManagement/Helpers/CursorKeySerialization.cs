namespace SLC_SM_UDAPI_ServiceManagement.Helpers
{
	using System;
	using System.Globalization;

	/// <summary>
	/// Serializes stable cursor sort keys for the UDAPI response contract.
	/// </summary>
	public static class CursorKeySerialization
	{
		/// <summary>Serializes a UTC creation timestamp.</summary>
		public static string SerializeDateTimeOffset(DateTimeOffset value)
		{
			return value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
		}

		/// <summary>Deserializes a UTC creation timestamp.</summary>
		public static DateTimeOffset DeserializeDateTimeOffset(string value)
		{
			return DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
		}

		/// <summary>Serializes a text cursor key.</summary>
		public static string SerializeString(string value)
		{
			return value ?? String.Empty;
		}

		/// <summary>Deserializes a text cursor key.</summary>
		public static string DeserializeString(string value)
		{
			return value ?? String.Empty;
		}
	}
}
