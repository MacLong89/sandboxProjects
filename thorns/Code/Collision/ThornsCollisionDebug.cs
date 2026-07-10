using System;

namespace Sandbox;

/// <summary>
/// Local-only wire overlay for solid colliders near the viewer (P key / developer panel).
/// </summary>
public static class ThornsCollisionDebug
{
	/// <summary>Master toggle — press P on the local pawn to flip.</summary>
	public static bool ShowCollisionDebug
	{
		get => ShowNearbySolidColliders;
		set => ShowNearbySolidColliders = value;
	}

	public static bool ShowNearbySolidColliders { get; set; }

	/// <summary>Also draw trigger volumes (magenta, semi-transparent).</summary>
	public static bool ShowTriggerColliders { get; set; } = true;

	public static float NearbyRadius { get; set; } = 3200f;

	public static int LastDrawSolidCount { get; private set; }
	public static int LastDrawTerrainSheetCount { get; private set; }
	public static int LastTerrainGridSegmentCount { get; private set; }
	public static int LastDrawProcBuildingCount { get; private set; }
	public static int LastDrawResourceNodeCount { get; private set; }

	static bool _loggedMissingOverlay;
	static double _nextToggleAllowedTime;

	const float DrawDurationSec = 0.2f;
	const double ToggleDebounceSeconds = 0.12;

	const int MaxShapes = 1400;

	/// <summary>Flip <see cref="ShowCollisionDebug"/> and log draw stats when enabling.</summary>
	public static void ToggleAndLog()
	{
		if ( Time.Now < _nextToggleAllowedTime )
			return;

		_nextToggleAllowedTime = Time.Now + ToggleDebounceSeconds;

		ShowCollisionDebug = !ShowCollisionDebug;
		if ( ShowCollisionDebug )
		{
			LastDrawSolidCount = 0;
			Log.Info(
				"[Thorns] collision_debug (P)=on — cyan=you/proc walls  orange=terrain sheet  green=nodes  gray=boulders  yellow=other" );
		}
		else
		{
			Log.Info( "[Thorns] collision_debug (P)=off" );
		}
	}

	public static void TickDraw( GameObject viewerRoot )
	{
		if ( !ShowNearbySolidColliders || viewerRoot is null || !viewerRoot.IsValid() || !Game.IsPlaying )
			return;

		var scene = ResolveScene( viewerRoot );
		if ( scene is null )
			return;

		var dbg = ResolveDebugOverlay( scene );
		if ( dbg is null )
		{
			if ( !_loggedMissingOverlay )
			{
				_loggedMissingOverlay = true;
				Log.Warning( "[Thorns] collision_debug: DebugOverlaySystem missing on active scene — wireframes cannot draw." );
			}

			return;
		}

		var origin = viewerRoot.WorldPosition;
		var radius = NearbyRadius;
		var drawn = 0;
		var terrainSheets = 0;
		var procBuildings = 0;
		var resourceNodes = 0;

		DrawViewerCharacterController( dbg, viewerRoot );

		terrainSheets = DrawSandboxTerrainSurfaces( dbg, scene, origin, radius, ref drawn );

		foreach ( var col in scene.GetAllComponents<Collider>() )
		{
			if ( drawn >= MaxShapes )
				break;

			if ( !col.IsValid() || !col.Enabled )
				continue;

			if ( col.IsTrigger && !ShowTriggerColliders )
				continue;

			if ( !TryGetColliderWorldBounds( col, out var bb ) )
				continue;

			if ( !BoundsIntersectsSphere( bb, origin, radius ) )
				continue;

			var go = col.GameObject;
			var color = ResolveColliderColor( go, col.IsTrigger );

			if ( col is BoxCollider bc )
				DrawOrientedBoxWire( dbg, bc.WorldTransform, bc.Center, bc.Scale, color );
			else if ( col is ModelCollider mc && mc.Model.IsValid() )
				DrawWorldBoundsWire( dbg, bb, color );
			else if ( col is CapsuleCollider cap )
				DrawCapsuleWire( dbg, cap.WorldTransform, cap.Start, cap.End, cap.Radius, color );
			else if ( col is SphereCollider sph )
				DrawSphereWire( dbg, sph.WorldTransform.PointToWorld( sph.Center ), sph.Radius * sph.WorldTransform.Scale.x, color );
			else
				DrawWorldBoundsWire( dbg, bb, color );

			drawn++;

			if ( go.Components.Get<ThornsProcBuildingPieceFixup>( FindMode.EnabledInSelf ).IsValid() )
				procBuildings++;
			else if ( go.Tags.Has( ThornsCollisionTags.ResourceNode ) )
				resourceNodes++;
		}

		LastDrawProcBuildingCount = procBuildings;
		LastDrawResourceNodeCount = resourceNodes;
		LastDrawSolidCount = drawn;
		LastDrawTerrainSheetCount = terrainSheets;
		DrawScreenSummary( dbg );
	}

