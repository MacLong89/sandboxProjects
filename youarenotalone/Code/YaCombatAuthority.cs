using System;

namespace Sandbox;

/// <summary>
/// Server-side helpers for THORNS_EVERYTHING_DOCUMENT §3 origin sanity and validated aim vs camera/head.
/// </summary>
public static class YaCombatAuthority
{
	static bool NameLooksLikeHead( string n )
	{
		if ( string.IsNullOrWhiteSpace( n ) )
			return false;

		return n.Contains( "head", StringComparison.OrdinalIgnoreCase )
		       || n.Contains( "neck", StringComparison.OrdinalIgnoreCase )
		       || n.Contains( "skull", StringComparison.OrdinalIgnoreCase )
		       || n.Contains( "face", StringComparison.OrdinalIgnoreCase );
	}

	public static GameObject FindChild( GameObject root, string name )
	{
		foreach ( var c in root.Children )
		{
			if ( c.Name == name )
				return c;
		}

		return default;
	}

	/// <summary>Eye position + rotation used for traces and aim validation (matches <see cref="YaPawnCamera"/> layout).</summary>
	public static bool TryGetAuthoritativeEye( GameObject pawnRoot, out Vector3 eyeWorldPos, out Rotation eyeWorldRot )
	{
		eyeWorldPos = default;
		eyeWorldRot = Rotation.Identity;

		var viewGo = FindChild( pawnRoot, "View" );
		if ( viewGo.IsValid() )
		{
			eyeWorldPos = viewGo.WorldPosition;
			eyeWorldRot = viewGo.WorldRotation;
			return true;
		}

		var move = pawnRoot.Components.Get<YaPawnMovement>();
		var pitch = move?.LookAngles.pitch ?? 0f;
		var eyeLocal = new Vector3( 0f, 0f, 52f );
		var pitchQ = Rotation.FromAxis( Vector3.Right, pitch );
		eyeWorldPos = pawnRoot.WorldPosition + pawnRoot.WorldRotation * eyeLocal;
		eyeWorldRot = pawnRoot.WorldRotation * pitchQ;
		return true;
	}

	public static bool IsDirectionWithinAimTolerance( Vector3 directionNormalized, Rotation eyeWorldRot, float minDot )
	{
		var f = eyeWorldRot.Forward;
		return Vector3.Dot( directionNormalized, f ) >= minDot;
	}

	/// <summary>
	/// Headshot requires either an explicit head-ish hitbox/child name, or a strict high-Z fallback on traced hits.
	/// Analytic/sphere fallback should not call this (it is body-biased and would over-credit headshots).
	/// </summary>
	public static bool TryHeadshotFromTrace( SceneTraceResult tr, YaPawn hitPawn )
	{
		if ( !tr.Hit || !hitPawn.IsValid() )
			return false;

		if ( tr.GameObject.IsValid() && NameLooksLikeHead( tr.GameObject.Name ) )
			return true;

		var root = hitPawn.GameObject.WorldPosition;
		var hp = tr.HitPosition;
		var ctrl = hitPawn.Components.Get<CharacterController>();
		var height = ctrl.IsValid() ? ctrl.Height : 72f;
		// Stricter than the old 0.55 threshold; near top of capsule only.
		return hp.z >= root.z + height * 0.88f;
	}
}
