namespace Sandbox;

/// <summary>Deterministic spray pattern + view kick for hitscan weapons (ported from terraingen).</summary>
public static class AimboxWeaponRecoilSolve
{
	const float GlobalGunRecoilKickMul = 0.85f;
	const float GlobalVisualKickMul = 0.25f;
	const float AdsVisualKickMul = 0.5f;
	const float CrouchVisualKickMul = 0.5f;
	const float DefaultPatternScaleDegrees = 0.24f;
	const float DefaultClientVisualKickScale = 24f;
	const float DefaultResetDelaySeconds = 0.26f;
	const float DefaultAdsRecoilMul = 0.72f;
	const float DefaultAdsBloomMul = 0.58f;
	const float DefaultMovingRecoilMul = 1.08f;
	const float DefaultMovingBloomMul = 1.25f;
	const float DefaultCrouchRecoilMul = 0.88f;
	const float DefaultCrouchBloomMul = 0.82f;
	const float DefaultBloomBaseDegrees = 0.08f;
	const float DefaultBloomPerShotDegrees = 0.032f;

	static readonly Vector2[] DefaultPattern =
	[
		new( 0f, 1f ),
		new( 0.2f, 1.2f ),
		new( -0.3f, 1.3f ),
		new( 0.4f, 1.5f ),
		new( -0.5f, 1.6f )
	];

	static readonly Dictionary<AimboxWeaponId, Vector2[]> _patternByWeapon = new()
	{
		[AimboxWeaponId.M4A1] = DefaultPattern,
		[AimboxWeaponId.Mp5] = DefaultPattern,
		[AimboxWeaponId.Usp] = DefaultPattern,
		[AimboxWeaponId.M700] =
		[
			new( 0f, 1.4f ),
			new( 0.1f, 1.05f ),
			new( -0.12f, 1.15f ),
			new( 0.15f, 1.2f )
		],
		[AimboxWeaponId.SpaghelliM4] =
		[
			new( 0f, 1.35f ),
			new( -0.4f, 1.05f ),
			new( 0.45f, 1.25f ),
			new( -0.2f, 1.05f ),
			new( 0.2f, 1.08f )
		]
	};

	public static Vector3 SolveFireDirection(
		Vector3 aimForward,
		AimboxWeaponDefinition def,
		ref double lastShotTime,
		ref int patternIndex,
		ref int sprayOrdinal,
		double now,
		bool adsHeld,
		bool moving,
		bool crouched,
		out float kickPitchDeg,
		out float kickYawDeg )
	{
		kickPitchDeg = 0f;
		kickYawDeg = 0f;

		if ( def is null )
		{
			AimboxRecoilDebug.LogSolveSkip( "null-definition" );
			return aimForward.Normal;
		}

		if ( def.IsMelee )
		{
			AimboxRecoilDebug.LogSolveSkip( "melee" );
			return aimForward.Normal;
		}

		var fwd = aimForward;
		if ( fwd.LengthSquared < 1e-8f )
			fwd = Rotation.Identity.Forward;
		fwd = fwd.Normal;

		if ( fwd.Length < 0.95f )
		{
			AimboxRecoilDebug.LogSolveSkip( $"weak-forward len={fwd.Length:F3}" );
			return aimForward.Normal;
		}

		var profile = GetProfile( def.Id );
		var pattern = GetPattern( def.Id );
		var patternLength = PatternLength( pattern );
		if ( patternLength <= 0 )
		{
			AimboxRecoilDebug.LogSolveSkip( "empty-pattern" );
			lastShotTime = now;
			return fwd;
		}

		if ( lastShotTime > 1e-6 && now - lastShotTime > profile.ResetDelaySeconds )
		{
			patternIndex = 0;
			sprayOrdinal = 0;
		}

		var useIdx = profile.ClampPatternEnd
			? Math.Clamp( patternIndex, 0, patternLength - 1 )
			: patternIndex % patternLength;

		var step = pattern[useIdx];

		var recoilMul = 1f;
		recoilMul *= moving ? profile.MovingRecoilMul : 1f;
		recoilMul *= crouched ? profile.CrouchRecoilMul : 1f;
		recoilMul *= adsHeld ? profile.AdsRecoilMul : 1f;

		var bloomMul = 1f;
		bloomMul *= moving ? profile.MovingBloomMul : 1f;
		bloomMul *= crouched ? profile.CrouchBloomMul : 1f;
		bloomMul *= adsHeld ? profile.AdsBloomMul : 1f;

		var yawKickDeg = step.x * profile.PatternScaleDegrees * recoilMul * GlobalGunRecoilKickMul;
		var pitchKickDeg = step.y * profile.PatternScaleDegrees * recoilMul * GlobalGunRecoilKickMul;

		var baseAngles = Rotation.LookAt( fwd ).Angles();
		baseAngles.yaw += yawKickDeg;
		baseAngles.pitch -= pitchKickDeg;
		var dirAfterPattern = Rotation.From( baseAngles ).Forward.Normal;

		var bloomRequested = (profile.BloomBaseDegrees + sprayOrdinal * profile.BloomPerShotDegrees) * bloomMul;
		var finalDir = AddBloomAroundForward( dirAfterPattern, bloomRequested );

		if ( profile.ClampPatternEnd )
			patternIndex = Math.Min( patternIndex + 1, patternLength - 1 );
		else
			patternIndex = (patternIndex + 1) % Math.Max( 1, patternLength );

		sprayOrdinal++;
		lastShotTime = now;

		kickPitchDeg = pitchKickDeg * profile.ClientVisualKickScale * GlobalVisualKickMul;
		kickYawDeg = yawKickDeg * profile.ClientVisualKickScale * GlobalVisualKickMul;
		if ( adsHeld )
		{
			kickPitchDeg *= AdsVisualKickMul;
			kickYawDeg *= AdsVisualKickMul;
		}

		if ( crouched )
		{
			kickPitchDeg *= CrouchVisualKickMul;
			kickYawDeg *= CrouchVisualKickMul;
		}

		AimboxRecoilDebug.LogSolve(
			def.Id,
			useIdx,
			step,
			profile.PatternScaleDegrees,
			profile.ClientVisualKickScale,
			kickPitchDeg,
			kickYawDeg );

		return finalDir;
	}

