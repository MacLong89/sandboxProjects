namespace Terraingen.Animals;

using Terraingen;
using Terraingen.Multiplayer;

/// <summary>Host-side alerts so tamed animals join the owner's fights.</summary>
public static class ThornsAnimalCompanion
{
	public static void NotifyOwnerMarkedTarget( GameObject owner, GameObject victim )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || !owner.IsValid() || !victim.IsValid() )
			return;

		var ownerKey = ThornsPersistenceIdentity.GetStableAccountKey( owner );
		if ( string.IsNullOrEmpty( ownerKey ) )
			return;

		foreach ( var brain in ThornsAnimalManager.AnimalRegistry )
		{
			if ( !brain.IsValid() || !brain.IsTamed || brain.IsDead || brain.TamedOwnerAccountKey != ownerKey )
				continue;

			brain.HostAlertOwnerMarkedTarget( victim );
		}
	}

	public static void NotifyOwnerThreat( GameObject owner, GameObject attacker )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || !owner.IsValid() || !attacker.IsValid() )
			return;

		if ( attacker == owner || attacker.Root == owner )
			return;

		var ownerKey = ThornsPersistenceIdentity.GetStableAccountKey( owner );
		if ( string.IsNullOrEmpty( ownerKey ) )
			return;

		foreach ( var brain in ThornsAnimalManager.AnimalRegistry )
		{
			if ( !brain.IsValid() || !brain.IsTamed || brain.IsDead || brain.TamedOwnerAccountKey != ownerKey )
				continue;

			brain.HostAlertOwnerThreat( attacker );
		}
	}

	/// <summary>Stops other owner tames that were hunting an animal that just became a tame.</summary>
	public static void NotifyOwnerTamedAnimal( GameObject tamedAnimal, string ownerKey )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || !tamedAnimal.IsValid() || string.IsNullOrEmpty( ownerKey ) )
			return;

		foreach ( var brain in ThornsAnimalManager.AnimalRegistry )
		{
			if ( !brain.IsValid() || !brain.IsTamed || brain.IsDead || brain.TamedOwnerAccountKey != ownerKey )
				continue;

			if ( brain.GameObject == tamedAnimal )
				continue;

			brain.HostStopEngaging( tamedAnimal );
		}
	}

	/// <summary>Stops all tames that were hunting an animal now in awaiting-tame state.</summary>
	public static void NotifyStopAttacking( GameObject victim )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || !victim.IsValid() )
			return;

		foreach ( var brain in ThornsAnimalManager.AnimalRegistry )
		{
			if ( !brain.IsValid() || !brain.IsTamed || brain.IsDead )
				continue;

			brain.HostStopEngaging( victim );
		}
	}
}
