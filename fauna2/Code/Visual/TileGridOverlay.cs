namespace Fauna2;

/// <summary>Pixel grass tiles overlaid on owned plots and biome base layers.</summary>
public static class TileGridOverlay
{
	private static readonly List<GameObject> _tiles = new();

	public static void ClearOwnedPlotOverlay( PlotSystem plots )
	{
		Clear();

		var count = plots?.OwnedPlots.Count ?? 0;
		Log.Info( $"[Fauna2 Tiles] Owned plot overlay disabled: plots={count}. Base ground grid now uses one tile scale across the whole map." );
	}

	public static void SpawnGroundGrid(
		List<GameObject> bucket,
		float halfExtent,
		Texture tile,
		float tileSize,
		string namePrefix,
		float z = WorldSprites.WildernessLayer,
		PlotSystem ownedPlots = null )
	{
		SpawnWildernessBiomeGrid( bucket, halfExtent, tileSize, namePrefix, z, ownedPlots, _ => tile, blendStarterBiome: null );
	}

	/// <summary>Paint wilderness ground per regional biome.</summary>
	public static void SpawnWildernessBiomeGrid(
		List<GameObject> bucket,
		float halfExtent,
		float tileSize,
		string namePrefix,
		float z,
		PlotSystem ownedPlots,
		Biome starterBiome )
	{
		SpawnWildernessBiomeGrid(
			bucket,
			halfExtent,
			tileSize,
			namePrefix,
			z,
			ownedPlots,
			center => WildernessBiomeMap.GroundTileAtWorld( new Vector3( center.x, center.y, 0f ), starterBiome ),
			blendStarterBiome: starterBiome );
	}

	private static void SpawnWildernessBiomeGrid(
		List<GameObject> bucket,
		float halfExtent,
		float tileSize,
		string namePrefix,
		float z,
		PlotSystem ownedPlots,
		Func<Vector2, Texture> tileForCenter,
		Biome? blendStarterBiome = null )
	{
		var spawned = 0;
		var skippedOwned = 0;
		var blendOverlays = 0;
		var draw = GroundGrid.BaseDrawSize;
		var textureUse = new Dictionary<string, int>( 16 );

		foreach ( var center in GroundGrid.TileCentersCoveringRect(
			         -halfExtent, -halfExtent, halfExtent, halfExtent, tileSize ) )
		{
			if ( ownedPlots is not null && IsOwnedPlotCenter( center, ownedPlots ) )
			{
				skippedOwned++;
				continue;
			}

			var tex = tileForCenter( center );
			var texKey = GroundDiagnostics.TextureKey( tex );
			textureUse[texKey] = textureUse.GetValueOrDefault( texKey ) + 1;
			var wobble = WildernessBiomeMap.GroundTilePlacementOffset( center.x, center.y );
			var worldPos = new Vector3( center.x + wobble.x, center.y + wobble.y, z );

			var go = WorldSprites.SpawnGroundTileWorld(
				worldPos,
				tex,
				draw,
				$"{namePrefix}Tile",
				layer: z );
			go.Tags.Add( "ground" );

			bucket.Add( go );
			spawned++;

			if ( blendStarterBiome is { } biome
			     && WildernessBiomeMap.TryGetGroundBlend( center, tileSize, biome, out var blendTex, out var alpha ) )
			{
				var blendGo = WorldSprites.SpawnGroundBlendOverlayWorld(
					worldPos,
					blendTex,
					draw,
					alpha,
					$"{namePrefix}Blend",
					layer: z );
				blendGo.Tags.Add( "ground" );
				blendGo.Tags.Add( "ground_blend" );
				bucket.Add( blendGo );
				blendOverlays++;
			}
		}

		GroundDiagnostics.LogSpawnSummary( namePrefix, spawned, blendOverlays, skippedOwned, textureUse, bucket );

		if ( Fauna2Debug.Enabled )
			Log.Info( $"[Fauna2 Tiles] Spawned {namePrefix} ground grid: halfExtent={halfExtent:0.##}, tileSize={tileSize:0.##}, drawSize={draw:0.##}, sprites={spawned}, blendOverlays={blendOverlays}, skippedOwned={skippedOwned}." );
	}