	static int PatternLength( IReadOnlyList<Vector2> pattern ) =>
		pattern is Vector2[] array ? array.Length : pattern.Count;

	static Vector2[] GetPattern( AimboxWeaponId id ) =>
		_patternByWeapon.TryGetValue( id, out var pattern ) ? pattern : DefaultPattern;

	static RecoilProfile GetProfile( AimboxWeaponId id ) =>
		id switch
		{
			AimboxWeaponId.M700 => RecoilProfile.ForM700(),
			AimboxWeaponId.SpaghelliM4 => RecoilProfile.ForShotgun(),
			AimboxWeaponId.Usp => RecoilProfile.ForUsp(),
			AimboxWeaponId.Mp5 => RecoilProfile.ForMp5(),
			_ => RecoilProfile.ForDefault()
		};

	static Vector3 AddBloomAroundForward( Vector3 forward, float halfAngleDeg )
	{
		forward = forward.Normal;
		if ( halfAngleDeg < 0.0005f )
			return forward;

		halfAngleDeg = Math.Clamp( halfAngleDeg, 0f, 2.5f );
		var spreadRad = halfAngleDeg * (MathF.PI / 180f );
		var cosMax = MathF.Cos( spreadRad );
		var cosTheta = 1f - Random.Shared.NextSingle() * (1f - cosMax );
		var sinTheta = MathF.Sqrt( MathF.Max( 0f, 1f - cosTheta * cosTheta ) );
		var phi = Random.Shared.NextSingle() * 2f * MathF.PI;

		var bitangent = Vector3.Cross( Vector3.Up, forward );
		if ( bitangent.LengthSquared < 1e-6f )
			bitangent = Vector3.Cross( Vector3.Right, forward );
		bitangent = bitangent.Normal;

		var up = Vector3.Cross( forward, bitangent ).Normal;
		return (forward * cosTheta + bitangent * (sinTheta * MathF.Cos( phi )) + up * (sinTheta * MathF.Sin( phi ))).Normal;
	}

	sealed class RecoilProfile
	{
		public float PatternScaleDegrees { get; private init; }
		public float ResetDelaySeconds { get; private init; }
		public float AdsRecoilMul { get; private init; }
		public float AdsBloomMul { get; private init; }
		public float MovingRecoilMul { get; private init; }
		public float MovingBloomMul { get; private init; }
		public float CrouchRecoilMul { get; private init; }
		public float CrouchBloomMul { get; private init; }
		public float ClientVisualKickScale { get; private init; }
		public float BloomBaseDegrees { get; private init; }
		public float BloomPerShotDegrees { get; private init; }
		public bool ClampPatternEnd { get; private init; }

