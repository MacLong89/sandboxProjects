using System;
using System.Collections.Generic;

namespace Sandbox;

/// <summary>
/// Host-only hitscan direction solve: deterministic spray pattern + small cone bloom (THORNS_EVERYTHING_DOCUMENT §3 host validates aim intent;
/// §7 spread/recoil fields — server-side pattern is an explicit tightening over “presentation-only” hipfire notes).
/// Client never chooses final hit direction; owner receives a separate visual kick only (see <see cref="YaPawnMovement.OwnerApplyMomentaryWeaponRecoil"/>).
/// </summary>
public static class YaWeaponRecoilSolve
{
	/// <summary>Shared test pattern (degrees: X = yaw right, Y = vertical kick applied as negative pitch = crosshair up).</summary>
	public static readonly Vector2[] DefaultTestPattern =
	{
		new( 0f, 1f ),
		new( 0.2f, 1.2f ),
		new( -0.3f, 1.3f ),
		new( 0.4f, 1.5f ),
		new( -0.5f, 1.6f )
	};

	static readonly Dictionary<string, Vector2[]> _patternByWeaponId = new( StringComparer.OrdinalIgnoreCase )
	{
		["m4"] = DefaultTestPattern,
		["mp5"] = DefaultTestPattern,
		["rifle"] = DefaultTestPattern,
		["dev_placeholder"] = DefaultTestPattern,
		["sniper"] = new Vector2[]
		{
			new( 0f, 1.4f ),
			new( 0.1f, 1.05f ),
			new( -0.12f, 1.15f ),
			new( 0.15f, 1.2f )
		},
		["shotgun"] = new Vector2[]
		{
			new( 0f, 1.35f ),
			new( -0.4f, 1.05f ),
			new( 0.45f, 1.25f ),
			new( -0.2f, 1.05f ),
			new( 0.2f, 1.08f ),
		}
	};

	public static IReadOnlyList<Vector2> GetPatternSteps( YaWeaponDefinitions.WeaponDefinition def )
	{
		if ( def is null )
			return DefaultTestPattern;

		if ( _patternByWeaponId.TryGetValue( def.Id, out var p ) )
			return p;

		return DefaultTestPattern;
	}

