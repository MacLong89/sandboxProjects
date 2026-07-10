namespace Terraingen.AI;

using Terraingen.Animals;
using Terraingen.Combat;
using Terraingen.Multiplayer;
using Terraingen.Player;

/// <summary>Hitscan helpers for bandit burst fire.</summary>
public static class ThornsBanditCombatUtil
{
	public static Vector3 SamplePelletDirection( Vector3 baseDir, float spreadHalfAngleDegrees )
	{
		if ( spreadHalfAngleDegrees <= 0.01f )
			return baseDir.Normal;

		var yaw = Game.Random.Float( -spreadHalfAngleDegrees, spreadHalfAngleDegrees );
		var pitch = Game.Random.Float( -spreadHalfAngleDegrees, spreadHalfAngleDegrees );
		return Rotation.From( new Angles( pitch, yaw, 0f ) ) * baseDir.Normal;
	}

	public static bool TryApplyPelletDamage(
		GameObject banditRoot,
		Vector3 eyePos,
		Vector3 dirN,
		float maxRange,
		Terraingen.Combat.ThornsWeaponDefinitions.WeaponDefinition def,
		float damageMul )
	{
		if ( !ThornsCombatHitResolver.TryResolveVictimAlongRay(
			     banditRoot.Scene,
			     eyePos,
			     dirN,
			     maxRange,
			     banditRoot,
			     out var victimRoot,
			     out var victimKind ) )
			return false;

		if ( !victimRoot.IsValid() || victimRoot == banditRoot )
			return false;

		if ( ShouldSkipNpcCombatVictim( banditRoot, victimRoot ) )
			return false;

		return TryApplyDamageToVictim(
			banditRoot,
			victimRoot,
			victimKind,
			eyePos,
			dirN,
			maxRange,
			def,
			damageMul );
	}

	/// <summary>Applies weapon damage directly to the bandit's acquired combat target (avoids ray misses on wildlife).</summary>
	public static bool TryApplyCombatTargetDamage(
		GameObject banditRoot,
		GameObject targetRoot,
		Vector3 hitPosition,
		Terraingen.Combat.ThornsWeaponDefinitions.WeaponDefinition def,
		float damageMul )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || !banditRoot.IsValid() || !targetRoot.IsValid() || targetRoot == banditRoot )
			return false;

		if ( !TryResolveCombatVictimKind( targetRoot, out var victimKind ) )
			return false;

		if ( ShouldSkipNpcCombatVictim( banditRoot, targetRoot ) )
			return false;

		var dirN = (hitPosition - banditRoot.WorldPosition).WithZ( 0.15f ).Normal;
		if ( dirN.LengthSquared < 1e-4f )
			dirN = Vector3.Forward;

		return TryApplyDamageToVictim(
			banditRoot,
			targetRoot,
			victimKind,
			banditRoot.WorldPosition + Vector3.Up * 64f,
			dirN,
			def.MaxRange,
			def,
			damageMul,
			hitPosition );
	}

	/// <summary>True when the bandit has a valid acquired target that should receive direct damage (not pellet raycasts).</summary>
	public static bool IsDirectCombatTarget( GameObject targetRoot )
		=> TryResolveCombatVictimKind( targetRoot, out _ );

	static bool TryApplyDamageToVictim(
		GameObject banditRoot,
		GameObject victimRoot,
		ThornsCombatDamage.VictimKind victimKind,
		Vector3 eyePos,
		Vector3 dirN,
		float maxRange,
		Terraingen.Combat.ThornsWeaponDefinitions.WeaponDefinition def,
		float damageMul,
		Vector3 hitPositionOverride = default )
	{
		ThornsCitizenHitbox.TryClassifyHeadshot( victimRoot, eyePos, dirN, maxRange, out var hitWorld, out var headshot );
		if ( hitPositionOverride != default )
			hitWorld = hitPositionOverride;

		var dmg = def.BaseDamage * damageMul;
		var info = new ThornsCombatDamage.DamageInfo
		{
			Amount = dmg,
			AttackerRoot = banditRoot,
			VictimRoot = victimRoot,
			VictimKind = victimKind,
			AttackerFaction = ThornsCombatFactions.FactionKind.Bandit,
			VictimFaction = ThornsCombatFactions.ResolveFaction( victimRoot ),
			IsHeadshot = headshot,
			DamageTypeId = "bandit_hitscan",
			WeaponId = def.Id,
			HitPosition = hitWorld,
			HitNormal = -dirN.Normal
		};

		var result = ThornsCombatDamage.HostApplyDamage( banditRoot, victimRoot, info );
		if ( ThornsBanditDebug.LogBehaviors )
		{
			if ( result.Applied )
			{
				Log.Info(
					$"[BanditAI] Combat hit {banditRoot.Name} -> {victimRoot.Name} " +
					$"kind={victimKind} dmg={result.DamageDealt:F1} killed={result.Killed}" );
			}
			else
			{
				Log.Info(
					$"[BanditAI] Combat damage rejected {banditRoot.Name} -> {victimRoot.Name} " +
					$"kind={victimKind} dmg={dmg:F1} victimFaction={info.VictimFaction}" );
			}
		}

		return result.Applied;
	}

	static bool TryResolveCombatVictimKind( GameObject root, out ThornsCombatDamage.VictimKind victimKind )
	{
		victimKind = ThornsCombatDamage.VictimKind.Unknown;
		if ( !root.IsValid() )
			return false;

		var animal = root.Components.Get<ThornsAnimalBrain>( FindMode.EverythingInSelfAndParent );
		if ( animal.IsValid() && !animal.IsDead )
		{
			victimKind = ThornsCombatDamage.VictimKind.Animal;
			return true;
		}

		var playerHp = root.Components.Get<ThornsPlayerHealth>( FindMode.EverythingInSelfAndParent );
		if ( playerHp is not null && playerHp.IsValid() && playerHp.IsAlive && !playerHp.IsDeadState )
		{
			victimKind = ThornsCombatDamage.VictimKind.Player;
			return true;
		}

		var bandit = root.Components.Get<ThornsBanditBrain>( FindMode.EverythingInSelfAndParent );
		if ( bandit.IsValid() && !bandit.IsDead )
		{
			victimKind = ThornsCombatDamage.VictimKind.Npc;
			return true;
		}

		return false;
	}

	/// <summary>Skip self and same-group allies; allow wildlife, other bandit groups, and players.</summary>
	static bool ShouldSkipNpcCombatVictim( GameObject attackerRoot, GameObject victimRoot )
	{
		if ( !victimRoot.IsValid() || victimRoot == attackerRoot )
			return true;

		var victimBandit = victimRoot.Components.Get<ThornsBanditBrain>( FindMode.EverythingInSelfAndParent );
		if ( !victimBandit.IsValid() || victimBandit.IsDead )
			return false;

		var attackerBandit = attackerRoot.Components.Get<ThornsBanditBrain>( FindMode.EverythingInSelfAndParent );
		if ( !attackerBandit.IsValid() )
			return false;

		if ( attackerBandit == victimBandit )
			return true;

		return attackerBandit.GroupId != 0 && attackerBandit.GroupId == victimBandit.GroupId;
	}
}
