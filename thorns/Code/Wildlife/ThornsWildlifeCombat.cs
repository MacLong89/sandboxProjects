namespace Sandbox;

/// <summary>Host melee resolution → <see cref="ThornsHealth.TakeDamage"/> (wildlife damage channel).</summary>
[Title( "Thorns — Wildlife combat" )]
[Category( "Thorns/Wildlife" )]
[Icon( "swords" )]
public sealed class ThornsWildlifeCombat : Component
{
	public const float TamedAssistFallbackDamage = 6f;

	double _nextMeleeAllowedTime;

	public bool HostTryMeleeAttack( ThornsWildlifeSpeciesDefinition def, GameObject targetRoot )
	{
		if ( !Networking.IsHost || !targetRoot.IsValid() || def.MeleeDamage <= 0.01f )
			return false;

		var now = Time.Now;
		if ( now < _nextMeleeAllowedTime )
			return false;

		var selfRoot = GameObject;
		var selfHp = Components.Get<ThornsHealth>();
		if ( selfHp.IsValid() && ( !selfHp.IsAlive || selfHp.IsDeadState ) )
			return false;

		var victimHp = targetRoot.Components.GetInAncestorsOrSelf<ThornsHealth>();
		if ( !victimHp.IsValid() || !victimHp.IsAlive || victimHp.IsDeadState )
			return false;

		var selfFlat = selfRoot.WorldPosition.WithZ( 0 );
		var tgtFlat = targetRoot.WorldPosition.WithZ( 0 );
		if ( (tgtFlat - selfFlat).Length > def.AttackRange )
			return false;

		if ( !ThornsSharedHostHitscan.MeleeVerticalSeparationAllowsHit(
			     selfRoot,
			     targetRoot,
			     ThornsSharedHostHitscan.MeleeMaxAbsVerticalSeparationFeetDefault ) )
			return false;

		if ( !ThornsSharedHostHitscan.CombatDamageHasClearLineOfSight( selfRoot, targetRoot ) )
			return false;

		var wid = Components.Get<ThornsWildlifeIdentity>();
		if ( wid.IsValid() && wid.HostIsTamed
		     && ThornsWildlifeIdentity.HostIsOwnerOrOwnedAlly( wid, targetRoot ) )
			return false;

		var cooldown = Math.Max( 0.15f, def.AttackCooldownSeconds );
		_nextMeleeAllowedTime = now + cooldown;
		Components.Get<ThornsWildlifeAnimSync>()?.HostNotifyMeleeStrike();

		var dmg = def.MeleeDamage;
		if ( wid.IsValid() && wid.HostIsTamed )
			dmg *= wid.GetEffectiveDamageMultiplier() * ThornsTameBondPerks.PackWarChantDamageMul( wid );

		victimHp.TakeDamage( dmg, new DamageContext
		{
			AttackerRoot = selfRoot,
			Headshot = false,
			Kind = "wildlife_melee"
		} );

		ThornsWildlifeLog.Attack( selfRoot.Name, targetRoot.Name, dmg );
		return true;
	}

