using System;

namespace Sandbox;

/// <summary>
/// Large decorative rocks (<c>models/clutter/rock1–2.vmdl</c>) — host scatter with clearance from harvest nodes, foliage props, proc-building footprints, and other boulders.
/// </summary>
public static class ThornsTerrainBoulderScatter
{
	/// <summary>Shrinks the mesh AABB slightly so the box sits closer to the rock silhouette (mesh AABB still pads corners).</summary>
	const float BoulderColliderVisualScale = 0.88f;

	/// <summary>
	/// Walk collision half-extent (world units) at the boulder root — independent of 260–440× visual scale on the child mesh.
	/// </summary>
	const float BoulderCollisionWorldHalfExtent = 44f;

	public static readonly string[] DefaultRockModelPaths =
	{
		"models/clutter/rock1.vmdl",
		"models/clutter/rock2.vmdl"
	};

	/// <summary>Gathers planar centers for props that must not overlap boulders (call after resource + foliage scatter).</summary>
	public static void CollectPlanarObstacleCenters( Scene scene, List<Vector2> resourceXZ, List<Vector2> foliageXZ )
	{
		resourceXZ.Clear();
		foliageXZ.Clear();

		if ( scene is null || !scene.IsValid() )
			return;

		foreach ( var node in ThornsResourceNode.ActiveById.Values )
		{
			if ( !node.IsValid() )
				continue;

			var p = node.GameObject.WorldPosition;
			resourceXZ.Add( new Vector2( p.x, p.y ) );
		}

		foreach ( var proxy in scene.GetAllComponents<ThornsFoliageCullProxy>() )
		{
			if ( !proxy.IsValid() )
				continue;

			var p = proxy.GameObject.WorldPosition;
			foliageXZ.Add( new Vector2( p.x, p.y ) );
		}
	}

	static bool IsFarFromPoints( Vector2 p, List<Vector2> points, float minDist )
	{
		if ( minDist <= 0f || points.Count == 0 )
			return true;

		var md = minDist * minDist;
		for ( var i = 0; i < points.Count; i++ )
		{
			var d = p - points[i];
			if ( d.LengthSquared < md )
				return false;
		}

		return true;
	}

