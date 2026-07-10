namespace Terraingen.Combat;

using Terraingen.Multiplayer;
using Terraingen.Player;

/// <summary>Authoritative player damage entry point — weapons must not call <see cref="ThornsPlayerHealth"/> directly.</summary>
[Title( "Thorns Player Damage Receiver" )]
[Category( "Thorns/Combat" )]
public sealed class ThornsPlayerDamageReceiver : Component
{
	[Property] public float HeadshotMultiplier { get; set; } = ThornsCitizenHitbox.HeadshotDamageMultiplier;
	[Property] public float CriticalMultiplier { get; set; } = 2f;
	[Property, Range( 0f, 2f )] public float IncomingDamageMultiplier { get; set; } = 0.6f;
	[Property] public float ArmorDamageReduction { get; set; }

	ThornsPlayerHealth _health;

	protected override void OnAwake()
	{
		EnsureHealth();
	}

	void EnsureHealth()
	{
		if ( _health is not null && _health.IsValid() )
			return;

		_health = Components.Get<ThornsPlayerHealth>( FindMode.EverythingInSelfAndParent )
		          ?? Components.Create<ThornsPlayerHealth>();
	}

	public static ThornsPlayerDamageReceiver EnsureOn( GameObject playerRoot )
	{
		if ( !playerRoot.IsValid() )
			return null;

		playerRoot = ThornsLocalPlayer.ResolvePawnRoot( playerRoot );
		if ( !ThornsLocalPlayer.IsPlayerPawnRoot( playerRoot ) )
			return null;

		var receiver = playerRoot.Components.Get<ThornsPlayerDamageReceiver>( FindMode.EverythingInSelf );
		if ( receiver is not null && receiver.IsValid )
		{
			receiver.EnsureHealth();
			return receiver;
		}

		receiver = playerRoot.Components.Create<ThornsPlayerDamageReceiver>();
		receiver?.EnsureHealth();
		return receiver;
	}

	public bool HostCanReceiveDamage( GameObject attackerRoot, in ThornsCombatDamage.DamageInfo info )
	{
		if ( !ThornsLocalPlayer.IsPlayerPawnRoot( GameObject ) )
			return false;

		EnsureHealth();
		if ( !ThornsMultiplayer.IsHostOrOffline || _health is null || !_health.IsValid() || !_health.IsAlive )
			return false;

		var victimRoot = ThornsLocalPlayer.ResolvePawnRoot( GameObject );
		return ThornsCombatFactions.HostCanDamage( attackerRoot, victimRoot, info );
	}

	public ThornsCombatDamage.DamageResult HostApplyDamage( GameObject attackerRoot, in ThornsCombatDamage.DamageInfo info )
	{
		EnsureHealth();
		if ( !HostCanReceiveDamage( attackerRoot, info ) )
			return default;

		var amount = ResolveFinalDamage( info );
		if ( amount <= 0f )
			return default;

		var killed = _health.HostApplyDamageFromPipeline( amount, attackerRoot, info );
		return new ThornsCombatDamage.DamageResult
		{
			Applied = true,
			Killed = killed,
			VictimKind = ThornsCombatDamage.VictimKind.Player,
			DamageDealt = amount
		};
	}

	float ResolveFinalDamage( in ThornsCombatDamage.DamageInfo info )
	{
		var amount = info.Amount;
		if ( info.IsHeadshot )
			amount *= HeadshotMultiplier;
		if ( info.IsCritical )
			amount *= CriticalMultiplier;

		amount *= IncomingDamageMultiplier;

		if ( ArmorDamageReduction > 0f )
			amount *= Math.Clamp( 1f - ArmorDamageReduction, 0.05f, 1f );

		return Math.Max( 0f, amount );
	}
}
