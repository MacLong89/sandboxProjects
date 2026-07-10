namespace Sandbox;

public enum AimboxGrenadeEquipPhase
{
	None,
	Equipping,
	Ready,
	Charging,
	Throwing
}

public enum AimboxGrenadeKind
{
	Explosive,
	Flash,
	Smoke,
	Decoy,
	Incendiary
}

public readonly struct AimboxGrenadeConfig
{
	public AimboxWeaponId WeaponId { get; init; }
	public AimboxGrenadeKind Kind { get; init; }
	public float FuseSeconds { get; init; }
	public float BlastRadius { get; init; }
	public float MaxDamage { get; init; }
	public float DamagePerSecond { get; init; }
	public float EffectDurationSeconds { get; init; }
	public float ThrowSpeed { get; init; }
	public float ThrowUpSpeed { get; init; }
}

public static class AimboxGrenadeCatalog
{
	public const int ChargesPerLife = 1;

	public static bool IsUnlimitedChargesMode =>
		AimboxGame.Instance?.Match.Mode == AimboxGameMode.Range;

	public static bool HasCharges( AimboxPlayerController player, bool isLethal )
	{
		if ( player is null )
			return false;

		if ( IsUnlimitedChargesMode )
			return true;

		return isLethal ? player.LethalGrenadesRemaining > 0 : player.TacticalGrenadesRemaining > 0;
	}

	public static bool IsGrenadeWeapon( AimboxWeaponId id ) => id switch
	{
		AimboxWeaponId.HeGrenade or AimboxWeaponId.FlashGrenade or AimboxWeaponId.SmokeGrenade
			or AimboxWeaponId.DecoyGrenade or AimboxWeaponId.IncendiaryGrenade => true,
		_ => false
	};

	public static AimboxWeaponId ResolveLoadoutGrenade( string grenadeId, AimboxWeaponId fallback )
	{
		if ( string.IsNullOrWhiteSpace( grenadeId ) )
			return fallback;

		var key = grenadeId.Trim().Replace( " ", "", StringComparison.OrdinalIgnoreCase ).ToUpperInvariant();
		return key switch
		{
			"FRAG" or "HE" or "GRENADE" or "HEGRENADE" => AimboxWeaponId.HeGrenade,
			"FLASH" or "FLASHBANG" or "FLASHGRENADE" => AimboxWeaponId.FlashGrenade,
			"SMOKE" or "SMOKEGRENADE" => AimboxWeaponId.SmokeGrenade,
			"DECOY" or "DECOYGRENADE" => AimboxWeaponId.DecoyGrenade,
			"INCENDIARY" or "FIRE" or "MOLOTOV" or "INCENDIARYGRENADE" => AimboxWeaponId.IncendiaryGrenade,
			_ => fallback
		};
	}

	public static AimboxGrenadeConfig GetConfig( AimboxWeaponId id ) => id switch
	{
		AimboxWeaponId.HeGrenade => new()
		{
			WeaponId = id,
			Kind = AimboxGrenadeKind.Explosive,
			FuseSeconds = 2.4f,
			BlastRadius = 220f,
			MaxDamage = 580f,
			ThrowSpeed = 1180f,
			ThrowUpSpeed = 320f
		},
		AimboxWeaponId.FlashGrenade => new()
		{
			WeaponId = id,
			Kind = AimboxGrenadeKind.Flash,
			FuseSeconds = 1.6f,
			BlastRadius = 420f,
			MaxDamage = 0f,
			ThrowSpeed = 1120f,
			ThrowUpSpeed = 280f
		},
		AimboxWeaponId.SmokeGrenade => new()
		{
			WeaponId = id,
			Kind = AimboxGrenadeKind.Smoke,
			FuseSeconds = 1.4f,
			BlastRadius = 240f,
			MaxDamage = 0f,
			EffectDurationSeconds = 11f,
			ThrowSpeed = 1050f,
			ThrowUpSpeed = 260f
		},
		AimboxWeaponId.DecoyGrenade => new()
		{
			WeaponId = id,
			Kind = AimboxGrenadeKind.Decoy,
			FuseSeconds = 1.4f,
			BlastRadius = 0f,
			MaxDamage = 0f,
			ThrowSpeed = 1050f,
			ThrowUpSpeed = 260f
		},
		AimboxWeaponId.IncendiaryGrenade => new()
		{
			WeaponId = id,
			Kind = AimboxGrenadeKind.Incendiary,
			FuseSeconds = 1.8f,
			BlastRadius = 130f,
			MaxDamage = 0f,
			DamagePerSecond = 104f,
			EffectDurationSeconds = 6.5f,
			ThrowSpeed = 1080f,
			ThrowUpSpeed = 270f
		},
		_ => new()
		{
			WeaponId = AimboxWeaponId.HeGrenade,
			Kind = AimboxGrenadeKind.Explosive,
			FuseSeconds = 2.4f,
			BlastRadius = 220f,
			MaxDamage = 580f,
			ThrowSpeed = 1180f,
			ThrowUpSpeed = 320f
		}
	};

