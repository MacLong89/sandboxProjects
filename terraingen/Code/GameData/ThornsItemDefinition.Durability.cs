namespace Terraingen.GameData;

/// <summary>Tool durability tuning for harvest gear (weapons use <see cref="Combat.ThornsWeaponDefinitions"/>).</summary>
public sealed partial class ThornsItemDefinition
{
	public float ToolMaxDurability { get; set; }
	public float ToolDurabilityLossPerStrike { get; set; }
}
