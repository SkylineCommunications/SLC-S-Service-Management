namespace Library
{
	public static class Defaults
	{
		public static readonly string ReferenceUnknown = "Reference Unknown";
		public static readonly int DialogMinWidth = 850;
		public static readonly int WidgetWidth = 300;

		public static readonly string SymbolPlus = "➕";
		public static readonly string SymbolMin = "➖";
		public static readonly string SymbolCross = "❌";

		public enum ScriptAction_CreateServiceInventoryItem
		{
			Add,
			AddItem,
			AddItemSilent,
			Edit,
		}
	}
}