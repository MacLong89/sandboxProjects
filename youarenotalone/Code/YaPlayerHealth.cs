namespace Sandbox;

[Title( "YouAreNotAlone — Health" )]
[Category( "YouAreNotAlone" )]
[Icon( "favorite" )]
[Order( 50 )]
public sealed class YaPlayerHealth : Component
{
	[Property] public float MaxHealth { get; set; } = 100f;
	[Property] public float AloneNotAloneKillHeal { get; set; } = 50f;

	[Sync( SyncFlags.FromHost )] public float CurrentHealth { get; set; } = 100f;
	[Sync( SyncFlags.FromHost )] public bool IsDeadState { get; set; }

	public bool IsAlive => CurrentHealth > 0f;

	protected override void OnStart()
	{
		if ( Networking.IsHost )
		{
			CurrentHealth = MaxHealth;
			IsDeadState = false;
		}
	}

	/// <summary>Host-only damage from <see cref="YaWeapon"/> hitscan.</summary>
	public bool TakeDamage( float amount, DamageContext context )
	{
		if ( !Networking.IsHost )
			return false;
		if ( amount <= 0f || !IsAlive )
			return false;
		if ( !YaRoundGate.MayDealOrTakeDamage() )
			return false;

		var hpBefore = CurrentHealth;
		CurrentHealth = Math.Max( 0f, CurrentHealth - amount );
		var killingBlow = hpBefore > 0f && CurrentHealth <= 0f;
		if ( killingBlow )
		{
			IsDeadState = true;
			YaPlayerStats.HostRecordDeath( GameObject );
			if ( context.AttackerRoot.IsValid() && context.AttackerRoot != GameObject )
			{
				YaPlayerStats.HostRecordKill( context.AttackerRoot );
				YaKillFeed.HostNotifyElimination( context.AttackerRoot, GameObject );
				var attackerRole = context.AttackerRoot.Components.Get<YaPlayerRoleComponent>( FindMode.EnabledInSelf );
				var victimRole = Components.Get<YaPlayerRoleComponent>( FindMode.EnabledInSelf );
				var attackerHealth = context.AttackerRoot.Components.Get<YaPlayerHealth>( FindMode.EnabledInSelf );
				if ( attackerRole is { IsValid: true, Role: YaPlayerRole.Alone }
				     && victimRole is { IsValid: true, Role: YaPlayerRole.NotAlone }
				     && attackerHealth.IsValid() )
				{
					var before = attackerHealth.CurrentHealth;
					attackerHealth.CurrentHealth = Math.Min( attackerHealth.MaxHealth, attackerHealth.CurrentHealth + AloneNotAloneKillHeal );
					var gained = attackerHealth.CurrentHealth - before;
					if ( gained > 0.01f )
						attackerHealth.RpcAloneKillHealNotify( gained );
				}
			}
		}

		var attackerPos = context.AttackerRoot.IsValid() ? context.AttackerRoot.WorldPosition : Vector3.Zero;
		RpcDamagedNotify( CurrentHealth, amount, attackerPos );
		return killingBlow;
	}

	/// <summary>Host: full heal + clear death flag and teleport (round respawn).</summary>
	public void HostRespawnFull( Transform worldTransform )
	{
		if ( !Networking.IsHost )
			return;

		CurrentHealth = MaxHealth;
		IsDeadState = false;

		var scene = GameObject.Scene;
		var spawn = scene is { IsValid: true }
			? YaPawnPlacement.SanitizeSpawnTransform( scene, GameObject, worldTransform )
			: worldTransform;
		GameObject.WorldTransform = spawn;

		var cc = Components.Get<CharacterController>();
		if ( cc.IsValid() )
			cc.Velocity = Vector3.Zero;
	}

	[Rpc.Owner]
	void RpcDamagedNotify( float healthAfter, float lastDamage, Vector3 attackerWorldPos )
	{
		var yawDelta = 0f;
		if ( attackerWorldPos != default && YaCombatAuthority.TryGetAuthoritativeEye( GameObject, out var eyePos, out var eyeRot ) )
		{
			var toAttacker = (attackerWorldPos - eyePos).WithZ( 0f );
			if ( toAttacker.Length > 1f )
			{
				toAttacker = toAttacker.Normal;
				var fwd = eyeRot.Forward.WithZ( 0f ).Normal;
				var right = eyeRot.Right.WithZ( 0f ).Normal;
				yawDelta = MathF.Atan2( Vector3.Dot( toAttacker, right ), Vector3.Dot( toAttacker, fwd ) ) * (180f / MathF.PI);
			}
		}

		var hud = GameObject.Components.GetInDescendantsOrSelf<YaPlayerHud>( true );
		hud?.NotifyDamageTakenLocal( lastDamage, yawDelta );
	}

	[Rpc.Owner]
	void RpcAloneKillHealNotify( float healAmount )
	{
		var hud = GameObject.Components.GetInDescendantsOrSelf<YaPlayerHud>( true );
		hud?.NotifyFloatingMessageLocal( $"+{(int)MathF.Round( healAmount )} HP" );
	}
}

/// <summary>Metadata for <see cref="YaPlayerHealth.TakeDamage"/> (same shape as Thorns).</summary>
public readonly struct DamageContext
{
	public GameObject AttackerRoot { get; init; }
	public bool Headshot { get; init; }
	public string Kind { get; init; }
}