	public static string FormatNearbySummary()
	{
		return $"collision(P): {LastDrawSolidCount} wires  terrain={LastDrawTerrainSheetCount}/{LastTerrainGridSegmentCount}  proc={LastDrawProcBuildingCount}  nodes={LastDrawResourceNodeCount}";
	}

	static void DrawScreenSummary( DebugOverlaySystem dbg )
	{
		dbg.ScreenText(
			new Vector2( 14f, 246f ),
			FormatNearbySummary(),
			13f,
			TextFlag.Left,
			Color.Orange,
			DrawDurationSec );
	}

	static Scene ResolveScene( GameObject viewerRoot )
	{
		var scene = viewerRoot.Scene;
		if ( scene is not null && scene.IsValid() )
			return scene;

		scene = Game.ActiveScene;
		return scene is not null && scene.IsValid() ? scene : null;
	}

	static DebugOverlaySystem ResolveDebugOverlay( Scene scene )
	{
		var dbg = scene.GetSystem<DebugOverlaySystem>();
		if ( dbg is not null )
			return dbg;

		var active = Game.ActiveScene;
		if ( active is not null && active.IsValid() && active != scene )
			dbg = active.GetSystem<DebugOverlaySystem>();

		return dbg;
	}

	const bool DrawAsScreenOverlay = false;

	static int DrawSandboxTerrainSurfaces(
		DebugOverlaySystem dbg,
		Scene scene,
		Vector3 origin,
		float radius,
		ref int drawn )
	{
		var count = 0;
		var color = new Color( 1f, 0.5f, 0.05f, 0.95f );
		var r2 = radius * radius;
		LastTerrainGridSegmentCount = 0;

		foreach ( var terrain in scene.GetAllComponents<Terrain>() )
		{
			if ( drawn >= MaxShapes )
				break;

			if ( !terrain.IsValid() || !terrain.GameObject.IsValid() )
				continue;

			var go = terrain.GameObject;
			if ( (go.WorldPosition - origin).LengthSquared > r2 * 4f )
				continue;

			var footprint = terrain.TerrainSize;
			var height = terrain.TerrainHeight;
			if ( footprint <= 1f || height <= 1f )
				continue;

			var segments = DrawTerrainSurfaceGrid( dbg, scene, terrain, origin, radius, color );
			LastTerrainGridSegmentCount += segments;
			if ( segments > 0 )
			{
				drawn++;
				count++;
			}
		}

		if ( count == 0 && drawn < MaxShapes )
		{
			var segments = DrawTerrainSnapGrid( dbg, scene, origin, radius, color );
			LastTerrainGridSegmentCount += segments;
			if ( segments > 0 )
			{
				drawn++;
				count++;
			}
		}

		return count;
	}

	static int DrawTerrainSnapGrid(
		DebugOverlaySystem dbg,
		Scene scene,
		Vector3 viewerWorld,
		float radius,
		Color color )
	{
		var gridRadius = MathF.Min( radius, 2600f );
		var step = Math.Clamp( gridRadius / 12f, 128f, 360f );
		var minX = viewerWorld.x - gridRadius;
		var maxX = viewerWorld.x + gridRadius;
		var minY = viewerWorld.y - gridRadius;
		var maxY = viewerWorld.y + gridRadius;
		var segments = 0;

		bool TrySample( float wx, float wy, out Vector3 world )
		{
			var approx = new Vector3( wx, wy, viewerWorld.z );
			if ( ThornsTerrainGeometry.TrySnapWorldPositionToTerrainGround(
				    scene,
				    approx,
				    startLiftZ: 12000f,
				    segmentLength: 32000f,
				    out world ) )
			{
				world += Vector3.Up * 8f;
				return true;
			}

			world = default;
			return false;
		}

		for ( var y = minY; y <= maxY + 0.01f; y += step )
		{
			Vector3 prev = default;
			var hasPrev = false;
			for ( var x = minX; x <= maxX + 0.01f; x += step )
			{
				if ( TrySample( x, y, out var p ) )
				{
					if ( hasPrev )
					{
						dbg.Line( prev, p, color, DrawDurationSec, default, DrawAsScreenOverlay );
						segments++;
					}

					prev = p;
					hasPrev = true;
				}
				else
					hasPrev = false;
			}
		}

		for ( var x = minX; x <= maxX + 0.01f; x += step )
		{
			Vector3 prev = default;
			var hasPrev = false;
			for ( var y = minY; y <= maxY + 0.01f; y += step )
			{
				if ( TrySample( x, y, out var p ) )
				{
					if ( hasPrev )
					{
						dbg.Line( prev, p, color, DrawDurationSec, default, DrawAsScreenOverlay );
						segments++;
					}

					prev = p;
					hasPrev = true;
				}
				else
					hasPrev = false;
			}
		}

		return segments;
	}