	private static bool IsOwnedPlotCenter( Vector2 center, PlotSystem plots ) =>
		plots.IsWorldPointOnOwnedPlot( new Vector3( center.x, center.y, 0 ) );

	/// <summary>Remove wilderness quads under a newly purchased plot before grass is painted.</summary>
	public static int RemoveWildernessGroundUnderPlot( List<GameObject> bucket, string plotKey )
	{
		if ( !PlotSystem.TryParseKey( plotKey, out var px, out var py ) )
			return 0;

		var halfPlot = GameConstants.PlotSize * 0.5f;
		var plotCenter = PlotSystem.PlotCenter( px, py );
		var minX = plotCenter.x - halfPlot;
		var maxX = plotCenter.x + halfPlot;
		var minY = plotCenter.y - halfPlot;
		var maxY = plotCenter.y + halfPlot;
		var removed = 0;

		for ( var i = bucket.Count - 1; i >= 0; i-- )
		{
			var go = bucket[i];
			if ( !go.IsValid() || !go.Name.StartsWith( "Wilderness", StringComparison.Ordinal ) )
				continue;

			var pos = go.WorldPosition;
			if ( pos.x < minX || pos.x > maxX || pos.y < minY || pos.y > maxY )
				continue;

			go.Destroy();
			bucket.RemoveAt( i );
			removed++;
		}

		return removed;
	}

	public static int SpawnOwnedPlotGroundGrid(
		List<GameObject> bucket,
		PlotSystem plots,
		Texture tile,
		float tileSize,
		string namePrefix )
	{
		if ( plots is null )
		{
			Log.Warning( $"[Fauna2 Tiles] {namePrefix} ground grid skipped: PlotSystem is null." );
			return 0;
		}

		var spawned = 0;
		foreach ( var key in plots.OwnedPlots )
			spawned += SpawnOwnedPlotGround( bucket, key, tile, tileSize, namePrefix );

		if ( Fauna2Debug.Enabled )
			Log.Info( $"[Fauna2 Tiles] Spawned {namePrefix} buildable ground grid: plots={plots.OwnedPlots.Count}, tileSize={tileSize:0.##}, drawSize={GroundGrid.BaseDrawSize:0.##}, sprites={spawned}." );

		return spawned;
	}

	/// <summary>Grass overlay for a single purchased plot — avoids rebuilding the whole world.</summary>
	public static int SpawnOwnedPlotGround(
		List<GameObject> bucket,
		string plotKey,
		Texture tile,
		float tileSize,
		string namePrefix )
	{
		if ( !PlotSystem.TryParseKey( plotKey, out var px, out var py ) )
			return 0;

		var spawned = 0;
		var z = WorldSprites.GrassLayer;
		var draw = GroundGrid.BaseDrawSize;
		var halfPlot = GameConstants.PlotSize * 0.5f;
		var plotCenter = PlotSystem.PlotCenter( px, py );

		foreach ( var tileCenter in GroundGrid.TileCentersCoveringRect(
			         plotCenter.x - halfPlot,
			         plotCenter.y - halfPlot,
			         plotCenter.x + halfPlot,
			         plotCenter.y + halfPlot,
			         tileSize ) )
		{
			var go = WorldSprites.SpawnGroundTileWorld(
				new Vector3( tileCenter.x, tileCenter.y, z ),
				tile,
				draw,
				$"{namePrefix}Tile",
				layer: z );
			go.Tags.Add( "ground" );

			bucket.Add( go );
			spawned++;
		}

		return spawned;
	}

	public static void Clear()
	{
		foreach ( var go in _tiles )
			go?.Destroy();
		_tiles.Clear();
	}
}
