namespace Terraingen.Combat;

using Terraingen.AI;
using Terraingen.Animals;
using Terraingen.Multiplayer;
using Terraingen.Player;

/// <summary>Shared authoritative damage pipeline for players, wildlife, and future NPCs.</summary>
public static class ThornsCombatDamage
{
	public enum VictimKind : byte
	{
		Unknown,
		Player,
		Animal,
		Npc
	}

	public readonly struct DamageInfo
	{
		public float Amount { get; init; }
		public GameObject AttackerRoot { get; init; }
		public GameObject VictimRoot { get; init; }
		public VictimKind VictimKind { get; init; }
		public ThornsCombatFactions.FactionKind AttackerFaction { get; init; }
		public ThornsCombatFactions.FactionKind VictimFaction { get; init; }
		public bool IsHeadshot { get; init; }
		public bool IsCritical { get; init; }
		public string DamageTypeId { get; init; }
		public string WeaponId { get; init; }
		public Vector3 HitPosition { get; init; }
		public Vector3 HitNormal { get; init; }
		public string AttackerAccountKey { get; init; }
		public string VictimAccountKey { get; init; }
	}

	public readonly struct DamageResult
	{
		public bool Applied { get; init; }
		public bool Killed { get; init; }
		public VictimKind VictimKind { get; init; }
		public float DamageDealt { get; init; }
	}

	public static DamageResult HostApplyDamage( GameObject victimRoot, in DamageInfo info )
		=> HostApplyDamage( info.AttackerRoot, victimRoot, info );

	public static DamageResult HostApplyDamage( GameObject attackerRoot, GameObject victimRoot, in DamageInfo info )
	{
		var result = HostApplyDamageInternal( attackerRoot, victimRoot, info );
		ThornsCombatHitFxWorldService.HostNotifyDamageApplied( victimRoot?.Scene, in info, in result );
		return result;
	}

