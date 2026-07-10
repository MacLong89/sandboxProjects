namespace Sandbox;

/// <summary>
/// Host bandit shooting — uses <see cref="ThornsWeaponDefinitions"/> tuning + shared hitscan (<see cref="ThornsSharedHostHitscan"/>).
/// Does not replicate RPC fire intents (NPC authority stays on server — THORNS §3 / §13).
/// </summary>
[Title( "Thorns — Bandit Combat" )]
[Category( "Thorns/AI" )]
[Icon( "gps_fixed" )]
public sealed class ThornsBanditCombat : Component
{
	/// <summary>Host hitscan only — player weapons use full <see cref="ThornsWeaponDefinitions.BaseDamage"/>.</summary>
	const float HumanNpcHitscanDamageMul = 0.75f;

	/// <summary>Per-shot hitscan damage chance (gunshot SFX still plays on misses).</summary>
	public const float HumanNpcPlayerHitChanceDefault = 0.30f;

	/// <summary>Shots per volley, spread across <see cref="BurstVolleyDurationSeconds"/>.</summary>
	public const int BurstShotsPerVolley = 3;

	public const float BurstVolleyDurationSeconds = 2f;

	/// <summary>Silence after a full volley before the next volley begins.</summary>
	public const float BurstPauseSeconds = 1f;

	/// <summary>Max hitscan + engagement range — 150m at Thorns building scale (10m = 100u).</summary>
	public const float HumanNpcMaxEngagementRangeWorld = 1500f;

	[Property] public string CombatWeaponDefinitionId { get; set; } = "m4";

	[Property] public float ExtraSpreadHalfAngleDegrees { get; set; } = 1.25f;

	/// <summary>Per-shot hit chance (see <see cref="HumanNpcPlayerHitChanceDefault"/>).</summary>
	[Property, Range( 0f, 1f )] public float HitChance { get; set; } = HumanNpcPlayerHitChanceDefault;

	int _burstShotsFiredInVolley;
	double _burstVolleyStartRealtime;
	double _burstNextShotRealtime;
	double _burstPauseUntilRealtime;
	double _nextNpcGunshotRpcRealtime;

	/// <summary>Host Attack tick — burst fire with optional context-aware accuracy tuning.</summary>
	public bool HostTryShootToward( GameObject targetRoot, ThornsBanditBrainContext ctx = null ) =>
		HostTryShootTowardInternal( targetRoot, ctx );

	bool HostTryShootTowardInternal( GameObject targetRoot, ThornsBanditBrainContext ctx )
	{
		var authoritative = !Networking.IsActive || Networking.IsHost;
		if ( !authoritative || !targetRoot.IsValid() )
		{
			HostResetBurstCadence();
			return false;
		}

		var now = Time.Now;
		if ( now < _burstPauseUntilRealtime )
			return false;

		if ( _burstShotsFiredInVolley > 0 && now < _burstNextShotRealtime )
			return false;

		var banditRoot = GameObject;
		var selfHp = Components.Get<ThornsHealth>();
		if ( selfHp.IsValid() && ( !selfHp.IsAlive || selfHp.IsDeadState ) )
		{
			HostResetBurstCadence();
			return false;
		}

		if ( _burstShotsFiredInVolley <= 0 || _burstShotsFiredInVolley >= BurstShotsPerVolley )
		{
			_burstShotsFiredInVolley = 0;
			_burstVolleyStartRealtime = now;
			_burstNextShotRealtime = now;
		}

		var def = ThornsWeaponDefinitions.Get( CombatWeaponDefinitionId );
		if ( !ThornsCombatAuthority.TryGetAuthoritativeEye( banditRoot, out var eyePos, out _ ) )
			eyePos = banditRoot.WorldPosition + Vector3.Up * 64f;

		var aimPoint = ThornsBanditPerception.ResolvePreferredAimWorldPoint( targetRoot );
		var baseDir = ( aimPoint - eyePos ).Normal;
		if ( baseDir.LengthSquared < 1e-6f )
			return false;

		HostPlayGunshotFx( banditRoot, eyePos );

		var hitChance = Math.Clamp( ResolveEffectiveHitChance( targetRoot, ctx ), 0f, 1f );
		if ( hitChance >= 0.999f || Random.Shared.NextDouble() < hitChance )
		{
			var range = Math.Min( def.MaxRange, HumanNpcMaxEngagementRangeWorld );
			var pelletCount = Math.Max( 1, def.PelletCount );
			for ( var p = 0; p < pelletCount; p++ )
			{
				var pelletDir = pelletCount <= 1
					? ThornsSharedHostHitscan.SamplePelletDirection( baseDir, ExtraSpreadHalfAngleDegrees )
					: ThornsSharedHostHitscan.SamplePelletDirection(
						baseDir,
						def.PelletSpreadHalfAngleDegrees + ExtraSpreadHalfAngleDegrees );

				TryApplyHitscanPellet( banditRoot, def, eyePos, pelletDir, range );
			}
		}

		_burstShotsFiredInVolley++;
		if ( _burstShotsFiredInVolley >= BurstShotsPerVolley )
		{
			_burstPauseUntilRealtime = now + BurstPauseSeconds;
			_burstShotsFiredInVolley = 0;
		}
		else
		{
			var spacing = BurstVolleyDurationSeconds / BurstShotsPerVolley;
			_burstNextShotRealtime = _burstVolleyStartRealtime + spacing * _burstShotsFiredInVolley;
		}

		return true;
	}