	static int DrawTerrainSurfaceGrid(
		DebugOverlaySystem dbg,
		Scene scene,
		Terrain terrain,
		Vector3 viewerWorld,
		float radius,
		Color color )
	{
		var terrainGo = terrain.GameObject;
		var wt = terrainGo.WorldTransform;
		var localViewer = terrainGo.WorldRotation.Inverse * (viewerWorld - terrainGo.WorldPosition);
		var size = MathF.Max( 1f, terrain.TerrainSize );
		var maxHeight = MathF.Max( 1f, terrain.TerrainHeight );
		var gridRadius = MathF.Min( radius, size * 0.5f );
		var step = Math.Clamp( gridRadius / 14f, 96f, 384f );
		var minX = Math.Clamp( localViewer.x - gridRadius, 0f, size );
		var maxX = Math.Clamp( localViewer.x + gridRadius, 0f, size );
		var minY = Math.Clamp( localViewer.y - gridRadius, 0f, size );
		var maxY = Math.Clamp( localViewer.y + gridRadius, 0f, size );

		if ( maxX <= minX || maxY <= minY )
			return 0;

		var segments = 0;

		bool TrySample( float lx, float ly, out Vector3 world )
		{
			var sampleWorld = wt.PointToWorld( new Vector3( lx, ly, 0f ) );
			if ( ThornsTerraingenTerrainQueries.TrySampleGroundWorld(
				     scene,
				     sampleWorld.x,
				     sampleWorld.y,
				     8f,
				     out world ) )
				return true;

			var start = wt.PointToWorld( new Vector3( lx, ly, maxHeight * 2.5f ) );
			if ( terrain.RayIntersects( new Ray( start, Vector3.Down ), maxHeight * 2.7f, out var localHit ) )
			{
				world = wt.PointToWorld( localHit ) + Vector3.Up * 8f;
				return true;
			}

			world = default;
			return false;
		}

		for ( var y = minY; y <= maxY + 0.01f; y += step )
		{
			Vector3 prev = default;
			var hasPrev = false;
			for ( var x = minX; x <= maxX + 0.01f; x += step )
			{
				if ( TrySample( MathF.Min( x, maxX ), y, out var p ) )
				{
					if ( hasPrev )
					{
						dbg.Line( prev, p, color, DrawDurationSec, default, DrawAsScreenOverlay );
						segments++;
					}
					prev = p;
					hasPrev = true;
				}
				else
					hasPrev = false;
			}
		}

		for ( var x = minX; x <= maxX + 0.01f; x += step )
		{
			Vector3 prev = default;
			var hasPrev = false;
			for ( var y = minY; y <= maxY + 0.01f; y += step )
			{
				if ( TrySample( x, MathF.Min( y, maxY ), out var p ) )
				{
					if ( hasPrev )
					{
						dbg.Line( prev, p, color, DrawDurationSec, default, DrawAsScreenOverlay );
						segments++;
					}
					prev = p;
					hasPrev = true;
				}
				else
					hasPrev = false;
			}
		}

		return segments;
	}

	static void DrawViewerCharacterController( DebugOverlaySystem dbg, GameObject viewerRoot )
	{
		var pc = viewerRoot.Components.Get<PlayerController>();
		var cc = viewerRoot.Components.Get<CharacterController>();
		float radius;
		float height;
		if ( pc.IsValid() )
		{
			radius = pc.BodyRadius;
			height = pc.BodyHeight;
		}
		else if ( cc.IsValid() )
		{
			radius = cc.Radius;
			height = cc.Height;
		}
		else
		{
			return;
		}

		var feet = viewerRoot.WorldPosition;
		var color = new Color( 0.25f, 0.95f, 1f, 0.95f );
		var bottom = feet + Vector3.Up * radius;
		var top = feet + Vector3.Up * MathF.Max( radius, height - radius );
		DrawCapsuleWireWorld( dbg, bottom, top, radius, color );
	}