	static DamageResult HostApplyDamageInternal( GameObject attackerRoot, GameObject victimRoot, in DamageInfo info )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || !victimRoot.IsValid() || info.Amount <= 0f )
			return default;

		victimRoot = ResolvePlayerPawnRoot( victimRoot );
		if ( attackerRoot.IsValid() )
			attackerRoot = ResolvePlayerPawnRoot( attackerRoot );

		if ( !ThornsCombatFactions.HostCanDamage( attackerRoot, victimRoot, info ) )
			return default;

		if ( ThornsLocalPlayer.IsPlayerPawnRoot( victimRoot ) )
		{
			var playerReceiver = ThornsPlayerDamageReceiver.EnsureOn( victimRoot );
			if ( playerReceiver is not null && playerReceiver.IsValid )
				return playerReceiver.HostApplyDamage( attackerRoot, info with { VictimRoot = victimRoot } );
		}

		var animalReceiver = victimRoot.Components.Get<ThornsAnimalDamageReceiver>( FindMode.EverythingInSelfAndParent );
		if ( animalReceiver is not null && animalReceiver.IsValid )
			return animalReceiver.HostApplyDamage( attackerRoot, info );

		var banditReceiver = victimRoot.Components.Get<ThornsBanditDamageReceiver>( FindMode.EverythingInSelfAndParent );
		if ( banditReceiver is not null && banditReceiver.IsValid )
			return banditReceiver.HostApplyDamage( attackerRoot, info );

		var banditBrain = victimRoot.Components.Get<ThornsBanditBrain>( FindMode.EverythingInSelfAndParent );
		if ( banditBrain is not null && banditBrain.IsValid() )
			return HostApplyToBandit( banditBrain, info.Amount, attackerRoot, info );

		var brain = victimRoot.Components.Get<ThornsAnimalBrain>( FindMode.EverythingInSelfAndParent );
		if ( brain is not null && brain.IsValid() )
			return HostApplyToAnimal( brain, info.Amount, attackerRoot, info.IsHeadshot, info.WeaponId, info.HitPosition, info.HitNormal );

		return default;
	}

	public static DamageResult HostApplyToBandit(
		ThornsBanditBrain brain,
		float amount,
		GameObject attackerRoot,
		in DamageInfo info )
	{
		if ( brain is null || !brain.IsValid() || !brain.GameObject.IsValid() )
			return default;

		if ( !ThornsMultiplayer.IsHostOrOffline || amount <= 0f || brain.IsDead )
			return default;

		var receiver = ThornsBanditDamageReceiver.EnsureOn( brain.GameObject );
		var banditInfo = new DamageInfo
		{
			Amount = amount,
			AttackerRoot = info.AttackerRoot,
			VictimRoot = brain.GameObject,
			VictimKind = VictimKind.Npc,
			AttackerFaction = info.AttackerFaction,
			VictimFaction = ThornsCombatFactions.FactionKind.Bandit,
			IsHeadshot = info.IsHeadshot,
			IsCritical = info.IsCritical,
			DamageTypeId = info.DamageTypeId,
			WeaponId = info.WeaponId,
			HitPosition = info.HitPosition,
			HitNormal = info.HitNormal,
			AttackerAccountKey = info.AttackerAccountKey,
			VictimAccountKey = info.VictimAccountKey
		};

		return receiver?.HostApplyDamage( attackerRoot, banditInfo ) ?? default;
	}

	public static DamageResult HostApplyToAnimal(
		ThornsAnimalBrain brain,
		float amount,
		GameObject attackerRoot,
		bool isHeadshot = false,
		string weaponId = "",
		Vector3 hitPosition = default,
		Vector3 hitNormal = default )
	{
		if ( brain is null || !brain.IsValid() || !brain.GameObject.IsValid() )
			return default;

		if ( !ThornsMultiplayer.IsHostOrOffline || amount <= 0f || brain.IsDead )
			return default;

		var info = new DamageInfo
		{
			Amount = amount,
			AttackerRoot = attackerRoot,
			VictimRoot = brain.GameObject,
			VictimKind = VictimKind.Animal,
			AttackerFaction = ThornsCombatFactions.ResolveFaction( attackerRoot ),
			VictimFaction = brain.IsTamed ? ThornsCombatFactions.FactionKind.TamedAnimal : ThornsCombatFactions.FactionKind.Wildlife,
			IsHeadshot = isHeadshot,
			DamageTypeId = "weapon",
			WeaponId = weaponId ?? "",
			HitPosition = hitPosition,
			HitNormal = hitNormal,
			AttackerAccountKey = ThornsCombatFactions.ResolveAccountKey( attackerRoot )
		};

		return HostApplyToAnimal( brain, in info );
	}

	public static DamageResult HostApplyToAnimal( ThornsAnimalBrain brain, in DamageInfo info )
	{
		if ( brain is null || !brain.IsValid() || !brain.GameObject.IsValid() )
			return default;

		if ( !ThornsMultiplayer.IsHostOrOffline || info.Amount <= 0f || brain.IsDead )
			return default;

		var victimRoot = brain.GameObject;
		var damageInfo = info.VictimRoot.IsValid() && info.VictimRoot == victimRoot
			? info
			: info with { VictimRoot = victimRoot, VictimKind = VictimKind.Animal };

		if ( !ThornsCombatFactions.HostCanDamage( damageInfo.AttackerRoot, victimRoot, damageInfo ) )
			return default;

		if ( ThornsAnimalCombatRules.ShouldIgnoreDamage( brain, damageInfo.AttackerRoot ) )
			return default;

		var finalAmount = Math.Max( 0f, damageInfo.Amount );
		var killed = brain.HostApplyDamageFromPipeline( finalAmount, damageInfo.AttackerRoot, damageInfo );
		return new DamageResult
		{
			Applied = true,
			Killed = killed,
			VictimKind = VictimKind.Animal,
			DamageDealt = finalAmount
		};
	}

	static GameObject ResolvePlayerPawnRoot( GameObject root )
	{
		if ( !root.IsValid() || !ThornsLocalPlayer.IsPlayerPawnRoot( root ) )
			return root;

		return ThornsLocalPlayer.ResolvePawnRoot( root );
	}

	public static DamageInfo BuildPlayerWeaponHit(
		GameObject attackerRoot,
		GameObject victimRoot,
		float amount,
		string weaponId,
		bool isHeadshot,
		Vector3 hitPosition,
		Vector3 hitNormal )
	{
		return BuildAttackerWeaponHit(
			attackerRoot,
			victimRoot,
			amount,
			weaponId,
			VictimKind.Player,
			hitNormal.LengthSquared > 0.01f ? -hitNormal.Normal : Vector3.Forward,
			isHeadshot,
			hitPosition,
			"weapon" );
	}

	public static DamageInfo BuildAttackerWeaponHit(
		GameObject attackerRoot,
		GameObject victimRoot,
		float amount,
		string weaponId,
		VictimKind victimKind,
		Vector3 hitDirection,
		bool isHeadshot = false,
		Vector3 hitPosition = default,
		string damageTypeId = "weapon" )
	{
		if ( hitPosition.LengthSquared < 1f && victimRoot.IsValid() )
			hitPosition = victimRoot.WorldPosition + Vector3.Up * 48f;

		var victimFaction = victimKind switch
		{
			VictimKind.Npc => ThornsCombatFactions.FactionKind.Bandit,
			VictimKind.Player => ThornsCombatFactions.FactionKind.Player,
			VictimKind.Animal => ThornsCombatFactions.ResolveFaction( victimRoot ),
			_ => ThornsCombatFactions.FactionKind.Neutral
		};

		var hitDir = hitDirection.LengthSquared > 0.01f ? hitDirection.Normal : Vector3.Forward;

		return new DamageInfo
		{
			Amount = amount,
			AttackerRoot = attackerRoot,
			VictimRoot = victimRoot,
			VictimKind = victimKind,
			AttackerFaction = ThornsCombatFactions.ResolveFaction( attackerRoot ),
			VictimFaction = victimFaction,
			IsHeadshot = isHeadshot,
			DamageTypeId = damageTypeId,
			WeaponId = weaponId ?? "",
			HitPosition = hitPosition,
			HitNormal = -hitDir,
			AttackerAccountKey = ThornsCombatFactions.ResolveAccountKey( attackerRoot ),
			VictimAccountKey = ThornsCombatFactions.ResolveAccountKey( victimRoot )
		};
	}
}