	float ResolveEffectiveHitChance( GameObject targetRoot, ThornsBanditBrainContext ctx )
	{
		var chance = HitChance;
		if ( ctx is null || !targetRoot.IsValid() )
			return chance;

		var dist = ( targetRoot.WorldPosition.WithZ( 0 ) - GameObject.WorldPosition.WithZ( 0 ) ).Length;
		if ( dist > 1000f )
			chance *= 0.55f;
		else if ( dist > 500f )
			chance *= 0.78f;
		else if ( dist < 200f )
			chance *= 1.15f;

		var cc = Components.Get<CharacterController>();
		var moving = cc.IsValid() && cc.Velocity.WithZ( 0 ).Length > 40f;
		if ( moving )
			chance *= 0.82f;

		if ( Time.Now - ctx.LastSeenTargetRealtime > 2.5 )
			chance *= 0.88f;
		else if ( Time.Now - ctx.LastSeenTargetRealtime < 0.6 )
			chance *= 1.08f;

		return Math.Clamp( chance, 0.05f, 0.65f );
	}

	void HostResetBurstCadence()
	{
		_burstShotsFiredInVolley = 0;
		_burstVolleyStartRealtime = 0;
		_burstNextShotRealtime = 0;
		_burstPauseUntilRealtime = 0;
	}

	void HostPlayGunshotFx( GameObject banditRoot, Vector3 eyePos )
	{
		if ( !ThornsWeapon.TryGetObserverGunshotSoundResourceForCombatDefinitionId( CombatWeaponDefinitionId, out var firePath )
		     || string.IsNullOrWhiteSpace( firePath ) )
			firePath = ThornsWeapon.M4FireSoundResource;

		var gunLocalOffset = ThornsWorldSpatialSfx.WorldEmitToLocalOffset( banditRoot, eyePos );

		if ( Networking.IsActive )
		{
			var minInterval = 1f / MathF.Max( 4f, ThornsPerformanceBudgets.ObserverNpcGunshotMaxRpcHz );
			if ( Time.Now >= _nextNpcGunshotRpcRealtime )
			{
				_nextNpcGunshotRpcRealtime = Time.Now + minInterval;
				ThornsMusicWorldSignals.HostRegisterGunshot( eyePos );
				RpcBroadcastNpcGunshotWorld( firePath );
			}
		}
		else
			ThornsWorldSpatialSfx.PlayWorldOneShotFollowing(
				banditRoot,
				gunLocalOffset,
				firePath.Trim(),
				ThornsSpatialSfxCategory.NpcGunshot );
	}

	[Rpc.Broadcast]
	void RpcBroadcastNpcGunshotWorld( string resourcePath )
	{
		if ( string.IsNullOrWhiteSpace( resourcePath ) || !GameObject.IsValid() )
			return;

		var eye = GameObject.WorldPosition + Vector3.Up * 64f;
		if ( ThornsCombatAuthority.TryGetAuthoritativeEye( GameObject, out var eyePos, out _ ) )
			eye = eyePos;

		if ( !ThornsWorldSpatialSfx.LocalListenerWithinPlanarRadius(
			     eye,
			     ThornsPerformanceBudgets.ObserverNpcGunshotMaxHearingRadius ) )
			return;

		var localOffset = ThornsWorldSpatialSfx.WorldEmitToLocalOffset( GameObject, eye );
		ThornsWorldSpatialSfx.PlayWorldOneShotFollowing(
			GameObject,
			localOffset,
			resourcePath.Trim(),
			ThornsSpatialSfxCategory.NpcGunshot );
	}

	static bool IsFriendlyBandit( ThornsPawn victimPawn )
	{
		return victimPawn.IsValid()
		       && victimPawn.GameObject.Components.Get<ThornsBanditBrain>( FindMode.EnabledInSelf ).IsValid();
	}

	void TryApplyHitscanPellet(
		GameObject banditRoot,
		ThornsWeaponDefinitions.WeaponDefinition def,
		Vector3 eyePos,
		Vector3 dirN,
		float maxRange )
	{
		if ( !ThornsSharedHostHitscan.TryResolveHitscanDamageTarget(
			     banditRoot,
			     eyePos,
			     dirN,
			     maxRange,
			     0f,
			     out var tr,
			     out var hitGo,
			     out var victimPawn,
			     out var victimHealth,
			     out var usedAnalyticFallback,
			     out var analyticHitWorld ) )
		{
			return;
		}

		if ( !hitGo.IsValid() || !victimHealth.IsValid() )
			return;

		if ( !ThornsSharedHostHitscan.IsResolvedDamageTarget( victimHealth, victimPawn, hitGo ) )
			return;

		if ( victimPawn.IsValid() && IsFriendlyBandit( victimPawn ) )
			return;

		var headshot = ThornsCombatAuthority.TryHeadshotForWeaponHit( usedAnalyticFallback, tr, analyticHitWorld, victimHealth );
		var dmg = def.BaseDamage * (headshot ? def.HeadshotMultiplier : 1f ) * HumanNpcHitscanDamageMul;

		victimHealth.TakeDamage( dmg, new DamageContext
		{
			AttackerRoot = banditRoot,
			Headshot = headshot,
			Kind = "bandit_hitscan"
		} );
	}
}
