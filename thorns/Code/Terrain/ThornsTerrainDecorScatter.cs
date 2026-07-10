#nullable disable

using System.Buffers;
using System.Collections.Generic;

namespace Sandbox;

/// <summary>
/// Deterministic decorative grass/mushroom scatter for every peer — props are parented under the terrain chunk (no <see cref="NetworkMode.Object"/>).
/// Building avoidance uses foundation half extents recovered from replicated <see cref="ThornsTerrainProcBuildingPad"/> (matches host <c>Cell*0.7</c> inflation).
/// </summary>
public static class ThornsTerrainDecorScatter
{
	public const string DecorRootName = "ThornsDecorFoliageRoot";

	/// <summary>Host fills replicated decor tuning before the terrain chunk replica is finalized (<see cref="ThornsTerrainChunk"/> v1 binary + legacy JSON fallback).</summary>
	public static void CopyHostDecorTuning( ThornsTerrainNetSpec spec, ThornsTerrainSystem host )
	{
		if ( !host.IsValid() )
			return;

		spec.DecorGenerateFoliageFluff = false;
		spec.DecorEnableFoliageDistanceCulling = host.EnableFoliageDistanceCulling;
		spec.DecorEdgeInsetFraction = host.ScatterEdgeInsetFraction;
		spec.DecorGrass ??= ThornsTerrainDecorGrassNet.EngineDefaults();
		spec.DecorMushroom ??= ThornsTerrainDecorMushroomNet.EngineDefaults();
		spec.DecorGrass.CopyFrom( host );
		spec.DecorGrass.ScatterGrassFoliage = false;
		spec.DecorMushroom.CopyFrom( host );
		SanitizeDecorSpec( spec );
	}

	public static void EnsureDecorNetDefaults( ThornsTerrainNetSpec spec )
	{
		spec.ProcBuildingTerrainPads ??= new List<ThornsTerrainProcBuildingPad>();
		spec.DecorGrass ??= ThornsTerrainDecorGrassNet.EngineDefaults();
		spec.DecorMushroom ??= ThornsTerrainDecorMushroomNet.EngineDefaults();
		SanitizeDecorSpec( spec );
	}

	/// <summary>
	/// Strips retired terrain grass decor from replicated specs (legacy <c>models/foliage/grass*</c> white placeholders) and disables fluff when terraingen owns foliage.
	/// </summary>
	public static void SanitizeDecorSpec( ThornsTerrainNetSpec spec )
	{
		if ( spec is null )
			return;

		spec.DecorGrass ??= ThornsTerrainDecorGrassNet.EngineDefaults();
		spec.DecorMushroom ??= ThornsTerrainDecorMushroomNet.EngineDefaults();

		if ( spec.UseTerraingenWorld || spec.UseTerraingenFoliage )
			spec.DecorGenerateFoliageFluff = false;

		var prefix = spec.DecorGrass.ScatterGrassModelPathPrefix ?? "";
		if ( ThornsFoliageScatter.IsLegacyTerrainGrassDecorPath( prefix ) )
		{
			spec.DecorGrass.ScatterGrassFoliage = false;
			spec.DecorGrass.ScatterGrassModelPathPrefix = ThornsFoliageScatter.ClutterGrassDecorPrefix;
			spec.DecorGrass.ScatterGrassVariantCount = 1;
		}
		else if ( ThornsFoliageScatter.IsClutterGrassDecorPath( prefix ) )
		{
			spec.DecorGrass.ScatterGrassFoliage = false;
		}

		if ( spec.UseTerraingenWorld || spec.UseTerraingenFoliage )
			spec.DecorGrass.ScatterGrassFoliage = false;
	}

	public static void DestroyDecorUnderChunk( GameObject chunkRoot )
	{
		if ( !chunkRoot.IsValid() )
			return;

		foreach ( var ch in chunkRoot.Children )
		{
			if ( ch.IsValid() && ch.Name == DecorRootName )
				ch.Destroy();
		}
	}

	public static void EnsureSceneFoliageCuller( Scene scene, bool decorFluff, bool enableCull )
	{
		if ( !decorFluff || !enableCull || scene is null || !scene.IsValid() )
			return;

		foreach ( var ts in scene.GetAllComponents<ThornsTerrainSystem>() )
		{
			if ( !ts.IsValid() || !ts.Enabled )
				continue;

			var c = ts.Components.Get<ThornsFoliageDistanceCullSystem>( FindMode.EverythingInSelfAndDescendants );
			if ( c.IsValid() )
				return;

			_ = ts.Components.Create<ThornsFoliageDistanceCullSystem>();
			return;
		}
	}

