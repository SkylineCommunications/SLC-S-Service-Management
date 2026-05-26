namespace SLC_SM_IAS_Service_Configuration.Model.DataRecords
{
	using Newtonsoft.Json;

	public class ScriptParameters
	{
		public class ScriptParameterUpdate
		{
			[JsonProperty("profileName")]
			public string ProfileName { get; set; }

			[JsonProperty("paramLabel")]
			public string ParamLabel { get; set; }

			[JsonProperty("value")]
			public string Value { get; set; }
		}
	}
}
