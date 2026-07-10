namespace Terraingen.Combat;

using Terraingen.Animals;
using Terraingen.Multiplayer;

/// <summary>Authoritative wildlife/NPC damage entry — hides <see cref="ThornsAnimalBrain"/> internals from weapons.</summary>
[Title( "Thorns Animal Damage Receiver" )]
[Category( "Thorns/Combat" )]
public sealed class ThornsAnimalDamageReceiver : Component
{
	ThornsAnimalBrain _brain;

	protected override void OnAwake()
	{
		_brain = Components.Get<ThornsAnimalBrain>( FindMode.EverythingInSelfAndParent );
	}

	public static ThornsAnimalDamageReceiver EnsureOn( ThornsAnimalBrain brain )
	{
		if ( brain is null || !brain.IsValid() )
			return null;

		var receiver = brain.Components.Get<ThornsAnimalDamageReceiver>( FindMode.EverythingInSelf );
		if ( receiver is not null && receiver.IsValid )
		{
			receiver.EnsureBrainBound();
			return receiver;
		}

		receiver = brain.Components.Create<ThornsAnimalDamageReceiver>();
		receiver?.EnsureBrainBound( brain );
		return receiver;
	}

	void EnsureBrainBound( ThornsAnimalBrain brain = null )
	{
		if ( _brain is not null && _brain.IsValid() )
			return;

		_brain = brain
		         ?? Components.Get<ThornsAnimalBrain>( FindMode.EverythingInSelfAndParent );
	}

	public bool HostCanReceiveDamage( GameObject attackerRoot, in ThornsCombatDamage.DamageInfo info )
	{
		EnsureBrainBound();
		if ( !ThornsMultiplayer.IsHostOrOffline || _brain is null || !_brain.IsValid() || _brain.IsDead )
			return false;

		return ThornsCombatFactions.HostCanDamage( attackerRoot, _brain.GameObject, info );
	}

	public ThornsCombatDamage.DamageResult HostApplyDamage( GameObject attackerRoot, in ThornsCombatDamage.DamageInfo info )
	{
		if ( !HostCanReceiveDamage( attackerRoot, info ) )
			return default;

		var amount = Math.Max( 0f, info.Amount );
		var killed = _brain.HostApplyDamageFromPipeline( amount, attackerRoot, info );
		return new ThornsCombatDamage.DamageResult
		{
			Applied = true,
			Killed = killed,
			VictimKind = ThornsCombatDamage.VictimKind.Animal,
			DamageDealt = amount
		};
	}
}
