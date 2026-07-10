namespace Terraingen.Animals;

using Terraingen.Multiplayer;
using Terraingen.Combat;
using Terraingen.Player;

/// <summary>Blocks damage between a player and their own tamed animals.</summary>
public static class ThornsAnimalCombatRules
{
	public static bool ShouldIgnoreDamage( ThornsAnimalBrain victim, GameObject attackerRoot )
	{
		if ( victim is null || !victim.IsValid() || !victim.IsTamed
		     || string.IsNullOrEmpty( victim.TamedOwnerAccountKey ) || !attackerRoot.IsValid() )
			return false;

		return SharesTameOwner( victim.TamedOwnerAccountKey, attackerRoot );
	}

	public static bool IsDamageFromOwnersTame( GameObject playerRoot, GameObject attackerRoot )
	{
		if ( !playerRoot.IsValid() || !attackerRoot.IsValid() )
			return false;

		var playerHealth = playerRoot.Components.Get<ThornsPlayerHealth>( FindMode.EverythingInSelfAndDescendants );
		var playerGameplay = playerRoot.Components.Get<ThornsPlayerGameplay>( FindMode.EverythingInSelfAndDescendants );
		if ( (playerHealth is null || !playerHealth.IsValid())
		     && (playerGameplay is null || !playerGameplay.IsValid()) )
			return false;

		var attackerBrain = attackerRoot.Components.Get<ThornsAnimalBrain>( FindMode.EverythingInSelfAndParent );
		if ( attackerBrain is null || !attackerBrain.IsValid() || !attackerBrain.IsTamed
		     || string.IsNullOrEmpty( attackerBrain.TamedOwnerAccountKey ) )
			return false;

		var playerKey = ThornsPersistenceIdentity.GetStableAccountKey( playerRoot );
		return !string.IsNullOrEmpty( playerKey )
		       && playerKey == attackerBrain.TamedOwnerAccountKey;
	}

	static bool SharesTameOwner( string ownerKey, GameObject attackerRoot )
	{
		var attackerBrain = attackerRoot.Components.Get<ThornsAnimalBrain>( FindMode.EverythingInSelfAndParent );
		if ( attackerBrain is not null && attackerBrain.IsValid() && attackerBrain.IsTamed )
			return attackerBrain.TamedOwnerAccountKey == ownerKey;

		var playerKey = ThornsPersistenceIdentity.GetStableAccountKey( attackerRoot );
		return !string.IsNullOrEmpty( playerKey ) && playerKey == ownerKey;
	}
}