	public static string ResolveViewModelPath( AimboxWeaponId id )
	{
		var def = AimboxWeapons.All.GetValueOrDefault( id );
		if ( def is not null && !string.IsNullOrWhiteSpace( def.ViewModelPath ) )
			return def.ViewModelPath;

		return id switch
		{
			AimboxWeaponId.HeGrenade => AimboxWeaponResourceLoad.HeGrenadeFirstPersonViewmodelPath,
			AimboxWeaponId.FlashGrenade => AimboxWeaponResourceLoad.FlashGrenadeFirstPersonViewmodelPath,
			AimboxWeaponId.SmokeGrenade => AimboxWeaponResourceLoad.SmokeGrenadeFirstPersonViewmodelPath,
			AimboxWeaponId.DecoyGrenade => AimboxWeaponResourceLoad.DecoyGrenadeFirstPersonViewmodelPath,
			AimboxWeaponId.IncendiaryGrenade => AimboxWeaponResourceLoad.IncendiaryGrenadeFirstPersonViewmodelPath,
			_ => AimboxWeaponResourceLoad.HeGrenadeFirstPersonViewmodelPath
		};
	}

	public static string ResolveWorldModelPath( AimboxWeaponId id )
	{
		var def = AimboxWeapons.All.GetValueOrDefault( id );
		if ( def is not null && !string.IsNullOrWhiteSpace( def.WorldModelPath ) )
			return def.WorldModelPath;

		return id switch
		{
			AimboxWeaponId.HeGrenade => AimboxWeaponResourceLoad.HeGrenadeWorldModelPath,
			AimboxWeaponId.FlashGrenade => AimboxWeaponResourceLoad.FlashGrenadeWorldModelPath,
			_ => AimboxWeaponResourceLoad.HeGrenadeWorldModelPath
		};
	}
}

public sealed class AimboxGrenadeSystem
{
	public const float ThrowCooldownSeconds = 0.85f;
	public const float FlashDurationSeconds = 2.35f;
	public const float ShortTossDistanceInches = 24f;
	public const float ShortTossFlightSeconds = 0.36f;

	public bool TryBeginEquipLethal( AimboxPlayerController player ) =>
		TryBeginEquip( player, isLethal: true );

	public bool TryBeginEquipTactical( AimboxPlayerController player ) =>
		TryBeginEquip( player, isLethal: false );

	public bool TryThrowEquipped( AimboxPlayerController player )
	{
		if ( player is null || player.IsProxy || player.GrenadeEquipPhase != AimboxGrenadeEquipPhase.Charging )
			return false;

		if ( player.GrenadePresentationWeaponId is not { } grenadeId )
			return false;

		if ( !TryConsumeEquippedGrenade( player ) )
			return false;

		BeginThrowPresentation( player, grenadeId, () => SpawnProjectile( player, grenadeId ) );
		return true;
	}