	public bool HostTryChargeAttack( ThornsWildlifeSpeciesDefinition def, GameObject targetRoot, float damage )
	{
		if ( !Networking.IsHost || !targetRoot.IsValid() || damage <= 0.01f )
			return false;

		var now = Time.Now;
		if ( now < _nextMeleeAllowedTime )
			return false;

		var selfRoot = GameObject;
		var selfHp = Components.Get<ThornsHealth>();
		if ( selfHp.IsValid() && ( !selfHp.IsAlive || selfHp.IsDeadState ) )
			return false;

		var victimHp = targetRoot.Components.GetInAncestorsOrSelf<ThornsHealth>();
		if ( !victimHp.IsValid() || !victimHp.IsAlive || victimHp.IsDeadState )
			return false;

		var reach = Math.Max( def.AttackRange, 95f );
		var selfFlat = selfRoot.WorldPosition.WithZ( 0 );
		var tgtFlat = targetRoot.WorldPosition.WithZ( 0 );
		if ( (tgtFlat - selfFlat).Length > reach )
			return false;

		if ( !ThornsSharedHostHitscan.MeleeVerticalSeparationAllowsHit(
			     selfRoot,
			     targetRoot,
			     ThornsSharedHostHitscan.MeleeMaxAbsVerticalSeparationFeetDefault ) )
			return false;

		if ( !ThornsSharedHostHitscan.CombatDamageHasClearLineOfSight( selfRoot, targetRoot ) )
			return false;

		var wid = Components.Get<ThornsWildlifeIdentity>();
		if ( wid.IsValid() && wid.HostIsTamed
		     && ThornsWildlifeIdentity.HostIsOwnerOrOwnedAlly( wid, targetRoot ) )
			return false;

		var cooldown = Math.Max( 0.28f, def.AttackCooldownSeconds );
		_nextMeleeAllowedTime = now + cooldown;
		Components.Get<ThornsWildlifeAnimSync>()?.HostNotifyMeleeStrike();

		victimHp.TakeDamage( damage, new DamageContext
		{
			AttackerRoot = selfRoot,
			Headshot = false,
			Kind = "wildlife_charge"
		} );

		ThornsWildlifeLog.Attack( selfRoot.Name, targetRoot.Name, damage );
		return true;
	}

	/// <summary>For herbivore tames — bite/strike assist damage when the species has no predator melee.</summary>
	public bool HostTryTamedAssistBite( ThornsWildlifeSpeciesDefinition def, GameObject targetRoot, float damage )
	{
		if ( !Networking.IsHost || !targetRoot.IsValid() || damage <= 0.01f )
			return false;

		var now = Time.Now;
		if ( now < _nextMeleeAllowedTime )
			return false;

		var selfRoot = GameObject;
		var selfHp = Components.Get<ThornsHealth>();
		if ( selfHp.IsValid() && ( !selfHp.IsAlive || selfHp.IsDeadState ) )
			return false;

		var victimHp = targetRoot.Components.GetInAncestorsOrSelf<ThornsHealth>();
		if ( !victimHp.IsValid() || !victimHp.IsAlive || victimHp.IsDeadState )
			return false;

		var reach = Math.Max( 95f, def.AttackRange );
		var selfFlat = selfRoot.WorldPosition.WithZ( 0 );
		var tgtFlat = targetRoot.WorldPosition.WithZ( 0 );
		if ( (tgtFlat - selfFlat).Length > reach )
			return false;

		if ( !ThornsSharedHostHitscan.MeleeVerticalSeparationAllowsHit(
			     selfRoot,
			     targetRoot,
			     ThornsSharedHostHitscan.MeleeMaxAbsVerticalSeparationFeetDefault ) )
			return false;

		if ( !ThornsSharedHostHitscan.CombatDamageHasClearLineOfSight( selfRoot, targetRoot ) )
			return false;

		var wid = Components.Get<ThornsWildlifeIdentity>();
		if ( wid.IsValid() && wid.HostIsTamed
		     && ThornsWildlifeIdentity.HostIsOwnerOrOwnedAlly( wid, targetRoot ) )
			return false;

		var cooldown = Math.Max( 0.22f, def.AttackCooldownSeconds * 0.65f );
		_nextMeleeAllowedTime = now + cooldown;
		Components.Get<ThornsWildlifeAnimSync>()?.HostNotifyMeleeStrike();

		var dmg = damage;
		if ( wid.IsValid() && wid.HostIsTamed )
			dmg *= wid.GetEffectiveDamageMultiplier() * ThornsTameBondPerks.PackWarChantDamageMul( wid );

		victimHp.TakeDamage( dmg, new DamageContext
		{
			AttackerRoot = selfRoot,
			Headshot = false,
			Kind = "tamed_assist"
		} );

		ThornsWildlifeLog.Attack( selfRoot.Name, targetRoot.Name, dmg );
		return true;
	}
}
