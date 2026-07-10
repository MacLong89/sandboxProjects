namespace Terraingen.Combat;

using Terraingen.World;

/// <summary>Legacy hold-loot component — interaction prompts only; loot uses container UI.</summary>
[Title( "Thorns Player Death Crate Use" )]
[Category( "Player" )]
public sealed class ThornsPlayerDeathCrateUse : Component
{
	public bool HasLootTargetInFront() =>
		ThornsDeathCrateWorldService.Instance?.HasTargetInFront( GameObject ) == true;
}