	public bool TryQuickTossEquipped( AimboxPlayerController player )
	{
		if ( player is null || player.IsProxy )
			return false;

		if ( player.GrenadeEquipPhase is not (AimboxGrenadeEquipPhase.Ready or AimboxGrenadeEquipPhase.Charging) )
			return false;

		if ( player.GrenadePresentationWeaponId is not { } grenadeId )
			return false;

		if ( !TryConsumeEquippedGrenade( player ) )
			return false;

		BeginQuickTossPresentation( player, grenadeId, () => SpawnProjectileShortToss( player, grenadeId ) );
		return true;
	}

	static bool TryConsumeEquippedGrenade( AimboxPlayerController player )
	{
		if ( player.GrenadeEquipIsLethal )
		{
			if ( !AimboxGrenadeCatalog.HasCharges( player, isLethal: true ) )
				return false;

			player.ConsumeLethalGrenade();
			return true;
		}

		if ( !AimboxGrenadeCatalog.HasCharges( player, isLethal: false ) )
			return false;

		player.ConsumeTacticalGrenade();
		return true;
	}

	static void BeginThrowPresentation( AimboxPlayerController player, AimboxWeaponId grenadeId, Action spawnProjectile )
	{
		player.SetGrenadeEquipPhase( AimboxGrenadeEquipPhase.Throwing );
		player.BeginGrenadeThrowCooldown();
		player.ReleaseGrenadeThrowPresentation(
			onRelease: spawnProjectile,
			onComplete: () => player.FinishGrenadeThrow() );
		AimboxGameplaySfx.PlayGrenadeThrow( player, grenadeId );
	}

	static void BeginQuickTossPresentation( AimboxPlayerController player, AimboxWeaponId grenadeId, Action spawnProjectile )
	{
		player.SetGrenadeEquipPhase( AimboxGrenadeEquipPhase.Throwing );
		player.BeginGrenadeThrowCooldown();
		player.ReleaseGrenadeQuickTossPresentation(
			onRelease: spawnProjectile,
			onComplete: () => player.FinishGrenadeThrow() );
		AimboxGameplaySfx.PlayGrenadeThrow( player, grenadeId );
	}

	bool TryBeginEquip( AimboxPlayerController player, bool isLethal )
	{
		if ( player is null || player.IsProxy || !player.IsAlive || player.Data is null )
			return false;

		if ( player.GrenadeThrowCooldownActive )
			return false;

		if ( AimboxGame.Instance?.Phase != AimboxSessionPhase.Playing || AimboxGame.Instance.IsMatchFrozen )
			return false;

		if ( player.GrenadeEquipPhase is AimboxGrenadeEquipPhase.Equipping or AimboxGrenadeEquipPhase.Throwing )
			return false;

		if ( player.GrenadeEquipPhase != AimboxGrenadeEquipPhase.None )
		{
			if ( player.GrenadeEquipIsLethal == isLethal )
			{
				player.CancelGrenadeEquip();
				return true;
			}

			player.CancelGrenadeEquip();
		}

		var loadout = AimboxGame.Instance.Loadouts.GetActiveLoadout( player.Data );
		var fallback = isLethal ? AimboxWeaponId.HeGrenade : AimboxWeaponId.FlashGrenade;
		var grenadeId = AimboxGrenadeCatalog.ResolveLoadoutGrenade(
			isLethal ? loadout.LethalGrenade : loadout.TacticalGrenade,
			fallback );

		if ( !AimboxGrenadeCatalog.IsGrenadeWeapon( grenadeId ) )
			return false;

		if ( !AimboxUnlockService.IsWeaponUnlocked( player.Data, grenadeId ) )
			return false;

		if ( !AimboxGrenadeCatalog.HasCharges( player, isLethal ) )
			return false;

		player.BeginGrenadeEquip( grenadeId, isLethal );
		return true;
	}