	/// <summary>Rebuilds all decorative foliage under <paramref name="chunkRoot"/> from <paramref name="spec"/>.</summary>
	public static void TryScatterLocalUnderChunk( GameObject chunkRoot, Scene scene, in ThornsTerrainNetSpec spec )
	{
		if ( !chunkRoot.IsValid() || scene is null || !scene.IsValid() )
			return;

		EnsureDecorNetDefaults( spec );

		DestroyDecorUnderChunk( chunkRoot );

		if ( !spec.DecorGenerateFoliageFluff )
			return;

		var decorRoot = new GameObject( true, DecorRootName );
		decorRoot.NetworkMode = NetworkMode.Never;
		decorRoot.SetParent( chunkRoot );

		if ( spec.DecorGrass.ScatterGrassFoliage )
			ScatterGrass( chunkRoot, decorRoot, scene, in spec, spec.DecorGrass );

		if ( spec.DecorMushroom.ScatterMushroomFoliage )
			ScatterMushrooms( chunkRoot, decorRoot, scene, in spec, spec.DecorMushroom );

		EnsureSceneFoliageCuller( scene, spec.DecorGenerateFoliageFluff, spec.DecorEnableFoliageDistanceCulling );
	}

	static bool IsSpawnableLandHeight( in ThornsTerrainNetSpec spec, float localHeightZ ) =>
		localHeightZ >= spec.WaterLevelWorldZ;

	static bool ScatterAcceptByNoise01( Random rnd, float noise01 )
	{
		noise01 = Math.Clamp( noise01, 0f, 1f );
		var p = 0.14f + MathF.Pow( noise01, 1.1f ) * 0.86f;
		return rnd.NextDouble() < Math.Clamp( p, 0.12f, 0.993f );
	}

	static bool ChunkXYInsideFootprintObb(
		float lx,
		float ly,
		float centerX,
		float centerY,
		float halfW,
		float halfD,
		float yawRad,
		float margin )
	{
		var dx = lx - centerX;
		var dy = ly - centerY;
		var c = MathF.Cos( -yawRad );
		var s = MathF.Sin( -yawRad );
		var bx = dx * c - dy * s;
		var by = dx * s + dy * c;
		return MathF.Abs( bx ) <= halfW + margin && MathF.Abs( by ) <= halfD + margin;
	}

	/// <summary>Maps inflated terrain pads back to foundation OBB half extents, then applies the same margin as host scatter.</summary>
	public static bool ChunkPointOverlapsAnyProcBuildingFootprintFromPads(
		float lx,
		float ly,
		IReadOnlyList<ThornsTerrainProcBuildingPad> pads )
	{
		if ( pads is null || pads.Count == 0 )
			return false;

		const float doorApproachMargin = ThornsBuildingModule.Cell * 0.7f;
		const float margin = ThornsBuildingModule.Cell * 0.65f;

		for ( var i = 0; i < pads.Count; i++ )
		{
			var pad = pads[i];
			var fh = pad.FoundationHalfW > 1f
				? pad.FoundationHalfW
				: Math.Max( 0.01f, pad.HalfW - doorApproachMargin );
			var fd = pad.FoundationHalfD > 1f
				? pad.FoundationHalfD
				: Math.Max( 0.01f, pad.HalfD - doorApproachMargin );
			if ( ChunkXYInsideFootprintObb( lx, ly, pad.CenterX, pad.CenterY, fh, fd, pad.YawRadians, margin ) )
				return true;
		}

		return false;
	}

	static bool ScatterPointBlocked(
		float lx,
		float ly,
		in ThornsTerrainNetSpec spec,
		IReadOnlyList<ThornsTerrainProcBuildingPad> pads )
	{
		if ( ChunkPointOverlapsAnyProcBuildingFootprintFromPads( lx, ly, pads ) )
			return true;

		if ( ThornsWorldRoadTerrain.PointInFoliageClearance( lx, ly, in spec ) )
		{
			ThornsWorldGenerationQaMetrics.RecordFoliageScatterRoadSkip();
			return true;
		}

		return false;
	}

