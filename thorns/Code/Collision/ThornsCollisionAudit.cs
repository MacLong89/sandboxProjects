using System;

namespace Sandbox;

/// <summary>
/// Opt-in spawn-time and manual checks for missing or suspicious solid colliders vs visible meshes.
/// Enable from the F1 developer panel — off by default (no per-frame cost).
/// </summary>
public static class ThornsCollisionAudit
{
	/// <summary>When true, <see cref="TrySpawnAudit"/> logs a one-line summary for registered spawn sites.</summary>
	public static bool SpawnValidationEnabled { get; set; }

	/// <summary>Log ratio warnings when collider aggregate volume vs renderer AABB volume exceeds this.</summary>
	public const float OversizedVolumeRatioWarn = 18f;

	/// <summary>Log when colliders are tiny vs renderer (missed hits / interaction).</summary>
	public const float UndersizedVolumeRatioWarn = 0.04f;

	public static void TrySpawnAudit( GameObject root, string context, bool forceAudit = false )
	{
		if ( root is null || !root.IsValid() || !Game.IsPlaying )
			return;

		if ( !SpawnValidationEnabled && !forceAudit )
			return;

		var solidBoxes = 0;
		var solidMeshes = 0;
		float collVol = 0f;

		foreach ( var bc in root.Components.GetAll<BoxCollider>( FindMode.EnabledInSelfAndDescendants ) )
		{
			if ( !bc.IsValid() || !bc.Enabled || bc.IsTrigger )
				continue;
			solidBoxes++;
			collVol += MathF.Max( 0f, ApproxWorldBoxVolume( bc ) );
		}

		foreach ( var mc in root.Components.GetAll<ModelCollider>( FindMode.EnabledInSelfAndDescendants ) )
		{
			if ( !mc.IsValid() || !mc.Enabled || mc.IsTrigger )
				continue;
			solidMeshes++;
			collVol += MathF.Max( 0f, ApproxWorldModelColliderVolume( mc ) );
		}

		TryUnionRendererWorldAabb( root, out var visBb, out var hasVis );
		var visVol = hasVis ? MathF.Max( 1f, visBb.Volume ) : 0f;

		var ratio = visVol > 1f && collVol > 1f ? collVol / visVol : 0f;

		Log.Info(
			$"[Thorns][CollisionAudit] {context}  boxes={solidBoxes} meshCols={solidMeshes}  collVol≈{collVol:F0}  visVol≈{visVol:F0}  ratio≈{ratio:F2}" );

		if ( hasVis && solidBoxes + solidMeshes == 0 )
			Log.Warning( $"[Thorns][CollisionAudit] {context}: ModelRenderer present but no solid colliders." );

		if ( hasVis && collVol > 200f && ratio >= OversizedVolumeRatioWarn && !RootHasTag( root, ThornsCollisionTags.ResourceNode ) )
			Log.Warning( $"[Thorns][CollisionAudit] {context}: collider volume ≫ visual AABB (ratio {ratio:F1}). Oversized blocker?" );

		if ( hasVis && collVol > 50f && ratio is > 0f and < UndersizedVolumeRatioWarn )
			Log.Warning( $"[Thorns][CollisionAudit] {context}: collider volume ≪ visual AABB (ratio {ratio:F3}). Missed hits?" );
	}

	/// <summary>One-shot scan from the local developer tools — logs summaries for nearby roots with Thorns / building / resource tags.</summary>
	public static void LogNearby( Scene scene, Vector3 origin, float radius )
	{
		if ( scene is null || !scene.IsValid() )
			return;

		var r2 = radius * radius;
		var n = 0;

		void ConsiderRoot( GameObject go, string label )
		{
			if ( !go.IsValid() || go.Parent is not null )
				return;
			if ( (go.WorldPosition - origin).LengthSquared > r2 )
				return;
			TrySpawnAudit( go, $"nearby:{label}:{go.Name}", forceAudit: true );
			n++;
		}

		foreach ( var ps in scene.GetAllComponents<ThornsPlacedStructure>() )
		{
			if ( n >= 64 )
				break;
			ConsiderRoot( ps.GameObject, "structure" );
		}

		foreach ( var rn in scene.GetAllComponents<ThornsResourceNode>() )
		{
			if ( n >= 64 )
				break;
			ConsiderRoot( rn.GameObject, "resource" );
		}

		foreach ( var lc in scene.GetAllComponents<ThornsLootCrate>() )
		{
			if ( n >= 64 )
				break;
			ConsiderRoot( lc.GameObject, "loot" );
		}

		foreach ( var dc in scene.GetAllComponents<ThornsDeathCrate>() )
		{
			if ( n >= 64 )
				break;
			ConsiderRoot( dc.GameObject, "death_crate" );
		}

		foreach ( var wi in ThornsWildlifeIdentity.ActiveByHost.Values )
		{
			if ( n >= 64 )
				break;
			ConsiderRoot( wi.GameObject, "wildlife" );
		}

		Log.Info( $"[Thorns][CollisionAudit] nearby scan roots={n} (cap 64) r={radius:F0}" );
	}