	void SpawnProjectile( AimboxPlayerController thrower, AimboxWeaponId grenadeId )
	{
		var scene = thrower.Scene;
		if ( scene is null || !scene.IsValid() )
			return;

		var config = AimboxGrenadeCatalog.GetConfig( grenadeId );
		var forward = thrower.AimForward.Normal;
		var origin = thrower.EyePosition + forward * 22f + Vector3.Up * -10f;
		var velocity = forward * config.ThrowSpeed + Vector3.Up * config.ThrowUpSpeed + thrower.GetMovementVelocity() * 0.35f;

		var go = new GameObject( true, $"Grenade {grenadeId}" );
		go.WorldPosition = origin;
		go.WorldRotation = Rotation.LookAt( velocity.WithZ( 0 ).Normal );

		var modelPath = AimboxGrenadeCatalog.ResolveWorldModelPath( grenadeId );
		if ( AimboxWeaponResourceLoad.TryLoadWeaponWorldModel( modelPath, $"grenade/{grenadeId}", out var model ) )
		{
			var renderer = go.Components.Create<ModelRenderer>();
			renderer.Model = model;
			renderer.Tint = Color.White;
		}

		var projectile = go.Components.Create<AimboxGrenadeProjectile>();
		projectile.Init( thrower, config, velocity );
	}

	void SpawnProjectileShortToss( AimboxPlayerController thrower, AimboxWeaponId grenadeId )
	{
		var scene = thrower?.Scene;
		if ( scene is null || !scene.IsValid() )
			return;

		var config = AimboxGrenadeCatalog.GetConfig( grenadeId );
		var forward = thrower.AimForward.WithZ( 0 ).Normal;
		if ( forward.LengthSquared < 1e-4f )
			forward = thrower.EyeRotation.Forward.WithZ( 0 ).Normal;

		var origin = thrower.EyePosition + forward * 20f + Vector3.Up * -10f;
		var target = ResolveShortTossTarget( scene, thrower.WorldPosition, forward );
		var velocity = ResolveShortTossVelocity( origin, target, ShortTossFlightSeconds )
		               + thrower.GetMovementVelocity() * 0.15f;

		var go = new GameObject( true, $"Grenade {grenadeId}" );
		go.WorldPosition = origin;
		go.WorldRotation = Rotation.LookAt( velocity.WithZ( 0 ).Normal );

		var modelPath = AimboxGrenadeCatalog.ResolveWorldModelPath( grenadeId );
		if ( AimboxWeaponResourceLoad.TryLoadWeaponWorldModel( modelPath, $"grenade/{grenadeId}", out var model ) )
		{
			var renderer = go.Components.Create<ModelRenderer>();
			renderer.Model = model;
			renderer.Tint = Color.White;
		}

		var projectile = go.Components.Create<AimboxGrenadeProjectile>();
		projectile.Init( thrower, config, velocity );
	}

	static Vector3 ResolveShortTossTarget( Scene scene, Vector3 feetPosition, Vector3 forward )
	{
		var target = feetPosition + forward * ShortTossDistanceInches;
		var tr = scene.Trace.Ray( target + Vector3.Up * 48f, target + Vector3.Down * 320f ).Run();
		return tr.Hit ? tr.EndPosition + tr.Normal * 2f : target;
	}

	static Vector3 ResolveShortTossVelocity( Vector3 origin, Vector3 target, float flightSeconds )
	{
		flightSeconds = MathF.Max( 0.12f, flightSeconds );
		var gravity = AimboxCitizenMovementMotor.Gravity;
		var delta = target - origin;
		return new Vector3(
			delta.x / flightSeconds,
			delta.y / flightSeconds,
			delta.z / flightSeconds + 0.5f * gravity * flightSeconds );
	}
}

[Title( "Aimbox Grenade Projectile" )]
[Category( "Aimbox" )]
public sealed class AimboxGrenadeProjectile : Component
{
	AimboxPlayerController _thrower;
	AimboxGrenadeConfig _config;
	Vector3 _velocity;
	Vector3 _position;
	TimeUntil _fuse;
	bool _detonated;
	float _flightTime;

