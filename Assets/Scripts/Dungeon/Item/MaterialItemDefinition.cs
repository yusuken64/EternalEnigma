// Sellable crafting material (gathering, disarmed traps). Instances come from MaterialCatalog.
public class MaterialItemDefinition : ItemDefinition
{
	public int SellValue;
	public bool HasKind;
	public EternalEnigma.Core.World.GatheringKind Kind;

	internal override InventoryItem AsInventoryItem(int? stock) => new MaterialInventoryItem(this, stock);
}