	static bool RootHasTag( GameObject root, string tag )
	{
		foreach ( var t in root.Tags )
		{
			if ( t == tag )
				return true;
		}

		return false;
	}

	static float ApproxWorldBoxVolume( BoxCollider bc )
	{
		var wt = bc.WorldTransform;
		var ax = wt.Rotation * new Vector3( bc.Scale.x * 0.5f, 0f, 0f );
		var ay = wt.Rotation * new Vector3( 0f, bc.Scale.y * 0.5f, 0f );
		var az = wt.Rotation * new Vector3( 0f, 0f, bc.Scale.z * 0.5f );
		return 8f * MathF.Abs( Vector3.Dot( ax, Vector3.Cross( ay, az ) ) );
	}

	static float ApproxWorldModelColliderVolume( ModelCollider mc )
	{
		if ( !mc.Model.IsValid() )
			return 0f;
		var bb = mc.Model.Bounds;
		var wt = mc.WorldTransform;
		return TransformBBoxWorldVolume( wt, bb );
	}

	static float TransformBBoxWorldVolume( Transform wt, BBox local )
	{
		Span<Vector3> corners = stackalloc Vector3[8];
		var c = local.Center;
		var e = local.Size * 0.5f;
		var i = 0;
		for ( var sx = -1; sx <= 1; sx += 2 )
		for ( var sy = -1; sy <= 1; sy += 2 )
		for ( var sz = -1; sz <= 1; sz += 2 )
			corners[i++] = wt.PointToWorld( c + new Vector3( sx * e.x, sy * e.y, sz * e.z ) );

		var mn = corners[0];
		var mx = corners[0];
		for ( var k = 1; k < 8; k++ )
		{
			mn = Vector3.Min( mn, corners[k] );
			mx = Vector3.Max( mx, corners[k] );
		}

		var s = mx - mn;
		return MathF.Max( 0f, s.x * s.y * s.z );
	}

	static void TryUnionRendererWorldAabb( GameObject root, out BBox union, out bool any )
	{
		any = false;
		union = default;
		foreach ( var mr in root.Components.GetAll<ModelRenderer>( FindMode.EnabledInSelfAndDescendants ) )
		{
			if ( !mr.IsValid() || !mr.Enabled || !mr.Model.IsValid() )
				continue;

			var bb = TransformBBoxToWorld( mr.WorldTransform, mr.Model.Bounds );
			if ( !any )
			{
				union = bb;
				any = true;
			}
			else
			{
				union = new BBox( Vector3.Min( union.Mins, bb.Mins ), Vector3.Max( union.Maxs, bb.Maxs ) );
			}
		}

		foreach ( var sk in root.Components.GetAll<SkinnedModelRenderer>( FindMode.EnabledInSelfAndDescendants ) )
		{
			if ( !sk.IsValid() || !sk.Enabled || !sk.Model.IsValid() )
				continue;

			var bb = TransformBBoxToWorld( sk.WorldTransform, sk.Model.Bounds );
			if ( !any )
			{
				union = bb;
				any = true;
			}
			else
			{
				union = new BBox( Vector3.Min( union.Mins, bb.Mins ), Vector3.Max( union.Maxs, bb.Maxs ) );
			}
		}
	}

	static BBox TransformBBoxToWorld( Transform wt, BBox local )
	{
		Span<Vector3> corners = stackalloc Vector3[8];
		var c = local.Center;
		var e = local.Size * 0.5f;
		var i = 0;
		for ( var sx = -1; sx <= 1; sx += 2 )
		for ( var sy = -1; sy <= 1; sy += 2 )
		for ( var sz = -1; sz <= 1; sz += 2 )
			corners[i++] = wt.PointToWorld( c + new Vector3( sx * e.x, sy * e.y, sz * e.z ) );

		var mn = corners[0];
		var mx = corners[0];
		for ( var k = 1; k < 8; k++ )
		{
			mn = Vector3.Min( mn, corners[k] );
			mx = Vector3.Max( mx, corners[k] );
		}

		return new BBox( mn, mx );
	}
}