	public void Init( AimboxPlayerController thrower, AimboxGrenadeConfig config, Vector3 velocity )
	{
		_thrower = thrower;
		_config = config;
		_velocity = velocity;
		_position = GameObject.WorldPosition;
		_fuse = config.FuseSeconds;
		_flightTime = 0f;
	}

	protected override void OnUpdate()
	{
		if ( _detonated )
			return;

		var delta = Time.Delta;
		_flightTime += delta;
		var previous = _position;
		_velocity += Vector3.Down * AimboxCitizenMovementMotor.Gravity * delta;
		_position += _velocity * delta;

		if ( Scene is not null && Scene.IsValid() && _flightTime > 0.04f )
		{
			var trace = Scene.Trace.Ray( previous, _position );
			if ( _thrower?.GameObject.IsValid() == true )
				trace = trace.IgnoreGameObjectHierarchy( _thrower.GameObject );

			var tr = trace.IgnoreGameObjectHierarchy( GameObject ).Run();

			if ( tr.Hit )
			{
				_position = tr.EndPosition + tr.Normal * 2f;
				Detonate();
				return;
			}
		}

		GameObject.WorldPosition = _position;
		if ( _velocity.WithZ( 0 ).Length > 8f )
		{
			var flat = _velocity.WithZ( 0 ).Normal;
			GameObject.WorldRotation = Rotation.LookAt( flat );
		}

		if ( (float)_fuse <= 0f )
			Detonate();
	}

	void Detonate()
	{
		if ( _detonated )
			return;

		_detonated = true;
		GameObject.WorldPosition = _position;
		AimboxGrenadeDetonation.Apply( _thrower, _config, _position );
		GameObject.Destroy();
	}
}

static class AimboxGrenadeDetonation
{
	public static void Apply( AimboxPlayerController thrower, AimboxGrenadeConfig config, Vector3 origin )
	{
		var scene = thrower?.Scene;
		AimboxGrenadeVfx.PlayDetonation( scene, in config, origin );

		switch ( config.Kind )
		{
			case AimboxGrenadeKind.Explosive:
				AimboxGrenadeDamage.ApplyAreaDamage(
					thrower,
					config.WeaponId,
					origin,
					config.BlastRadius,
					config.MaxDamage,
					useQuadraticFalloff: true );
				break;
			case AimboxGrenadeKind.Incendiary:
				SpawnIncendiaryZone( thrower, config, origin );
				break;
			case AimboxGrenadeKind.Flash:
				ApplyFlash( thrower, config, origin );
				break;
			case AimboxGrenadeKind.Smoke:
				AimboxCombatNoiseBus.EmitGunfire( thrower, AimboxWeapons.Get( AimboxWeaponId.Usp ), 0.35f );
				break;
			case AimboxGrenadeKind.Decoy:
				AimboxCombatNoiseBus.EmitGunfire( thrower, AimboxWeapons.Get( AimboxWeaponId.M4A1 ), 0.75f );
				break;
		}

		AimboxGameplaySfx.PlayGrenadeDetonate( thrower, config.WeaponId );
	}

	static void SpawnIncendiaryZone( AimboxPlayerController thrower, AimboxGrenadeConfig config, Vector3 origin )
	{
		var scene = thrower?.Scene;
		if ( scene is null || !scene.IsValid() )
			return;

		var floorOrigin = SnapToFloor( scene, origin );
		var go = new GameObject( true, "Incendiary Fire" );
		go.WorldPosition = floorOrigin;
		go.Parent = scene;

		var zone = go.Components.Create<AimboxIncendiaryFireZone>();
		zone.Init(
			thrower,
			config.WeaponId,
			floorOrigin,
			config.BlastRadius,
			config.DamagePerSecond,
			config.EffectDurationSeconds );
	}

	static Vector3 SnapToFloor( Scene scene, Vector3 origin )
	{
		var tr = scene.Trace.Ray( origin + Vector3.Up * 48f, origin + Vector3.Down * 320f ).Run();
		return tr.Hit ? tr.EndPosition + tr.Normal * 2f : origin;
	}