	static void ScatterGrass(
		GameObject chunkRoot,
		GameObject decorRoot,
		Scene scene,
		in ThornsTerrainNetSpec spec,
		ThornsTerrainDecorGrassNet g )
	{
		var variantCount = Math.Max( 1, g.ScatterGrassVariantCount );
		var prefix = string.IsNullOrWhiteSpace( g.ScatterGrassModelPathPrefix )
			? ThornsFoliageScatter.ClutterGrassDecorPrefix
			: g.ScatterGrassModelPathPrefix.Trim();

		if ( ThornsFoliageScatter.IsLegacyTerrainGrassDecorPath( prefix ) )
		{
			Log.Warning(
				"[Thorns] Grass decor scatter skipped — legacy models/foliage/grass* was removed (use terraingen clutter grass only)." );
			return;
		}

		if ( ThornsFoliageScatter.IsClutterGrassDecorPath( prefix ) )
		{
			Log.Info(
				"[Thorns] Grass decor scatter skipped — grass_common_short is spawned only by terraingen client grass (ClientGrassRenderer)." );
			return;
		}

		var loadedModels = new List<Model>( variantCount );
		for ( var i = 1; i <= variantCount; i++ )
		{
			var path = variantCount == 1
				? ( prefix.EndsWith( ".vmdl", StringComparison.OrdinalIgnoreCase ) ? prefix : $"{prefix}.vmdl" )
				: $"{prefix}{i}.vmdl";
			if ( ThornsFoliageScatter.TryLoadDecorModel( path, out var mdl ) )
				loadedModels.Add( mdl );
		}

		if ( loadedModels.Count == 0 )
		{
			Log.Warning( $"[Thorns] Grass foliage skipped: no valid models from prefix '{prefix}' variants 1..{variantCount}." );
			return;
		}

		var patches = Math.Max( 0, g.ScatterGrassPatchCount );
		if ( patches == 0 )
			return;

		var minPer = Math.Min( g.ScatterGrassPerPatchMin, g.ScatterGrassPerPatchMax );
		var maxPer = Math.Max( g.ScatterGrassPerPatchMin, g.ScatterGrassPerPatchMax );
		var radMin = Math.Min( g.ScatterGrassPatchRadiusMin, g.ScatterGrassPatchRadiusMax );
		var radMax = Math.Max( g.ScatterGrassPatchRadiusMin, g.ScatterGrassPatchRadiusMax );
		var yLift = g.ScatterGrassGroundOffset;
		var debugN = Math.Max( 0, g.ScatterGrassDebugSampleCount );

		var rx = Math.Max( 2, spec.HeightmapResolutionX );
		var rz = Math.Max( 2, spec.HeightmapResolutionZ );
		var cells = rx * rz;
		var heights = ArrayPool<float>.Shared.Rent( cells );
		ThornsTerrainGeometry.FillHeightmap( spec, heights );

		try
		{
			var ww = Math.Max( 64f, spec.WorldWidth );
			var wd = Math.Max( 64f, spec.WorldDepth );
			var hw = ww * 0.5f;
			var hd = wd * 0.5f;
			var inset = Math.Clamp( spec.DecorEdgeInsetFraction, 0f, 0.45f );
			var minX = -hw + ww * inset;
			var maxX = hw - ww * inset;
			var minY = -hd + wd * inset;
			var maxY = hd - wd * inset;
			var pads = spec.ProcBuildingTerrainPads;

			var spawnedBlades = 0;
			var sampled = 0;
			var sampleBuf = debugN > 0 ? new List<string>( debugN ) : null;

			void RecordSample( Vector3 worldPos, float scaleU, string tag )
			{
				if ( sampled >= debugN || sampleBuf is null )
					return;
				sampled++;
				sampleBuf.Add( $"#{sampled} {tag} pos=({worldPos.x:F0},{worldPos.y:F0},{worldPos.z:F0}) scale={scaleU:F2}" );
			}

			if ( patches > 0 )
			{
				var rnd = new Random( spec.Seed ^ unchecked( (int)0x2f1cc4b7u ) );
				var grassFluffNoise = ThornsWorldNoise.CreateFoliageFluffNoise( spec.Seed );

				for ( var c = 0; c < patches; c++ )
				{
					float ax = 0f, ay = 0f, ahz = 0f;
					var anchorOk = false;

					for ( var attempt = 0; attempt < 72 && !anchorOk; attempt++ )
					{
						ax = minX + (float)rnd.NextDouble() * (maxX - minX);
						ay = minY + (float)rnd.NextDouble() * (maxY - minY);
						ahz = ThornsTerrainGeometry.SampleHeightLocalZUp(
							heights.AsSpan( 0, cells ),
							rx,
							rz,
							ww,
							wd,
							spec.CenterOnWorldOrigin,
							ax,
							ay );

						if ( !IsSpawnableLandHeight( in spec, ahz ) )
							continue;

						if ( ScatterPointBlocked( ax, ay, in spec, pads ) )
							continue;

						var fpAnchor = ThornsWorldNoise.SampleFoliageFluff01( grassFluffNoise, ax, ay, in spec );
						if ( !ScatterAcceptByNoise01( rnd, fpAnchor ) )
							continue;

						anchorOk = true;
					}

					if ( !anchorOk )
						continue;

					var grassCount = rnd.Next( minPer, maxPer + 1 );
					for ( var k = 0; k < grassCount; k++ )
					{
						var ang = (float)(rnd.NextDouble() * Math.PI * 2.0);
						var rr = radMin + (float)rnd.NextDouble() * Math.Max( 0.001f, radMax - radMin );
						var lx = ax + MathF.Cos( ang ) * rr;
						var ly = ay + MathF.Sin( ang ) * rr;
						lx = Math.Clamp( lx, minX, maxX );
						ly = Math.Clamp( ly, minY, maxY );

						var hz = ThornsTerrainGeometry.SampleHeightLocalZUp(
							heights.AsSpan( 0, cells ),
							rx,
							rz,
							ww,
							wd,
							spec.CenterOnWorldOrigin,
							lx,
							ly );

						if ( !IsSpawnableLandHeight( in spec, hz ) )
							continue;

						if ( ScatterPointBlocked( lx, ly, in spec, pads ) )
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
						worldPos += Vector3.Up * yLift;

						var yaw = (float)(rnd.NextDouble() * 360.0);
						var pitch = ( (float)rnd.NextDouble() * 2f - 1f ) * 3f;
						var roll = ( (float)rnd.NextDouble() * 2f - 1f ) * 3f;
						var mdl = loadedModels[rnd.Next( loadedModels.Count )];
						var u = ThornsFoliageScatter.ComputeClutterGrassUniformScale( mdl, rnd );
						var rot = chunkRoot.WorldRotation * Rotation.From( pitch, yaw, roll );

						var go = ThornsFoliageScatter.SpawnLocalDecorFoliage(
							decorRoot,
							worldPos,
							rot,
							new Vector3( u, u, u ),
							mdl );

						if ( go.IsValid() )
						{
							spawnedBlades++;
							RecordSample( worldPos, u, "blade" );
						}
					}
				}
			}

			Log.Info(
				$"[Thorns] Grass foliage (local decor): bladePatches={patches} bladeProps={spawnedBlades} variantsLoaded={loadedModels.Count} prefix='{prefix}'." );
			if ( sampleBuf is { Count: > 0 } )
				Log.Info( $"[Thorns] Grass foliage samples: {string.Join( " | ", sampleBuf )}" );
		}
		finally
		{
			ArrayPool<float>.Shared.Return( heights );
		}
	}