		public static RecoilProfile ForDefault() => new()
		{
			PatternScaleDegrees = DefaultPatternScaleDegrees,
			ResetDelaySeconds = DefaultResetDelaySeconds,
			AdsRecoilMul = DefaultAdsRecoilMul,
			AdsBloomMul = DefaultAdsBloomMul,
			MovingRecoilMul = DefaultMovingRecoilMul,
			MovingBloomMul = DefaultMovingBloomMul,
			CrouchRecoilMul = DefaultCrouchRecoilMul,
			CrouchBloomMul = DefaultCrouchBloomMul,
			ClientVisualKickScale = DefaultClientVisualKickScale,
			BloomBaseDegrees = DefaultBloomBaseDegrees,
			BloomPerShotDegrees = DefaultBloomPerShotDegrees,
			ClampPatternEnd = false
		};

		public static RecoilProfile ForMp5() => new()
		{
			PatternScaleDegrees = 0.22f,
			ResetDelaySeconds = DefaultResetDelaySeconds,
			AdsRecoilMul = DefaultAdsRecoilMul,
			AdsBloomMul = DefaultAdsBloomMul,
			MovingRecoilMul = DefaultMovingRecoilMul,
			MovingBloomMul = DefaultMovingBloomMul,
			CrouchRecoilMul = DefaultCrouchRecoilMul,
			CrouchBloomMul = DefaultCrouchBloomMul,
			ClientVisualKickScale = DefaultClientVisualKickScale,
			BloomBaseDegrees = DefaultBloomBaseDegrees,
			BloomPerShotDegrees = 0.038f,
			ClampPatternEnd = false
		};

		public static RecoilProfile ForUsp() => new()
		{
			PatternScaleDegrees = 0.28f,
			ResetDelaySeconds = DefaultResetDelaySeconds,
			AdsRecoilMul = DefaultAdsRecoilMul,
			AdsBloomMul = DefaultAdsBloomMul,
			MovingRecoilMul = DefaultMovingRecoilMul,
			MovingBloomMul = DefaultMovingBloomMul,
			CrouchRecoilMul = DefaultCrouchRecoilMul,
			CrouchBloomMul = DefaultCrouchBloomMul,
			ClientVisualKickScale = 12f,
			BloomBaseDegrees = DefaultBloomBaseDegrees,
			BloomPerShotDegrees = DefaultBloomPerShotDegrees,
			ClampPatternEnd = false
		};

		public static RecoilProfile ForBow() => new()
		{
			PatternScaleDegrees = 0.1f,
			ResetDelaySeconds = DefaultResetDelaySeconds,
			AdsRecoilMul = 0.45f,
			AdsBloomMul = 0.35f,
			MovingRecoilMul = DefaultMovingRecoilMul,
			MovingBloomMul = DefaultMovingBloomMul,
			CrouchRecoilMul = DefaultCrouchRecoilMul,
			CrouchBloomMul = DefaultCrouchBloomMul,
			ClientVisualKickScale = DefaultClientVisualKickScale * 0.5f,
			BloomBaseDegrees = 0.035f,
			BloomPerShotDegrees = DefaultBloomPerShotDegrees,
			ClampPatternEnd = true
		};

		public static RecoilProfile ForM700() => new()
		{
			PatternScaleDegrees = 0.24f,
			ResetDelaySeconds = DefaultResetDelaySeconds,
			AdsRecoilMul = DefaultAdsRecoilMul,
			AdsBloomMul = DefaultAdsBloomMul,
			MovingRecoilMul = DefaultMovingRecoilMul,
			MovingBloomMul = DefaultMovingBloomMul,
			CrouchRecoilMul = DefaultCrouchRecoilMul,
			CrouchBloomMul = DefaultCrouchBloomMul,
			ClientVisualKickScale = DefaultClientVisualKickScale,
			BloomBaseDegrees = 0.065f,
			BloomPerShotDegrees = DefaultBloomPerShotDegrees,
			ClampPatternEnd = true
		};

		public static RecoilProfile ForShotgun() => new()
		{
			PatternScaleDegrees = 0.24f,
			ResetDelaySeconds = DefaultResetDelaySeconds,
			AdsRecoilMul = DefaultAdsRecoilMul,
			AdsBloomMul = DefaultAdsBloomMul,
			MovingRecoilMul = DefaultMovingRecoilMul,
			MovingBloomMul = DefaultMovingBloomMul,
			CrouchRecoilMul = DefaultCrouchRecoilMul,
			CrouchBloomMul = DefaultCrouchBloomMul,
			ClientVisualKickScale = DefaultClientVisualKickScale,
			BloomBaseDegrees = 0.12f,
			BloomPerShotDegrees = 0.04f,
			ClampPatternEnd = false
		};
	}
}