	static void ApplyFlash( AimboxPlayerController thrower, AimboxGrenadeConfig config, Vector3 origin )
	{
		var game = AimboxGame.Instance;
		if ( game is null )
			return;

		_ = config;
		foreach ( var actor in game.GetAllCombatActors() )
		{
			if ( actor is not AimboxPlayerController player || !player.IsAlive || player.IsProxy )
				continue;

			if ( thrower is not null && player.IsTeammate( thrower ) )
				continue;

			if ( !HasLineOfSight( player.Scene, player.EyePosition, origin, player.GameObject ) )
				continue;

			if ( !player.IsWorldPointInView( origin ) )
				continue;

			player.ApplyFlashBlind( AimboxGrenadeSystem.FlashDurationSeconds );
		}
	}

	static bool HasLineOfSight( Scene scene, Vector3 from, Vector3 to, GameObject ignore )
	{
		if ( scene is null || !scene.IsValid() )
			return false;

		var tr = scene.Trace.Ray( from, to )
			.IgnoreGameObjectHierarchy( ignore )
			.Run();

		return !tr.Hit || tr.EndPosition.Distance( to ) <= 24f;
	}
}

static class AimboxGrenadeDamage
{
	static bool AllowsSelfDamage( AimboxWeaponId weaponId ) =>
		weaponId is AimboxWeaponId.HeGrenade or AimboxWeaponId.IncendiaryGrenade;

	public static void ApplyAreaDamage(
		AimboxPlayerController thrower,
		AimboxWeaponId weaponId,
		Vector3 origin,
		float radius,
		float maxDamage,
		bool useQuadraticFalloff,
		bool horizontalOnly = false )
	{
		if ( radius <= 0f || maxDamage <= 0.5f )
			return;

		var game = AimboxGame.Instance;
		if ( game is null )
			return;

		var allowSelfDamage = AllowsSelfDamage( weaponId );

		foreach ( var actor in game.GetAllCombatActors() )
		{
			if ( actor is null || !actor.IsAlive )
				continue;

			if ( thrower is not null && actor != thrower && actor.IsTeammate( thrower ) )
				continue;

			var distance = GetDistance( actor.WorldPosition, origin, horizontalOnly );
			if ( distance > radius )
				continue;

			var falloff = 1f - Math.Clamp( distance / radius, 0f, 1f );
			var damage = useQuadraticFalloff ? maxDamage * falloff * falloff : maxDamage * falloff;
			if ( damage <= 0.5f )
				continue;

			game.Damage.ApplyDamage( thrower, actor, weaponId, damage, false, distance, allowSelfDamage );
			if ( actor != thrower )
				NotifyAttackerHit( thrower, damage );
		}

		if ( thrower?.Scene is null )
			return;

		foreach ( var dummy in thrower.Scene.GetAllComponents<AimboxDummyTarget>() )
		{
			if ( dummy is null || !dummy.IsAlive )
				continue;

			var distance = GetDistance( dummy.WorldPosition, origin, horizontalOnly );
			if ( distance > radius )
				continue;

			var falloff = 1f - Math.Clamp( distance / radius, 0f, 1f );
			var damage = useQuadraticFalloff ? maxDamage * falloff * falloff : maxDamage * falloff;
			if ( damage <= 0.5f )
				continue;

			dummy.TakeDamage( thrower, weaponId, damage, false );
			NotifyAttackerHit( thrower, damage );
		}
	}

	static float GetDistance( Vector3 target, Vector3 origin, bool horizontalOnly )
	{
		if ( !horizontalOnly )
			return target.Distance( origin );

		return target.WithZ( 0f ).Distance( origin.WithZ( 0f ) );
	}

	static void NotifyAttackerHit( AimboxPlayerController thrower, float damage )
	{
		if ( thrower is null || thrower.IsProxy || damage <= 0.5f )
			return;

		var scaled = damage * thrower.CombatDamageMultiplier;
		thrower.RegisterCombatHitFeedback( scaled, headshot: false );
	}
}