	static void ScatterMushrooms(
		GameObject chunkRoot,
		GameObject decorRoot,
		Scene scene,
		in ThornsTerrainNetSpec spec,
		ThornsTerrainDecorMushroomNet m )
	{
		var path = string.IsNullOrWhiteSpace( m.ScatterMushroomModelPath )
			? ThornsFoliageScatter.DefaultMushroomModelPath
			: m.ScatterMushroomModelPath;

		if ( !ThornsFoliageScatter.TryLoadMushroomModel( path, out var mushroomModel ) )
		{
			Log.Warning( $"[Thorns] Mushroom foliage skipped: invalid model '{path}'." );
			return;
		}

		var clusters = Math.Max( 0, m.ScatterMushroomClusterCount );
		if ( clusters == 0 )
			return;

		var minPer = Math.Min( m.ScatterMushroomsPerClusterMin, m.ScatterMushroomsPerClusterMax );
		var maxPer = Math.Max( m.ScatterMushroomsPerClusterMin, m.ScatterMushroomsPerClusterMax );
		var radMin = Math.Min( m.ScatterMushroomClusterRadiusMin, m.ScatterMushroomClusterRadiusMax );
		var radMax = Math.Max( m.ScatterMushroomClusterRadiusMin, m.ScatterMushroomClusterRadiusMax );
		var sclMin = Math.Min( m.ScatterMushroomUniformScaleMin, m.ScatterMushroomUniformScaleMax );
		var sclMax = Math.Max( m.ScatterMushroomUniformScaleMin, m.ScatterMushroomUniformScaleMax );
		var yLift = m.ScatterMushroomGroundOffset;
		var debugN = Math.Max( 0, m.ScatterMushroomDebugSampleCount );

		var rx = Math.Max( 2, spec.HeightmapResolutionX );
		var rz = Math.Max( 2, spec.HeightmapResolutionZ );
		var cells = rx * rz;
		var heights = ArrayPool<float>.Shared.Rent( cells );
		ThornsTerrainGeometry.FillHeightmap( spec, heights );

		try
		{
			var ww = Math.Max( 64f, spec.WorldWidth );
			var wd = Math.Max( 64f, spec.WorldDepth );
			var hw = ww * 0.5f;
			var hd = wd * 0.5f;
			var inset = Math.Clamp( spec.DecorEdgeInsetFraction, 0f, 0.45f );
			var minX = -hw + ww * inset;
			var maxX = hw - ww * inset;
			var minY = -hd + wd * inset;
			var maxY = hd - wd * inset;
			var rnd = new Random( spec.Seed ^ unchecked( (int)0x71c941aeu ) );
			var mushroomPropsNoise = ThornsWorldNoise.CreateFoliagePropsNoise( spec.Seed );
			var pads = spec.ProcBuildingTerrainPads;

			var spawned = 0;
			var sampled = 0;
			var sampleBuf = debugN > 0 ? new List<string>( debugN ) : null;

			for ( var c = 0; c < clusters; c++ )
			{
				float ax = 0f, ay = 0f, ahz = 0f;
				var anchorOk = false;

				for ( var attempt = 0; attempt < 92 && !anchorOk; attempt++ )
				{
					ax = minX + (float)rnd.NextDouble() * (maxX - minX);
					ay = minY + (float)rnd.NextDouble() * (maxY - minY);
					ahz = ThornsTerrainGeometry.SampleHeightLocalZUp(
						heights.AsSpan( 0, cells ),
						rx,
						rz,
						ww,
						wd,
						spec.CenterOnWorldOrigin,
						ax,
						ay );

					if ( !IsSpawnableLandHeight( in spec, ahz ) )
						continue;

					if ( ScatterPointBlocked( ax, ay, in spec, pads ) )
						continue;

					var fpAnchor = ThornsWorldNoise.SampleFoliageProps01( mushroomPropsNoise, ax, ay, in spec );
					if ( !ScatterAcceptByNoise01( rnd, fpAnchor ) )
						continue;

					anchorOk = true;
				}

				if ( !anchorOk )
					continue;

				var mushroomCount = rnd.Next( minPer, maxPer + 1 );
				for ( var mi = 0; mi < mushroomCount; mi++ )
				{
					var ang = (float)(rnd.NextDouble() * Math.PI * 2.0);
					var rr = radMin + (float)rnd.NextDouble() * Math.Max( 0.001f, radMax - radMin );
					var lx = ax + MathF.Cos( ang ) * rr;
					var ly = ay + MathF.Sin( ang ) * rr;
					lx = Math.Clamp( lx, minX, maxX );
					ly = Math.Clamp( ly, minY, maxY );

					var hz = ThornsTerrainGeometry.SampleHeightLocalZUp(
						heights.AsSpan( 0, cells ),
						rx,
						rz,
						ww,
						wd,
						spec.CenterOnWorldOrigin,
						lx,
						ly );

					if ( !IsSpawnableLandHeight( in spec, hz ) )
						continue;

					if ( ScatterPointBlocked( lx, ly, in spec, pads ) )
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

					var yaw = (float)(rnd.NextDouble() * 360.0);
					var pitch = ((float)rnd.NextDouble() * 2f - 1f) * 7f;
					var roll = ((float)rnd.NextDouble() * 2f - 1f) * 7f;
					var u = sclMin + (float)rnd.NextDouble() * Math.Max( 0.0001f, sclMax - sclMin );
					var rot = chunkRoot.WorldRotation * Rotation.From( pitch, yaw, roll );
					var mushScale = new Vector3( u, u, u );
					worldPos = ThornsFoliageScatter.AlignPivotWorldPositionMeshBottomOnGround(
						worldPos,
						mushroomModel,
						mushScale,
						rot );
					worldPos += Vector3.Up * yLift;
					worldPos -= Vector3.Up * ThornsFoliageScatter.FoliagePostAlignSinkWorldZ;

					var go = ThornsFoliageScatter.SpawnLocalDecorFoliage(
						decorRoot,
						worldPos,
						rot,
						mushScale,
						mushroomModel,
						Color.White );

					if ( go.IsValid() )
					{
						spawned++;
						if ( sampled < debugN && sampleBuf is not null )
						{
							sampled++;
							sampleBuf.Add( $"#{sampled} pos=({worldPos.x:F0},{worldPos.y:F0},{worldPos.z:F0}) scale={u:F2}" );
						}
					}
				}
			}

			Log.Info( $"[Thorns] Mushroom foliage (local decor): clusters={clusters} props={spawned} model='{path}'." );
			if ( sampleBuf is { Count: > 0 } )
				Log.Info( $"[Thorns] Mushroom foliage samples: {string.Join( " | ", sampleBuf )}" );
		}
		finally
		{
			ArrayPool<float>.Shared.Return( heights );
		}
	}
}
