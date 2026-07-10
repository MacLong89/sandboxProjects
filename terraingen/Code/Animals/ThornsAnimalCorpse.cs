namespace Terraingen.Animals;

/// <summary>Interactable-ready corpse hook — loot/XP/taming later.</summary>
[Title( "Thorns Animal Corpse" )]
[Category( "Thorns/Animals" )]
public sealed class ThornsAnimalCorpse : Component
{
	[Property] public bool CanInteract { get; set; } = true;

	public bool HostTryLoot( Connection player )
	{
		_ = player;
		return false;
	}
}
