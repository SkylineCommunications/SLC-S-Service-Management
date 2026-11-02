namespace SLC_SM_IAS_Service_Configuration.Model
{
	public class ProfileOption
	{
		public ProfileOption(string id, string name, bool isProfileDefinition)
		{
			Id = id;
			Name = name;
			IsProfileDefinition = isProfileDefinition;
		}

		public string Id { get; set; }

		public string Name { get; set; }

		public bool IsProfileDefinition { get; set; }
	}
}
