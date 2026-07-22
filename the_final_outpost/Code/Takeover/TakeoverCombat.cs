namespace FinalOutpost;

/// <summary>Aimbox-style Scene.Trace hitscan against capsule hitboxes on zombies/hostiles.</summary>
public static class TakeoverCombat
{
	public static void Fire(
		TakeoverPawn pawn,
		TakeoverWeaponRuntime weapon,
		Vector3 aimForward,
		bool ads,
		bool moving,
		bool crouched )
	{
		if ( pawn is null || weapon is null ) return;
		var def = weapon.Definition;
		var combat = GameCore.Instance?.Combat;
		if ( combat is null ) return;
		if ( pawn.Scene is null || !pawn.Scene.IsValid() ) return;

		var origin = pawn.EyePosition;
		var forward = ApplyRecoilKick( aimForward, def, ads, moving, crouched, out var kickPitch, out var kickYaw );
		pawn.ApplyViewKick( kickPitch, kickYaw );

		var pellets = Math.Clamp( def.Pellets, 1, 12 );
		var spread = def.SpreadDegrees;
		if ( ads ) spread *= def.AdsSpreadMultiplier;
		if ( moving ) spread *= 1.2f;
		if ( crouched ) spread *= 0.85f;
		var pelletSpread = def.PelletSpreadDegrees > 0.01f ? def.PelletSpreadDegrees : spread;

		TakeoverSfx.PlayFire( pawn, def );

		var anyHit = false;
		var anyHeadshot = false;

		for ( var i = 0; i < pellets; i++ )
		{
			var dir = ApplySpread( forward, pellets > 1 ? pelletSpread : spread );
			var end = origin + dir * def.Range;
			var hitSomething = false;
			var hitPos = end;
			var headshot = false;

			var tr = pawn.Scene.Trace.Ray( origin, end )
				.IgnoreGameObjectHierarchy( pawn.GameObject )
				.Run();

			if ( tr.Hit )
			{
				hitPos = tr.HitPosition;
				end = hitPos;

				var zombie = ZombieHitTarget.FindZombie( tr.GameObject );
				if ( zombie is not null )
				{
					var scale = zombie.TypeDef?.Scale ?? 1f;
					headshot = OutpostHitboxes.IsHeadshot( hitPos, zombie.Position, scale );
					var dmg = def.Damage * (headshot ? def.HeadshotMultiplier : 1f);
					combat.ApplyTakeoverHit( zombie, dmg );
					hitSomething = true;
				}
				else
				{
					var hostile = HostileHitTarget.FindUnit( tr.GameObject );
					if ( hostile is not null )
					{
						headshot = OutpostHitboxes.IsHeadshot( hitPos, hostile.WorldPos );
						var dmg = def.Damage * (headshot ? def.HeadshotMultiplier : 1f);
						var killed = HostileForceSystem.Instance?.DamageUnit( hostile, dmg ) == true;
						if ( killed )
						{
							var core = GameCore.Instance;
							if ( core is not null )
							{
								core.Wallet.Earn( (GameConstants.ScrapPerKillBase * 0.65) * core.ScrapMultiplier + core.SalvageKillBonus );
								core.Save.TotalKills++;
							}
						}

						hitSomething = true;
					}
				}
			}

			// Fallback geometric probe if collider miss (e.g. old zombies before restart).
			if ( !hitSomething )
			{
				if ( TraceZombieFallback( origin, dir, def.Range, out var zombie, out hitPos, out headshot ) )
				{
					end = hitPos;
					var dmg = def.Damage * (headshot ? def.HeadshotMultiplier : 1f);
					combat.ApplyTakeoverHit( zombie, dmg );
					hitSomething = true;
				}
				else if ( TraceHostileFallback( origin, dir, def.Range, out var hostile, out hitPos, out headshot ) )
				{
					end = hitPos;
					var dmg = def.Damage * (headshot ? def.HeadshotMultiplier : 1f);
					HostileForceSystem.Instance?.DamageUnit( hostile, dmg );
					hitSomething = true;
				}
			}

			if ( hitSomething )
			{
				anyHit = true;
				if ( headshot ) anyHeadshot = true;
			}

			var muzzle = TakeoverMuzzleResolve.Resolve(
				pawn,
				dir,
				pawn.ViewModel?.Renderer,
				pawn.ViewModel?.Root,
				pawn.Camera );
			if ( (end - muzzle).Length < 1f )
				end = muzzle + dir * MathF.Max( 80f, def.Range * 0.25f );

			TakeoverCombatTracerFx.Spawn( pawn.Scene, muzzle, end, def.TracerColor );
		}

		if ( anyHit )
			pawn.RegisterHitFeedback( anyHeadshot );

		pawn.PulseAttackAnim();
	}