	static Color ResolveColliderColor( GameObject go, bool isTrigger )
	{
		if ( isTrigger )
			return new Color( 1f, 0.35f, 0.95f, 0.55f );

		if ( go.Components.Get<ThornsProcBuildingPieceFixup>( FindMode.EnabledInSelf ).IsValid() )
			return new Color( 0.35f, 0.95f, 1f, 0.95f );

		if ( go.Tags.Has( ThornsCollisionTags.Structure ) )
			return new Color( 0.35f, 0.95f, 1f, 0.95f );

		if ( go.Tags.Has( ThornsCollisionTags.ResourceNode ) )
			return Color.Green;

		if ( go.Tags.Has( ThornsCollisionTags.WildlifeHull ) )
			return Color.Magenta;

		if ( go.Tags.Has( ThornsCollisionTags.Boulder ) )
			return new Color( 0.65f, 0.65f, 0.72f, 0.95f );

		if ( go.Tags.Has( ThornsCollisionTags.InteriorFurniture ) )
			return new Color( 1f, 0.25f, 0.2f, 0.95f );

		if ( go.Tags.Has( ThornsCollisionTags.TerrainChunk ) )
			return new Color( 1f, 0.55f, 0.15f, 0.85f );

		if ( go.Tags.Has( "player" ) )
			return new Color( 0.25f, 0.95f, 1f, 0.95f );

		return Color.Yellow;
	}

	static bool TryGetColliderWorldBounds( Collider col, out BBox worldBounds )
	{
		worldBounds = default;
		if ( col is null || !col.IsValid() )
			return false;

		if ( col is BoxCollider bc )
		{
			worldBounds = TransformWorldBounds( bc.WorldTransform, new BBox( bc.Center - bc.Scale * 0.5f, bc.Center + bc.Scale * 0.5f ) );
			return worldBounds.Size.LengthSquared > 1e-8f;
		}

		if ( col is ModelCollider mc && mc.Model.IsValid() )
		{
			worldBounds = TransformWorldBounds( mc.WorldTransform, mc.Model.Bounds );
			return worldBounds.Size.LengthSquared > 1e-8f;
		}

		if ( col is CapsuleCollider cap )
		{
			var wt = cap.WorldTransform;
			var a = wt.PointToWorld( cap.Start );
			var b = wt.PointToWorld( cap.End );
			var r = MathF.Max( 0.05f, cap.Radius * MathF.Max( wt.Scale.x, MathF.Max( wt.Scale.y, wt.Scale.z ) ) );
			worldBounds = new BBox(
				Vector3.Min( a, b ) - new Vector3( r, r, r ),
				Vector3.Max( a, b ) + new Vector3( r, r, r ) );
			return true;
		}

		if ( col is SphereCollider sph )
		{
			var c = sph.WorldTransform.PointToWorld( sph.Center );
			var r = MathF.Max( 0.05f, sph.Radius * sph.WorldTransform.Scale.x );
			worldBounds = new BBox( c - new Vector3( r, r, r ), c + new Vector3( r, r, r ) );
			return true;
		}

		try
		{
			var local = col.LocalBounds;
			if ( local.Size.LengthSquared > 1e-8f )
			{
				worldBounds = TransformWorldBounds( col.WorldTransform, local );
				return true;
			}
		}
		catch
		{
			// LocalBounds not implemented on this collider type.
		}

		return false;
	}

	static BBox TransformWorldBounds( Transform wt, BBox local )
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

	static bool BoundsIntersectsSphere( in BBox bb, Vector3 center, float radius )
	{
		var closest = new Vector3(
			Math.Clamp( center.x, bb.Mins.x, bb.Maxs.x ),
			Math.Clamp( center.y, bb.Mins.y, bb.Maxs.y ),
			Math.Clamp( center.z, bb.Mins.z, bb.Maxs.z ) );
		return ( closest - center ).LengthSquared <= radius * radius;
	}