	/// <param name="rejectChunkLocalXY">When true, <paramref name="lx"/>/<paramref name="ly"/> in chunk plane is rejected (e.g. proc-building footprint).</param>
	/// <returns>Number of boulders spawned.</returns>
	public static int HostScatter(
		GameObject chunkRoot,
		ThornsTerrainNetSpec spec,
		ReadOnlySpan<float> heights,
		int cellsRx,
		int cellsRz,
		float worldW,
		float worldD,
		Random rnd,
		IReadOnlyList<string> rockPaths,
		float insetMinX,
		float insetMaxX,
		float insetMinY,
		float insetMaxY,
		int targetCount,
		float minSeparationBoulders,
		float clearanceResourceNodes,
		float clearanceFoliageProps,
		float uniformScaleMin,
		float uniformScaleMax,
		float maxGroundSlopeDelta,
		int maxAttemptsPerRock,
		Func<float, float, bool> rejectChunkLocalXY )
	{
		if ( Networking.IsActive && !Networking.IsHost )
			return 0;

		if ( chunkRoot is null || !chunkRoot.IsValid() )
			return 0;

		var scene = chunkRoot.Scene;
		if ( scene is null || !scene.IsValid() )
			return 0;

		var spawnQueue = ThornsDeferredHostSpawnQueue.EnsureOn( chunkRoot, 64 );

		var models = new Model[rockPaths.Count];
		var anyModel = false;
		for ( var i = 0; i < rockPaths.Count; i++ )
		{
			var path = rockPaths[i];
			if ( string.IsNullOrWhiteSpace( path ) )
				continue;

			var m = Model.Load( path.Trim() );
			if ( m.IsValid() && !m.IsError )
			{
				models[i] = m;
				anyModel = true;
			}
		}

		if ( !anyModel )
		{
			Log.Warning( "[Thorns] Terrain boulder scatter: no valid rock models loaded — skipped." );
			return 0;
		}

		var resourceXZ = new List<Vector2>( 8192 );
		var foliageXZ = new List<Vector2>( 4096 );
		CollectPlanarObstacleCenters( scene, resourceXZ, foliageXZ );

		var placedXZ = new List<Vector2>( targetCount );
		var cr = Math.Max( 0f, clearanceResourceNodes );
		var cf = Math.Max( 0f, clearanceFoliageProps );
		var crSq = cr * cr;
		var cfSq = cf * cf;
		var sep = Math.Max( 0f, minSeparationBoulders );

		var slopeSpan = Math.Max( 8f, maxGroundSlopeDelta );
		const float slopeSampleR = 140f;

		var spawned = 0;
		var tries = Math.Max( 12, maxAttemptsPerRock );

		for ( var n = 0; n < targetCount; n++ )
		{
			for ( var attempt = 0; attempt < tries; attempt++ )
			{
				var lx = insetMinX + (float)rnd.NextDouble() * (insetMaxX - insetMinX);
				var ly = insetMinY + (float)rnd.NextDouble() * (insetMaxY - insetMinY);

				if ( rejectChunkLocalXY is not null && rejectChunkLocalXY( lx, ly ) )
					continue;

				var hz = ThornsTerrainGeometry.SampleHeightLocalZUp(
					heights,
					cellsRx,
					cellsRz,
					worldW,
					worldD,
					spec.CenterOnWorldOrigin,
					lx,
					ly );
				if ( hz < spec.WaterLevelWorldZ )
					continue;

				var h0 = hz;
				var hNx = ThornsTerrainGeometry.SampleHeightLocalZUp(
					heights, cellsRx, cellsRz, worldW, worldD, spec.CenterOnWorldOrigin, lx + slopeSampleR, ly );
				var hPx = ThornsTerrainGeometry.SampleHeightLocalZUp(
					heights, cellsRx, cellsRz, worldW, worldD, spec.CenterOnWorldOrigin, lx - slopeSampleR, ly );
				var hNy = ThornsTerrainGeometry.SampleHeightLocalZUp(
					heights, cellsRx, cellsRz, worldW, worldD, spec.CenterOnWorldOrigin, lx, ly + slopeSampleR );
				var hPy = ThornsTerrainGeometry.SampleHeightLocalZUp(
					heights, cellsRx, cellsRz, worldW, worldD, spec.CenterOnWorldOrigin, lx, ly - slopeSampleR );
				var minH = MathF.Min( MathF.Min( h0, hNx ), MathF.Min( hPx, MathF.Min( hNy, hPy ) ) );
				var maxH = MathF.Max( MathF.Max( h0, hNx ), MathF.Max( hPx, MathF.Max( hNy, hPy ) ) );
				if ( maxH - minH > slopeSpan )
					continue;

				var flatLocal = new Vector3( lx, ly, hz );
				var approxWorld = chunkRoot.WorldPosition + chunkRoot.WorldRotation * flatLocal;
				var worldPos = approxWorld;
				if ( ThornsTerrainGeometry.TrySnapWorldPositionToTerrainGround(
					     scene,
					     approxWorld,
					     startLiftZ: 4096f,
					     segmentLength: 32768f,
					     out var snapped ) )
					worldPos = snapped;

				var p2 = new Vector2( worldPos.x, worldPos.y );

				if ( !IsFarFromPoints( p2, placedXZ, sep ) )
					continue;

				var blocked = false;
				for ( var ri = 0; ri < resourceXZ.Count && !blocked; ri++ )
				{
					var d = p2 - resourceXZ[ri];
					if ( d.LengthSquared < crSq )
						blocked = true;
				}

				for ( var fi = 0; fi < foliageXZ.Count && !blocked; fi++ )
				{
					var d = p2 - foliageXZ[fi];
					if ( d.LengthSquared < cfSq )
						blocked = true;
				}

				if ( blocked )
					continue;

				var pathIx = rnd.Next( 0, models.Length );
				var model = models[pathIx];
				if ( !model.IsValid() )
				{
					for ( var mi = 0; mi < models.Length; mi++ )
					{
						if ( models[mi].IsValid() )
						{
							model = models[mi];
							break;
						}
					}
				}

				if ( !model.IsValid() )
					continue;

				var sclMin = Math.Min( uniformScaleMin, uniformScaleMax );
				var sclMax = Math.Max( uniformScaleMin, uniformScaleMax );
				var u = sclMin + (float)rnd.NextDouble() * (sclMax - sclMin);
				var scale = new Vector3( u, u, u );

				var yaw = (float)(rnd.NextDouble() * 360.0);
				var rot = Rotation.FromYaw( yaw );

				worldPos = ThornsFoliageScatter.AlignPivotWorldPositionMeshBottomOnGround(
					worldPos,
					model,
					scale,
					rot );
				worldPos -= Vector3.Up * ThornsFoliageScatter.FoliagePostAlignSinkWorldZ * 0.35f;
				worldPos -= Vector3.Up * 10f;

				var capturedPos = worldPos;
				var capturedRot = rot;
				var capturedScale = scale;
				var capturedModel = model;
				var capturedP2 = p2;
				spawnQueue.EnqueueOrRunNow( () =>
				{
					var go = new GameObject( true, "ThornsTerrainBoulder" );
					go.WorldPosition = capturedPos;
					go.WorldRotation = capturedRot;
					go.LocalScale = Vector3.One;
					go.Tags.Add( ThornsCollisionTags.Boulder );
					go.Tags.Add( ThornsCollisionTags.TerrainChunk );

					var visual = new GameObject( true, "Visual" );
					visual.SetParent( go );
					visual.LocalPosition = Vector3.Zero;
					visual.LocalRotation = Rotation.Identity;
					visual.LocalScale = capturedScale;

					var mr = visual.Components.Create<ModelRenderer>();
					mr.Model = capturedModel;
					mr.Tint = Color.White;
					ThornsModelMaterialUvScale.ApplyClutterRockMaterial( mr, visual, capturedModel, capturedModel.Name );

					var maxAxis = MathF.Max(
						capturedModel.Bounds.Size.x,
						MathF.Max( capturedModel.Bounds.Size.y, capturedModel.Bounds.Size.z ) );
					var hullExtent = maxAxis > 1e-4f
						? (BoulderCollisionWorldHalfExtent * 2f / maxAxis) * BoulderColliderVisualScale
						: BoulderColliderVisualScale;
					ThornsAnchoredWorldPhysics.EnsureAnchoredBoxPhysicsMatchVisualMesh( go, capturedModel, hullExtent );

					if ( Networking.IsActive )
					{
						go.NetworkMode = NetworkMode.Object;
						go.NetworkSpawn();
					}
				} );

				placedXZ.Add( capturedP2 );
				spawned++;
				break;
			}
		}

		return spawned;
	}
}