	static Vector3 ApplyRecoilKick(
		Vector3 aimForward,
		TakeoverWeaponDef def,
		bool ads,
		bool moving,
		bool crouched,
		out float kickPitch,
		out float kickYaw )
	{
		var patternScale = 0.24f * 0.85f * (ads ? 0.72f : 1f) * (moving ? 1.08f : 1f) * (crouched ? 0.88f : 1f);
		var firePitch = patternScale * (0.9f + Game.Random.Float( 0f, 0.4f ));
		var fireYaw = patternScale * Game.Random.Float( -0.35f, 0.35f );
		var visualMul = 0.25f * (ads ? 0.5f : 1f) * (crouched ? 0.5f : 1f);
		kickPitch = firePitch * visualMul;
		kickYaw = fireYaw * visualMul;

		var rot = Rotation.LookAt( aimForward.Normal );
		rot *= Rotation.From( new Angles( -firePitch, fireYaw, 0f ) );
		return rot.Forward;
	}

	static Vector3 ApplySpread( Vector3 forward, float degrees )
	{
		if ( degrees <= 0.01f ) return forward.Normal;
		var yaw = Game.Random.Float( -degrees, degrees );
		var pitch = Game.Random.Float( -degrees, degrees );
		return (Rotation.LookAt( forward.Normal ) * Rotation.From( new Angles( pitch, yaw, 0f ) )).Forward;
	}

	static bool TraceZombieFallback(
		Vector3 origin,
		Vector3 dir,
		float range,
		out ZombieInstance hitZombie,
		out Vector3 hitPos,
		out bool headshot )
	{
		hitZombie = null;
		hitPos = origin + dir * range;
		headshot = false;
		var combat = GameCore.Instance?.Combat;
		if ( combat is null ) return false;

		var bestT = range;
		var radius = OutpostHitboxes.CitizenRadius + 6f;
		foreach ( var z in combat.Zombies )
		{
			if ( z is null || z.Dead ) continue;
			var scale = z.TypeDef?.Scale ?? 1f;
			var half = OutpostHitboxes.CitizenHeadTopZ * scale * 0.5f;
			var center = z.Position + Vector3.Up * half;
			if ( !RayHitsCapsule( origin, dir, range, center, radius * scale, half, out var t, out var pos ) )
				continue;
			if ( t >= bestT ) continue;
			bestT = t;
			hitZombie = z;
			hitPos = pos;
			headshot = OutpostHitboxes.IsHeadshot( pos, z.Position, scale );
		}

		return hitZombie is not null;
	}

	static bool TraceHostileFallback(
		Vector3 origin,
		Vector3 dir,
		float range,
		out HostileUnit hitUnit,
		out Vector3 hitPos,
		out bool headshot )
	{
		hitUnit = null;
		hitPos = origin + dir * range;
		headshot = false;
		var hostiles = HostileForceSystem.Instance;
		if ( hostiles is null ) return false;

		var bestT = range;
		var radius = OutpostHitboxes.CitizenRadius + 4f;
		foreach ( var u in hostiles.Units )
		{
			if ( u is null || !u.IsAlive ) continue;
			var half = OutpostHitboxes.CitizenHeadTopZ * 0.5f;
			var center = u.WorldPos + Vector3.Up * half;
			if ( !RayHitsCapsule( origin, dir, range, center, radius, half, out var t, out var pos ) )
				continue;
			if ( t >= bestT ) continue;
			bestT = t;
			hitUnit = u;
			hitPos = pos;
			headshot = OutpostHitboxes.IsHeadshot( pos, u.WorldPos );
		}

		return hitUnit is not null;
	}

	static bool RayHitsCapsule(
		Vector3 origin,
		Vector3 dir,
		float range,
		Vector3 center,
		float radius,
		float halfHeight,
		out float t,
		out Vector3 hitPos )
	{
		t = 0f;
		hitPos = default;
		var a = center - Vector3.Up * halfHeight;
		var b = center + Vector3.Up * halfHeight;
		var ab = b - a;
		var abLenSq = MathF.Max( 0.001f, ab.LengthSquared );
		var bestDist = float.MaxValue;
		var bestT = -1f;
		var bestPos = Vector3.Zero;

		for ( var i = 0; i <= 24; i++ )
		{
			var tt = (i / 24f) * range;
			var p = origin + dir * tt;
			var ap = p - a;
			var u = Math.Clamp( Vector3.Dot( ap, ab ) / abLenSq, 0f, 1f );
			var closest = a + ab * u;
			var dist = (p - closest).Length;
			if ( dist >= bestDist ) continue;
			bestDist = dist;
			bestT = tt;
			bestPos = closest + (p - closest).Normal * MathF.Min( dist, radius );
		}

		if ( bestDist > radius || bestT < 0f )
			return false;

		t = bestT;
		hitPos = bestPos;
		return true;
	}
}
