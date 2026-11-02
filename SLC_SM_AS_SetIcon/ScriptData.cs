namespace SLC_SM_AS_SetIcon
{
	using System;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.ServiceManagement.Common.Extensions;

	internal class ScriptData
	{
		private IEngine _engine;

		public ScriptData(IEngine engine)
		{
			_engine = engine;
			LoadParameters();
		}

		internal enum ObjectType
		{
			ServiceCategory,
			Service,
			ServiceItem,
		}

		public ObjectType Type { get; set; }

		public string Name { get; set; }

		public Guid DomId { get; set; }

		private void LoadParameters()
		{
			var paramType = _engine.ReadScriptParamFromApp("Type");
			if (!Enum.TryParse(paramType, out ObjectType type))
			{
				throw new InvalidOperationException($"Invalid object type: {paramType}");
			}

			Type = type;
			DomId = _engine.ReadScriptParamFromApp<Guid>("DomId");
			Name = _engine.ReadScriptParamFromApp("Path");
		}
	}
}
