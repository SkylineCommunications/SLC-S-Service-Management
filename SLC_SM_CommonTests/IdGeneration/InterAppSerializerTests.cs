namespace SLC_SM_CommonTests.IdGeneration
{
	using Skyline.DataMiner.Core.InterAppCalls.Common.CallBulk;
	using Skyline.DataMiner.Core.InterAppCalls.Common.CallSingle;
	using Skyline.DataMiner.Core.InterAppCalls.Common.Shared;

	using SLC_SM_Common.IdGeneration;

	[TestClass]
	public class InterAppSerializerTests
	{
		// Reply as serialized by the connector's InterAppCalls.Common 1.1.1.1 default serializer (Message.Reply).
		private const string ConnectorReply =
			"{\"$id\":\"1\",\"$type\":\"IdReservationResponse, \",\"brokerReturnAddress\":null,\"error\":null,\"expectsReply\":false," +
			"\"guid\":\"60762ba2-18a7-4fc6-806b-ec046d3aae77\",\"returnAddress\":null,\"source\":null,\"value\":43}";

		private static InterAppSerializer CreateSerializer() => new InterAppSerializer(new[] { typeof(IdReservationRequest), typeof(IdReservationResponse) });

		[TestMethod]
		public void DeserializesConnectorReply()
		{
			var response = MessageFactory.CreateFromRaw(ConnectorReply, CreateSerializer()) as IdReservationResponse;

			Assert.IsNotNull(response);
			Assert.AreEqual(43L, response.Value);
			Assert.IsNull(response.Error);
		}

		[TestMethod]
		public void SerializesCallInInterAppFormat()
		{
			var call = InterAppCallFactory.CreateNew();
			call.ReturnAddress = new ReturnAddress(1, 2, 9000001);
			call.Messages.Add(new IdReservationRequest { Kind = "Service", Floor = 1, Allocate = true, ReturnAddress = call.ReturnAddress });

			string raw = CreateSerializer().SerializeToString(call);

			StringAssert.Contains(raw, "\"$type\":\"IdReservationRequest, \"");
			StringAssert.Contains(raw, "\"kind\":\"Service\"");
			StringAssert.Contains(raw, "\"parameterId\":9000001");
		}

		[TestMethod]
		public void RejectsUnknownTypes()
		{
			Assert.ThrowsException<ArgumentException>(() => CreateSerializer().DeserializeFromString<Message>("{\"$type\":\"System.IO.FileInfo, mscorlib\"}"));
		}
	}
}
