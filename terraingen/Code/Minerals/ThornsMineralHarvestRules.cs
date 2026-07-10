namespace Terraingen.Minerals;

/// <summary>Host-authoritative stone/ore node tuning (mirrors tree chop).</summary>
public static class ThornsMineralHarvestRules
{
	public const int HitsToBreak = 5;
	public const int StonePerHit = 3;
	public const int StonePerSalvage = 1;
	public const int OrePerHit = 2;
	public const int StoneBonusOnBreak = 5;
	public const int OreBonusOnBreak = 3;
	public const float RespawnSeconds = 300f;

	public static string ResourceItemId( MineralKind kind ) =>
		kind == MineralKind.Ore ? "metal_ore" : "stone";

	public static int YieldPerHit( MineralKind kind ) =>
		kind == MineralKind.Ore ? OrePerHit : StonePerHit;

	public static int BonusOnBreak( MineralKind kind ) =>
		kind == MineralKind.Ore ? OreBonusOnBreak : StoneBonusOnBreak;
}
