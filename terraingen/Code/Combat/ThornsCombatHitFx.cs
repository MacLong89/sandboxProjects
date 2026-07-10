namespace Terraingen.Combat;

using Terraingen.AI;
using Terraingen.Animals;

/// <summary>Resolves impact points and spray vectors for combat hit feedback.</summary>
public static class ThornsCombatHitFx
{
	const float PlausibleHitOffsetSq = 500f * 500f;

	public static bool ShouldSpawnBlood( in ThornsCombatDamage.DamageInfo info, in ThornsCombatDamage.DamageResult result )
	{
		if ( !result.Applied || result.DamageDealt <= 0f )
			return false;

		return info.VictimKind is ThornsCombatDamage.VictimKind.Animal
			or ThornsCombatDamage.VictimKind.Npc
			or ThornsCombatDamage.VictimKind.Player;
	}

	public static Vector3 ResolveImpactPoint( in ThornsCombatDamage.DamageInfo info )
	{
		if ( info.VictimRoot.IsValid() && IsPlausibleHitPosition( info.VictimRoot, info.HitPosition ) )
			return info.HitPosition;

		var victim = info.VictimRoot;
		if ( !victim.IsValid() )
			return info.HitPosition;

		var height = ResolveChestHeight( victim );
		var point = victim.WorldPosition + Vector3.Up * height;

		if ( info.AttackerRoot.IsValid() )
		{
			var towardAttacker = (info.AttackerRoot.WorldPosition - point).WithZ( 0f );
			if ( towardAttacker.LengthSquared > 1f )
				point += towardAttacker.Normal * Math.Min( 12f, height * 0.35f );
		}

		return point;
	}

	public static Vector3 ResolveSprayDirection( in ThornsCombatDamage.DamageInfo info )
	{
		if ( info.HitNormal.LengthSquared > 0.2f )
			return info.HitNormal.Normal;

		if ( info.AttackerRoot.IsValid() && info.VictimRoot.IsValid() )
		{
			var away = info.VictimRoot.WorldPosition - info.AttackerRoot.WorldPosition;
			away.z += 0.22f;
			if ( away.LengthSquared > 1f )
				return away.Normal;
		}

		return (Vector3.Up * 0.35f + Vector3.Random.WithZ( 0f ).Normal).Normal;
	}

	public static float ResolveIntensity( in ThornsCombatDamage.DamageResult result )
		=> Math.Clamp( result.DamageDealt / 14f, 0.55f, 2f );

	static bool IsPlausibleHitPosition( GameObject victim, Vector3 hitPosition )
	{
		if ( hitPosition == default )
			return false;

		return victim.WorldPosition.DistanceSquared( hitPosition ) <= PlausibleHitOffsetSq;
	}

	static float ResolveChestHeight( GameObject victim )
	{
		var brain = victim.Components.Get<ThornsAnimalBrain>( FindMode.EverythingInSelfAndParent );
		if ( brain.IsValid() )
			return MathF.Max( 24f, brain.GetBodyRadius() * 0.58f );

		var bandit = victim.Components.Get<ThornsBanditBrain>( FindMode.EverythingInSelfAndParent );
		if ( bandit.IsValid() )
			return 52f;

		return 48f;
	}
}
