namespace Terraingen.Combat;

using Terraingen.AI;
using Terraingen.Animals;
using Terraingen.Multiplayer;
using Terraingen.Player;

/// <summary>Attacker/victim faction resolution for damage permission checks.</summary>
public static class ThornsCombatFactions
{
	public enum FactionKind : byte
	{
		Neutral,
		Player,
		Wildlife,
		TamedAnimal,
		Bandit,
		Boss,
		World
	}

	public static FactionKind ResolveFaction( GameObject root )
	{
		if ( !root.IsValid() )
			return FactionKind.Neutral;

		root = ResolvePlayerPawnRootIfAny( root );

		var bandit = root.Components.Get<ThornsBanditBrain>( FindMode.EverythingInSelfAndParent );
		if ( bandit is not null && bandit.IsValid() )
			return FactionKind.Bandit;

		var brain = root.Components.Get<ThornsAnimalBrain>( FindMode.EverythingInSelfAndParent );
		if ( brain is not null && brain.IsValid() )
			return brain.IsTamed ? FactionKind.TamedAnimal : FactionKind.Wildlife;

		var gameplay = root.Components.Get<ThornsPlayerGameplay>( FindMode.EverythingInSelfAndParent );
		if ( gameplay is not null && gameplay.IsValid() )
			return FactionKind.Player;

		if ( ThornsLocalPlayer.IsPlayerPawnRoot( root ) )
			return FactionKind.Player;

		return FactionKind.Neutral;
	}

	public static string ResolveAccountKey( GameObject root )
	{
		if ( !root.IsValid() )
			return "";

		var gameplay = root.Components.Get<ThornsPlayerGameplay>( FindMode.EnabledInSelf );
		if ( gameplay is not null && gameplay.IsValid() && !string.IsNullOrWhiteSpace( gameplay.AccountKey ) )
			return gameplay.AccountKey;

		return ThornsPersistenceIdentity.GetStableAccountKey( root ) ?? "";
	}

	public static bool HostCanDamage( GameObject attackerRoot, GameObject victimRoot, in ThornsCombatDamage.DamageInfo info )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || !victimRoot.IsValid() || info.Amount <= 0f )
			return false;

		if ( !attackerRoot.IsValid() )
			return info.DamageTypeId is "starvation" or "environment" or "world";

		if ( attackerRoot == victimRoot )
			return false;

		attackerRoot = ResolvePlayerPawnRootIfAny( attackerRoot );
		victimRoot = ResolvePlayerPawnRootIfAny( victimRoot );

		var attackerFaction = info.AttackerFaction != FactionKind.Neutral
			? info.AttackerFaction
			: ResolveFaction( attackerRoot );
		var victimFaction = info.VictimFaction != FactionKind.Neutral
			? info.VictimFaction
			: ResolveFaction( victimRoot );

		var victimBrain = victimRoot.Components.Get<ThornsAnimalBrain>( FindMode.EverythingInSelfAndParent );
		if ( victimBrain is not null && victimBrain.IsValid() && ThornsAnimalCombatRules.ShouldIgnoreDamage( victimBrain, attackerRoot ) )
			return false;

		if ( ThornsAnimalCombatRules.IsDamageFromOwnersTame( victimRoot, attackerRoot ) )
			return false;

		if ( attackerFaction == FactionKind.Player && victimFaction == FactionKind.Player )
		{
			if ( !ThornsCombatSettings.EnablePvPDamage )
				return false;

			if ( ThornsCombatSettings.BlockGuildFriendlyFire && ShareGuild( attackerRoot, victimRoot ) )
				return false;
		}

		if ( attackerFaction == FactionKind.Player && victimFaction == FactionKind.Wildlife )
			return true;

		if ( attackerFaction == FactionKind.TamedAnimal && victimFaction == FactionKind.Wildlife )
			return true;

		if ( attackerFaction == FactionKind.Wildlife && victimFaction == FactionKind.Player )
			return true;

		if ( attackerFaction == FactionKind.Wildlife && victimFaction == FactionKind.Wildlife )
			return true;

		if ( attackerFaction == FactionKind.Wildlife && victimFaction == FactionKind.Bandit )
			return true;

		if ( attackerFaction == FactionKind.TamedAnimal && victimFaction == FactionKind.Bandit )
			return true;

		if ( attackerFaction == FactionKind.Wildlife && victimFaction == FactionKind.TamedAnimal )
			return true;

		if ( attackerFaction == FactionKind.TamedAnimal && victimFaction == FactionKind.TamedAnimal )
			return true;

		if ( attackerFaction == FactionKind.Bandit || victimFaction == FactionKind.Bandit )
			return true;

		var attackerIsHostileNpc = attackerFaction is FactionKind.Wildlife or FactionKind.TamedAnimal or FactionKind.Bandit;
		var victimIsHostileNpc = victimFaction is FactionKind.Wildlife or FactionKind.TamedAnimal or FactionKind.Bandit;
		if ( attackerIsHostileNpc && victimIsHostileNpc )
			return true;

		if ( attackerFaction == FactionKind.Boss || victimFaction == FactionKind.Boss )
			return true;

		return info.DamageTypeId is "starvation" or "environment" or "world";
	}

	static bool ShareGuild( GameObject a, GameObject b )
	{
		var keyA = ResolveAccountKey( a );
		var keyB = ResolveAccountKey( b );
		if ( string.IsNullOrWhiteSpace( keyA ) || string.IsNullOrWhiteSpace( keyB ) || keyA == keyB )
			return keyA == keyB && !string.IsNullOrWhiteSpace( keyA );

		var service = ThornsGuildWorldService.Instance;
		if ( service is null )
			return false;

		if ( !service.TryGetAccountGuildId( keyA, out var guildA ) || !service.TryGetAccountGuildId( keyB, out var guildB ) )
			return false;

		return guildA == guildB;
	}

	static GameObject ResolvePlayerPawnRootIfAny( GameObject root )
	{
		if ( !root.IsValid() || !ThornsLocalPlayer.IsPlayerPawnRoot( root ) )
			return root;

		return ThornsLocalPlayer.ResolvePawnRoot( root );
	}
}
