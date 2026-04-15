namespace SLC_SM_IAS_Service_Configuration.Model
{
	using System;

	public class ProfileOption
	{
		public ProfileOption(Guid id, string name, bool isProfileDefinition)
		{
			Id = id;
			Name = name;
			IsProfileDefinition = isProfileDefinition;
		}

		public Guid Id { get; set; }

		public string Name { get; set; }

		public bool IsProfileDefinition { get; set; }
	}
}
