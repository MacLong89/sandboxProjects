namespace Terraingen.Combat;

using Terraingen.World;

/// <summary>Legacy hold-loot component — interaction prompts only; loot uses container UI.</summary>
[Title( "Thorns Player Airdrop Use" )]
[Category( "Player" )]
public sealed class ThornsPlayerAirdropUse : Component
{
	public bool HasLootTargetInFront() =>
		ThornsAirdropWorldService.Instance?.HasTargetInFront( GameObject ) == true;
}
