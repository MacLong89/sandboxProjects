using System;

namespace Sandbox;

/// <summary>Host helpers — keep player/bot capsules out of world geometry (spawn, push, nav).</summary>
public static class YaPawnPlacement
{
	const float WallPadding = 6f;
	const float GroundProbeUp = 12f;
	const float GroundProbeDown = 96f;
	const float MaxSpawnSearchRadius = 192f;

	public static bool TryGetCapsule( GameObject root, out float radius, out float height )
	{
		radius = 20f;
		height = 72f;
		if ( root is null || !root.IsValid() )
			return false;

		var cc = root.Components.Get<CharacterController>( FindMode.EnabledInSelf );
		if ( !cc.IsValid() )
			return false;

		radius = Math.Max( 8f, cc.Radius );
		height = Math.Max( 24f, cc.Height );
		return true;
	}

	public static bool IsFeetPositionClear( Scene scene, GameObject root, Vector3 feetPos, float radius, float height )
	{
		if ( scene is null || !scene.IsValid() )
			return false;

		if ( !HasSupportGround( scene, root, feetPos ) )
			return false;

		ReadOnlySpan<float> probeHeights = stackalloc float[] { radius + 6f, height * 0.5f, height - radius - 6f };
		foreach ( var h in probeHeights )
		{
			var center = feetPos + Vector3.Up * h;
			for ( var i = 0; i < 8; i++ )
			{
				var dir = Rotation.FromYaw( i * 45f ) * Vector3.Forward;
				var reach = radius + WallPadding;
				var tr = scene.Trace.Ray( center, center + dir * reach )
					.UseHitboxes( false )
					.UsePhysicsWorld( true )
					.IgnoreGameObjectHierarchy( root )
					.Run();

				if ( tr.Hit && tr.Distance < radius + 2f )
					return false;
			}
		}

		return true;
	}

	public static Vector3 SanitizeFeetPosition( Scene scene, GameObject root, Vector3 desiredFeet )
	{
		if ( scene is null || !scene.IsValid() || root is null || !root.IsValid() )
			return desiredFeet;

		if ( !TryGetCapsule( root, out var radius, out var height ) )
			return desiredFeet;

		desiredFeet = SnapToGround( scene, root, desiredFeet );

		if ( IsFeetPositionClear( scene, root, desiredFeet, radius, height ) )
			return desiredFeet;

		var best = desiredFeet;
		var bestScore = float.MinValue;
		var found = false;

		ReadOnlySpan<float> ringRadii = stackalloc float[] { 28f, 56f, 84f, 112f, 140f, 168f, MaxSpawnSearchRadius };
		foreach ( var ring in ringRadii )
		{
			for ( var i = 0; i < 12; i++ )
			{
				var dir = Rotation.FromYaw( i * 30f ) * Vector3.Forward;
				var cand = SnapToGround( scene, root, desiredFeet + dir * ring );
				if ( !IsFeetPositionClear( scene, root, cand, radius, height ) )
					continue;

				var score = -ring;
				if ( score > bestScore )
				{
					bestScore = score;
					best = cand;
					found = true;
				}
			}

			if ( found )
				return best;
		}

		if ( TryDepenetrate( scene, root, desiredFeet, radius, height, out var depen ) )
			return depen;

		return desiredFeet;
	}

	public static Transform SanitizeSpawnTransform( Scene scene, GameObject root, Transform desired )
	{
		var feet = SanitizeFeetPosition( scene, root, desired.Position );
		return desired.WithPosition( feet );
	}