	static void DrawWorldBoundsWire( DebugOverlaySystem dbg, in BBox worldBounds, Color color )
	{
		var mn = worldBounds.Mins;
		var mx = worldBounds.Maxs;
		var p0 = new Vector3( mn.x, mn.y, mn.z );
		var p1 = new Vector3( mx.x, mn.y, mn.z );
		var p2 = new Vector3( mx.x, mx.y, mn.z );
		var p3 = new Vector3( mn.x, mx.y, mn.z );
		var p4 = new Vector3( mn.x, mn.y, mx.z );
		var p5 = new Vector3( mx.x, mn.y, mx.z );
		var p6 = new Vector3( mx.x, mx.y, mx.z );
		var p7 = new Vector3( mn.x, mx.y, mx.z );

		void Edge( Vector3 a, Vector3 b ) => dbg.Line( a, b, color, DrawDurationSec, default, DrawAsScreenOverlay );

		Edge( p0, p1 );
		Edge( p1, p2 );
		Edge( p2, p3 );
		Edge( p3, p0 );
		Edge( p4, p5 );
		Edge( p5, p6 );
		Edge( p6, p7 );
		Edge( p7, p4 );
		Edge( p0, p4 );
		Edge( p1, p5 );
		Edge( p2, p6 );
		Edge( p3, p7 );
	}

	static void DrawOrientedBoxWire( DebugOverlaySystem dbg, Transform wt, Vector3 localCenter, Vector3 localSize, Color color )
	{
		var half = localSize * 0.5f;
		var bb = new BBox( localCenter - half, localCenter + half );
		DrawWorldBoundsWire( dbg, TransformWorldBounds( wt, bb ), color );
	}

	static void DrawCapsuleWire( DebugOverlaySystem dbg, Transform wt, Vector3 localStart, Vector3 localEnd, float radius, Color color )
	{
		var a = wt.PointToWorld( localStart );
		var b = wt.PointToWorld( localEnd );
		var r = MathF.Max( 0.05f, radius * MathF.Max( wt.Scale.x, MathF.Max( wt.Scale.y, wt.Scale.z ) ) );
		DrawCapsuleWireWorld( dbg, a, b, r, color );
	}

	static void DrawCapsuleWireWorld( DebugOverlaySystem dbg, Vector3 a, Vector3 b, float radius, Color color )
	{
		var r = MathF.Max( 0.05f, radius );
		var axis = b - a;
		var len = axis.Length;
		if ( len < 1e-4f )
		{
			DrawSphereWire( dbg, a, r, color );
			return;
		}

		var dir = axis / len;
		var right = Math.Abs( dir.z ) < 0.98f ? Vector3.Cross( Vector3.Up, dir ).Normal : Vector3.Cross( Vector3.Right, dir ).Normal;
		var up = Vector3.Cross( dir, right ).Normal;

		const int ringSeg = 10;
		const int spineSeg = 6;
		for ( var s = 0; s <= spineSeg; s++ )
		{
			var t = s / (float)spineSeg;
			var center = Vector3.Lerp( a, b, t );
			Vector3 prev = default;
			for ( var i = 0; i <= ringSeg; i++ )
			{
				var ang = i / (float)ringSeg * MathF.PI * 2f;
				var offset = right * MathF.Cos( ang ) * r + up * MathF.Sin( ang ) * r;
				var pt = center + offset;
				if ( i > 0 )
					dbg.Line( prev, pt, color, DrawDurationSec, default, DrawAsScreenOverlay );
				prev = pt;
			}
		}

		dbg.Line( a, b, color.WithAlpha( color.a * 0.65f ), DrawDurationSec, default, DrawAsScreenOverlay );
	}

	static void DrawSphereWire( DebugOverlaySystem dbg, Vector3 center, float radius, Color color )
	{
		const int seg = 12;
		var r = MathF.Max( 0.05f, radius );
		for ( var ring = 0; ring < 3; ring++ )
		{
			Vector3 prev = default;
			var axisA = ring == 0 ? Vector3.Right : ring == 1 ? Vector3.Forward : Vector3.Up;
			var axisB = Vector3.Cross( axisA, Vector3.Up ).Normal;
			if ( axisB.Length < 0.5f )
				axisB = Vector3.Cross( axisA, Vector3.Right ).Normal;

			for ( var i = 0; i <= seg; i++ )
			{
				var ang = i / (float)seg * MathF.PI * 2f;
				var pt = center + (axisA * MathF.Cos( ang ) + axisB * MathF.Sin( ang )) * r;
				if ( i > 0 )
					dbg.Line( prev, pt, color, DrawDurationSec, default, DrawAsScreenOverlay );
				prev = pt;
			}
		}
	}
}