	/// <summary>
	/// If <paramref name="lastShotTimeHost"/> is 0 → first shot session. Otherwise gap &gt;
	/// <see cref="YaWeaponDefinitions.WeaponDefinition.RecoilResetDelaySeconds"/> resets pattern/spray ordinal (tap / burst cadence — not instantaneous).
	/// </summary>
	public static Vector3 SolveAuthoritativeFireDirection(
		Vector3 validatedAimForward,
		Rotation eyeRotationWorld,
		YaWeaponDefinitions.WeaponDefinition def,
		ref double lastShotTimeHost,
		ref int patternIndexHost,
		ref int sprayOrdinalHost,
		double nowHost,
		bool adsHeld,
		bool moving,
		bool crouched,
		out float bloomHalfAngleDegUsed,
		out float clientKickPitchDeg,
		out float clientKickYawDeg,
		out int patternRowUsed )
	{
		bloomHalfAngleDegUsed = 0f;
		clientKickPitchDeg = 0f;
		clientKickYawDeg = 0f;
		patternRowUsed = 0;

		if ( def is null || YaWeaponDefinitions.IsMeleeWeapon( def ) )
			return validatedAimForward.Normal;

		var fwd = validatedAimForward.Normal;
		if ( fwd.Length < 0.95f )
			return validatedAimForward.Normal;

		if ( lastShotTimeHost > 1e-6 && nowHost - lastShotTimeHost > def.RecoilResetDelaySeconds )
		{
			Log.Info(
				$"[Thorns Recoil] reset (gap {((nowHost - lastShotTimeHost) * 1000f):F0}ms > {def.RecoilResetDelaySeconds * 1000f:F0}ms) weapon={def.Id}" );
			patternIndexHost = 0;
			sprayOrdinalHost = 0;
		}

		var pattern = GetPatternSteps( def );
		if ( pattern.Count == 0 )
		{
			lastShotTimeHost = nowHost;
			return fwd;
		}

		var useIdx = def.RecoilPatternClampEnd
			? Math.Clamp( patternIndexHost, 0, pattern.Count - 1 )
			: patternIndexHost % pattern.Count;

		patternRowUsed = useIdx;
		var step = pattern[useIdx];

		float recoilMul = 1f;
		recoilMul *= moving ? def.MovingRecoilMul : 1f;
		recoilMul *= crouched ? def.CrouchRecoilMul : 1f;
		recoilMul *= adsHeld ? def.AdsRecoilMul : 1f;

		float bloomMul = 1f;
		bloomMul *= moving ? def.MovingBloomMul : 1f;
		bloomMul *= crouched ? def.CrouchBloomMul : 1f;
		bloomMul *= adsHeld ? def.AdsBloomMul : 1f;

		var yawKickDeg = step.x * def.RecoilPatternScaleDegrees * recoilMul;
		var pitchKickDeg = step.y * def.RecoilPatternScaleDegrees * recoilMul;

		var baseAngles = Rotation.LookAt( fwd ).Angles();
		baseAngles.yaw += yawKickDeg;
		baseAngles.pitch -= pitchKickDeg;
		var dirAfterPattern = Rotation.From( baseAngles ).Forward.Normal;

		var bloomRequested =
			(def.BloomHalfAngleDegreesBase + sprayOrdinalHost * def.BloomHalfAngleDegreesPerSprayShot) * bloomMul;

		bloomHalfAngleDegUsed = AddMinimalBloomAroundForward( dirAfterPattern, bloomRequested, out var finalDir );

		if ( def.RecoilPatternClampEnd )
			patternIndexHost = Math.Min( patternIndexHost + 1, pattern.Count - 1 );
		else
			patternIndexHost = (patternIndexHost + 1) % Math.Max( 1, pattern.Count );

		sprayOrdinalHost++;
		lastShotTimeHost = nowHost;

		clientKickPitchDeg = pitchKickDeg * def.ClientVisualKickScale;
		clientKickYawDeg = yawKickDeg * def.ClientVisualKickScale;

		Log.Info(
			$"[Thorns Recoil] shot weapon={def.Id} row={patternRowUsed}/{pattern.Count} nextIdx={patternIndexHost} sprayOrd={sprayOrdinalHost} " +
			$"ads={adsHeld} move={moving} crouch={crouched} bloomHalfDeg={bloomHalfAngleDegUsed:F3} " +
			$"authPitchDeg={pitchKickDeg:F4} authYawDeg={yawKickDeg:F4} clientPitchDeg={clientKickPitchDeg:F4} clientYawDeg={clientKickYawDeg:F4} " +
			$"finalDir={finalDir:F3} (pattern primary; bloom secondary)" );

		return finalDir;
	}

	/// <summary>
	/// Small uniform-cone deviation; returns effective half-angle used (clamped). Bloom must stay subordinate to pattern magnitude.
	/// </summary>
	static float AddMinimalBloomAroundForward( Vector3 forward, float halfAngleDeg, out Vector3 outDir )
	{
		forward = forward.Normal;
		if ( halfAngleDeg < 0.0005f )
		{
			outDir = forward;
			return 0f;
		}

		halfAngleDeg = Math.Clamp( halfAngleDeg, 0f, 2.5f );

		var spreadRad = halfAngleDeg * (MathF.PI / 180f);
		var cosMax = MathF.Cos( spreadRad );
		var cosTheta = 1f - Random.Shared.NextSingle() * (1f - cosMax);
		var sinTheta = MathF.Sqrt( MathF.Max( 0f, 1f - cosTheta * cosTheta ) );
		var phi = Random.Shared.NextSingle() * 2f * MathF.PI;

		var bitangent = Vector3.Cross( Vector3.Up, forward );
		if ( bitangent.LengthSquared < 1e-6f )
			bitangent = Vector3.Cross( Vector3.Right, forward );
		bitangent = bitangent.Normal;

		var up = Vector3.Cross( forward, bitangent ).Normal;
		var perturbed = forward * cosTheta + bitangent * (sinTheta * MathF.Cos( phi )) + up * (sinTheta * MathF.Sin( phi ));

		outDir = perturbed.Normal;
		return halfAngleDeg;
	}
}