	/// <summary>Slide a horizontal move through physics; returns false if no valid position found.</summary>
	public static bool TrySlideHorizontal(
		Scene scene,
		GameObject root,
		Vector3 fromFeet,
		Vector3 toFeet,
		out Vector3 resolvedFeet )
	{
		resolvedFeet = fromFeet;
		if ( scene is null || !scene.IsValid() || root is null || !root.IsValid() )
			return false;

		if ( !TryGetCapsule( root, out var radius, out var height ) )
			return false;

		toFeet = toFeet.WithZ( fromFeet.z );
		var delta = toFeet - fromFeet;
		delta.z = 0f;
		if ( delta.LengthSquared < 0.01f )
		{
			resolvedFeet = fromFeet;
			return true;
		}

		var cc = root.Components.Get<CharacterController>( FindMode.EnabledInSelf );
		if ( cc.IsValid() )
		{
			var trace = cc.TraceDirection( delta );
			if ( trace.Hit )
			{
				var travel = Math.Max( 0f, trace.Distance - WallPadding * 0.5f );
				resolvedFeet = fromFeet + delta.Normal * travel;
			}
			else
				resolvedFeet = toFeet;
		}
		else
		{
			var tr = scene.Trace.Sphere( radius, fromFeet + Vector3.Up * (height * 0.5f), toFeet + Vector3.Up * (height * 0.5f ) )
				.UseHitboxes( false )
				.UsePhysicsWorld( true )
				.IgnoreGameObjectHierarchy( root )
				.Run();

			if ( tr.Hit )
			{
				var travel = Math.Max( 0f, tr.Distance - WallPadding );
				resolvedFeet = fromFeet + delta.Normal * travel;
			}
			else
				resolvedFeet = toFeet;
		}

		resolvedFeet = SnapToGround( scene, root, resolvedFeet );

		if ( !IsFeetPositionClear( scene, root, resolvedFeet, radius, height ) )
		{
			resolvedFeet = fromFeet;
			return false;
		}

		return true;
	}

	public static bool TryDepenetrate(
		Scene scene,
		GameObject root,
		Vector3 feetPos,
		float radius,
		float height,
		out Vector3 resolvedFeet )
	{
		resolvedFeet = feetPos;
		if ( IsFeetPositionClear( scene, root, feetPos, radius, height ) )
			return true;

		var best = feetPos;
		var bestScore = float.MinValue;
		var found = false;

		for ( var ring = 0; ring < 4; ring++ )
		{
			var dist = 12f + ring * 14f;
			for ( var i = 0; i < 8; i++ )
			{
				var dir = Rotation.FromYaw( i * 45f ) * Vector3.Forward;
				var cand = SnapToGround( scene, root, feetPos + dir * dist );
				if ( !IsFeetPositionClear( scene, root, cand, radius, height ) )
					continue;

				var score = -dist;
				if ( score > bestScore )
				{
					bestScore = score;
					best = cand;
					found = true;
				}
			}
		}

		if ( !found )
			return false;

		resolvedFeet = best;
		return true;
	}

	public static void ApplyFeetPosition( GameObject root, Vector3 feetPos )
	{
		if ( root is null || !root.IsValid() )
			return;

		root.WorldPosition = feetPos;
		var cc = root.Components.Get<CharacterController>( FindMode.EnabledInSelf );
		if ( cc.IsValid() )
			cc.Velocity = cc.Velocity.WithZ( 0f );
	}

	static bool HasSupportGround( Scene scene, GameObject root, Vector3 feetPos )
	{
		var start = feetPos + Vector3.Up * GroundProbeUp;
		var end = feetPos + Vector3.Down * GroundProbeDown;
		var tr = scene.Trace.Ray( start, end )
			.UseHitboxes( false )
			.UsePhysicsWorld( true )
			.IgnoreGameObjectHierarchy( root )
			.Run();

		return tr.Hit;
	}

	static Vector3 SnapToGround( Scene scene, GameObject root, Vector3 feetPos )
	{
		var start = feetPos + Vector3.Up * GroundProbeUp;
		var end = feetPos + Vector3.Down * GroundProbeDown;
		var tr = scene.Trace.Ray( start, end )
			.UseHitboxes( false )
			.UsePhysicsWorld( true )
			.IgnoreGameObjectHierarchy( root )
			.Run();

		if ( !tr.Hit )
			return feetPos;

		return tr.HitPosition;
	}
}
