namespace SLC_SM_IAS_Profiles.Views
{
	using System;
	using DomHelpers.SlcConfigurations;
	using Skyline.DataMiner.Utils.InteractiveAutomationScript;
	using SLC_SM_IAS_Profiles.Presenters;

	public abstract class Row
	{
		protected Row(RowData data)
		{
			Data = data;

			MultipleAllowed = new CheckBox() { IsEnabled = data.Record.RecordType == RecordType.Reference };
		}

		public RowData Data { get; set; }

		public CheckBox MultipleAllowed { get; protected set; }

		public CheckBox Mandatory { get; protected set; }

		public abstract Row Configure();

		public abstract void BuildRow(Dialog view);
	}

	public class RowFactory
	{
		public static Row Create(RowData data)
		{
			if (data is ConfigurationRowData)
			{
				var rowData = (ConfigurationRowData)data;
				var configurationRecord = rowData.Record;
				switch (configurationRecord.ConfigurationParameter.Type)
				{
					case SlcConfigurationsIds.Enums.Type.Number:
						{
							return new NumberRow(rowData);
						}

					case SlcConfigurationsIds.Enums.Type.Text:
						{
							return new TextRow(rowData);
						}

					case SlcConfigurationsIds.Enums.Type.Discrete:
						{
							return new DiscreteRow(rowData);
						}

					default:
						throw new NotSupportedException($"Configuration parameter type: {configurationRecord.ConfigurationParameter.Type} is not supported");
				}
			}
			else if (data is ProfileDefinitionRowData)
			{
				var rowData = (ProfileDefinitionRowData)data;
				return new ProfileDefinitionRow(rowData);
			}
			else
			{
				throw new NotSupportedException($"Row data of type {data.GetType().Name} is not supported");
			}
		}
	}
}
