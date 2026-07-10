namespace Terraingen.Combat;

using Terraingen.AI;
using Terraingen.Multiplayer;

/// <summary>Authoritative bandit damage entry for the shared combat pipeline.</summary>
[Title( "Thorns Bandit Damage Receiver" )]
[Category( "Thorns/Combat" )]
public sealed class ThornsBanditDamageReceiver : Component
{
	ThornsBanditHealth _health;
	ThornsBanditBrain _brain;

	protected override void OnAwake()
	{
		EnsureBound();
	}

	public static ThornsBanditDamageReceiver EnsureOn( GameObject banditRoot )
	{
		if ( !banditRoot.IsValid() )
			return null;

		var receiver = banditRoot.Components.Get<ThornsBanditDamageReceiver>( FindMode.EverythingInSelf );
		if ( receiver is not null && receiver.IsValid )
		{
			receiver.EnsureBound();
			return receiver;
		}

		receiver = banditRoot.Components.Create<ThornsBanditDamageReceiver>();
		receiver?.EnsureBound();
		return receiver;
	}

	void EnsureBound()
	{
		if ( _health is null || !_health.IsValid )
			_health = Components.Get<ThornsBanditHealth>( FindMode.EverythingInSelfAndParent );

		if ( _brain is null || !_brain.IsValid() )
			_brain = Components.Get<ThornsBanditBrain>( FindMode.EverythingInSelfAndParent );
	}

	public bool HostCanReceiveDamage( GameObject attackerRoot, in ThornsCombatDamage.DamageInfo info )
	{
		EnsureBound();
		if ( !ThornsMultiplayer.IsHostOrOffline || _health is null || !_health.IsValid || !_health.IsAlive )
			return false;

		if ( _brain is not null && _brain.IsValid() && _brain.IsDead )
			return false;

		return ThornsCombatFactions.HostCanDamage( attackerRoot, GameObject, info );
	}

	public ThornsCombatDamage.DamageResult HostApplyDamage( GameObject attackerRoot, in ThornsCombatDamage.DamageInfo info )
	{
		if ( !HostCanReceiveDamage( attackerRoot, info ) || _health is null )
			return default;

		var amount = Math.Max( 0f, info.Amount );
		if ( info.IsHeadshot )
			amount *= ThornsCitizenHitbox.HeadshotDamageMultiplier;

		var killed = _health.HostApplyDamage( amount, attackerRoot, info.IsHeadshot, info.WeaponId );
		return new ThornsCombatDamage.DamageResult
		{
			Applied = true,
			Killed = killed,
			VictimKind = ThornsCombatDamage.VictimKind.Npc,
			DamageDealt = amount
		};
	}
}
