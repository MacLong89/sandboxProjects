namespace Fauna2;

/// <summary>Always-on ground render diagnostics — filter console with "Fauna2 Ground".</summary>
public static class GroundDiagnostics
{
	private const string Tag = "[Fauna2 Ground]";
	private static TimeUntil _nextViewportProbe;
	private static bool _pendingPostCullAudit;
	private static int _spawnSampleLogs;

	public static void MarkPostCullAudit() => _pendingPostCullAudit = true;

	public static void ResetSession() => _spawnSampleLogs = 0;

	public static void LogTileManifest()
	{
		var paths = new (string Label, string Path)[]
		{
			("grass", SuppliedTileManifest.GrassPath),
			("forest", SuppliedTileManifest.ForestPath),
			("sand", SuppliedTileManifest.SandPath),
			("water", SuppliedTileManifest.WaterPath),
			("mud", SuppliedTileManifest.MudPath),
			("beach", SuppliedTileManifest.BeachPath),
			("snow", SuppliedTileManifest.SnowPath),
			("rainforest", SuppliedTileManifest.RainforestPath),
			("rock", SuppliedTileManifest.RockPath),
			("dirt", SuppliedTileManifest.DirtPath),
			("wilderness", SuppliedTileManifest.WildernessPath),
		};

		foreach ( var ( label, path ) in paths )
		{
			var exists = FileSystem.Mounted.FileExists( path );
			Log.Info( $"{Tag} Tile manifest '{label}': exists={exists} path={path}" );
		}
	}

	/// <summary>Sample the wilderness ground grid and log % breakdown — filter console with "Fauna2 Biome".</summary>
	public static void LogBiomeDistribution( Biome starterBiome, PlotSystem plots, float worldHalf, float tileSize )
	{
		const string biomeTag = "[Fauna2 Biome]";
		var floorCounts = new Dictionary<string, int>( 16 );
		var regionalCounts = new Dictionary<Biome, int>();
		var visualCounts = new Dictionary<Biome, int>();
		var wildernessSamples = 0;
		var skippedOwned = 0;
		var blendTiles = 0;
		var plotSize = GameConstants.PlotSize;

		foreach ( var center in GroundGrid.TileCentersCoveringRect(
			         -worldHalf, -worldHalf, worldHalf, worldHalf, tileSize ) )
		{
			if ( plots is not null && plots.IsWorldPointOnOwnedPlot( new Vector3( center.x, center.y, 0f ) ) )
			{
				skippedOwned++;
				continue;
			}

			wildernessSamples++;
			var pos = new Vector3( center.x, center.y, 0f );

			var floorLabel = FloorLabel( WildernessBiomeMap.GroundTileAtWorld( pos, starterBiome ) );
			floorCounts[floorLabel] = floorCounts.GetValueOrDefault( floorLabel ) + 1;

			var regional = WildernessBiomeMap.BiomeAtWorld( pos, starterBiome );
			regionalCounts[regional] = regionalCounts.GetValueOrDefault( regional ) + 1;

			var visual = WildernessBiomeMap.ComputeVisualGroundWeights( center.x, center.y ).Primary;
			visualCounts[visual] = visualCounts.GetValueOrDefault( visual ) + 1;

			if ( WildernessBiomeMap.TryGetGroundBlend( center, tileSize, starterBiome, out _, out _ ) )
				blendTiles++;
		}

		if ( wildernessSamples <= 0 )
		{
			Log.Warning( $"{biomeTag} No wilderness tiles sampled (skippedOwned={skippedOwned})." );
			return;
		}

		Log.Info(
			$"{biomeTag} Wilderness coverage: tiles={wildernessSamples}, skippedOwned={skippedOwned}, tileSize={tileSize:0.##}, starter={starterBiome}" );
		LogPercentBreakdown( biomeTag, "floor texture", floorCounts, wildernessSamples );
		LogPercentBreakdown( biomeTag, "regional biome (primary)", regionalCounts, wildernessSamples );
		LogPercentBreakdown( biomeTag, "visual voronoi (primary)", visualCounts, wildernessSamples );
		Log.Info(
			$"{biomeTag}   blend overlay tiles: {blendTiles * 100f / wildernessSamples:F1}% ({blendTiles}/{wildernessSamples})" );

		LogRegionalWeightHints( biomeTag, worldHalf, plots, plotSize, starterBiome );
	}

	public static void LogSpawnSummary(
		string prefix,
		int spawned,
		int blendOverlays,
		int skippedOwned,
		IReadOnlyDictionary<string, int> textureUse,
		IReadOnlyList<GameObject> bucket )
	{
		Log.Info( $"{Tag} Spawn '{prefix}': tiles={spawned}, blends={blendOverlays}, skippedOwned={skippedOwned}, bucketTotal={bucket.Count}" );

		foreach ( var ( tex, count ) in textureUse.OrderByDescending( kv => kv.Value ).Take( 12 ) )
			Log.Info( $"{Tag}   texture '{tex}': {count}" );

		var audited = 0;
		var missingSprite = 0;
		var whiteTexture = 0;
		var disabled = 0;
		var sorted = 0;
		var unsorted = 0;

		foreach ( var go in bucket )
		{
			if ( !go.IsValid() || !go.Tags.Has( "ground" ) )
				continue;

			if ( audited < 48 )
			{
				audited++;
				if ( !go.Enabled ) disabled++;

				var renderer = WorldSprites.GetGroundSpriteRenderer( go );
				if ( !renderer.IsValid() )
				{
					missingSprite++;
					continue;
				}

				if ( renderer.IsSorted ) sorted++;
				else unsorted++;

				if ( !renderer.Sprite.IsValid() || renderer.Sprite.Animations is null || renderer.Sprite.Animations.Count == 0 )
				{
					missingSprite++;
					continue;
				}

				var tex = renderer.Sprite.Animations[0].Frames?[0].Texture;
				if ( tex is null || !tex.IsValid() || ReferenceEquals( tex, Texture.White ) )
					whiteTexture++;
			}

			if ( _spawnSampleLogs < 3 && go.Name.Contains( "Tile", StringComparison.Ordinal ) )
			{
				_spawnSampleLogs++;
				Log.Info( $"{Tag} Spawn sample #{_spawnSampleLogs}: {DescribeGroundTile( go )}" );
			}
		}

		Log.Info( $"{Tag} Spawn audit '{prefix}': sampled={audited}, disabled={disabled}, missingSprite={missingSprite}, " +
			$"whiteTex={whiteTexture}, sorted={sorted}, unsorted={unsorted}" );
	}

	public static void AuditGroundList( string reason, IEnumerable<GameObject> objects )
	{
		var list = objects?.Where( o => o.IsValid() ).ToList() ?? [];
		var groundTagged = list.Count( o => o.Tags.Has( "ground" ) );
		var enabled = 0;
		var disabled = 0;
		var missingRenderer = 0;
		var missingSprite = 0;
		var whiteTexture = 0;
		var sorted = 0;
		var unsorted = 0;
		var zeroSize = 0;

		foreach ( var go in list )
		{
			if ( go.Enabled ) enabled++;
			else disabled++;

			if ( !go.Tags.Has( "ground" ) )
				continue;

			var renderer = WorldSprites.GetGroundSpriteRenderer( go );
			if ( !renderer.IsValid() )
			{
				missingRenderer++;
				continue;
			}

			if ( renderer.IsSorted ) sorted++;
			else unsorted++;

			if ( renderer.Size.x <= 0.01f || renderer.Size.y <= 0.01f )
				zeroSize++;

			if ( !renderer.Sprite.IsValid() || renderer.Sprite.Animations is null || renderer.Sprite.Animations.Count == 0 )
			{
				missingSprite++;
				continue;
			}

			var tex = renderer.Sprite.Animations[0].Frames?[0].Texture;
			if ( tex is null || !tex.IsValid() || ReferenceEquals( tex, Texture.White ) )
				whiteTexture++;
		}

		Log.Info( $"{Tag} Audit ({reason}): objects={list.Count}, groundTag={groundTagged}, enabled={enabled}, disabled={disabled}, " +
			$"missingRenderer={missingRenderer}, missingSprite={missingSprite}, whiteTex={whiteTexture}, " +
			$"sorted={sorted}, unsorted={unsorted}, zeroSize={zeroSize}" );
	}

	public static void TickViewportProbe()
	{
		if ( !_pendingPostCullAudit && _nextViewportProbe )
			return;

		var postCull = _pendingPostCullAudit;
		if ( _pendingPostCullAudit )
		{
			_pendingPostCullAudit = false;
			GroundVisibilityCuller.LogState( "post-cull" );
			LogSceneSpriteSnapshot( "post-cull" );
			LogCameraState();
			LogEnabledGroundSamples();
			LogPropComparison();
		}

		if ( !postCull && !Fauna2Debug.Enabled )
			return;

		_nextViewportProbe = 4f;

		var camera = ZooCameraController.Instance;
		if ( camera is null )
		{
			Log.Warning( $"{Tag} Viewport probe: ZooCameraController is null." );
			return;
		}

		var focus = camera.FocusPoint;
		var ortho = camera.GetOrthoHeight();
		Log.Info( $"{Tag} Viewport probe: focus=({focus.x:0.#},{focus.y:0.#}) orthoH={ortho:0.#}" );

		ProbeWorldPoint( focus, "camera-focus" );
		ProbeWorldPoint( focus + new Vector3( ortho * 0.35f, 0f, 0f ), "focus+east" );
		ProbeWorldPoint( focus + new Vector3( 0f, ortho * 0.35f, 0f ), "focus+north" );

		var starter = ZooState.Instance?.StarterBiome ?? Biome.Grassland;
		var weights = WildernessBiomeMap.ComputeVisualGroundWeights( focus.x, focus.y );
		var tex = WildernessBiomeMap.GroundTileAtWorld( focus, starter );
		Log.Info( $"{Tag} Biome sample @ focus: primary={weights.Primary} secondary={weights.Secondary} blend={weights.BlendRatio:0.##} " +
			$"tile={DescribeTexture( tex )}" );
	}

	private static void LogCameraState()
	{
		var cameraCtrl = ZooCameraController.Instance;
		if ( cameraCtrl is null )
		{
			Log.Warning( $"{Tag} Camera: ZooCameraController is null." );
			return;
		}

		var camGo = cameraCtrl.GameObject;
		var cam = cameraCtrl.Components.Get<CameraComponent>();
		var forward = camGo.WorldRotation.Forward;
		Log.Info( $"{Tag} Camera: pos=({camGo.WorldPosition.x:0.#},{camGo.WorldPosition.y:0.#},{camGo.WorldPosition.z:0.#}) " +
			$"rot=({camGo.WorldRotation.Angles().pitch:0.#},{camGo.WorldRotation.Angles().yaw:0.#},{camGo.WorldRotation.Angles().roll:0.#}) " +
			$"forward=({forward.x:0.##},{forward.y:0.##},{forward.z:0.##}) orthoH={cameraCtrl.GetOrthoHeight():0.#} " +
			$"focus=({cameraCtrl.FocusPoint.x:0.#},{cameraCtrl.FocusPoint.y:0.#},{cameraCtrl.FocusPoint.z:0.#}) " +
			$"mainCam={cam?.IsMainCamera} ortho={cam?.Orthographic} bg={cam?.BackgroundColor}" );
	}

	private static void LogSceneSpriteSnapshot( string reason )
	{
		var scene = Game.ActiveScene;
		if ( !scene.IsValid() )
		{
			Log.Warning( $"{Tag} Scene snapshot ({reason}): no active scene." );
			return;
		}

		var sprites = scene.GetAllComponents<SpriteRenderer>().Where( r => r.IsValid() ).ToList();
		var groundTagged = sprites.Count( r => IsGroundSprite( r ) );
		var sorted = sprites.Count( r => r.IsSorted );
		var enabled = sprites.Count( r => r.Enabled && IsGroundHierarchyEnabled( r.GameObject ) );
		var groundSorted = sprites.Count( r => IsGroundSprite( r ) && r.IsSorted );
		var groundUnsorted = groundTagged - groundSorted;

		Log.Info( $"{Tag} Scene ({reason}): spriteRenderers={sprites.Count}, renderChainEnabled={enabled}, sorted={sorted}, " +
			$"groundRenderers={groundTagged}, groundSorted={groundSorted}, groundUnsorted={groundUnsorted}" );

		if ( groundUnsorted > 0 )
			Log.Warning( $"{Tag} Scene ({reason}): {groundUnsorted} ground sprites still unsorted — may not draw." );
	}

	private static bool IsGroundSprite( SpriteRenderer renderer )
	{
		if ( !renderer.IsValid() )
			return false;

		if ( renderer.GameObject.Tags.Has( "ground" ) )
			return true;

		var node = renderer.GameObject;
		while ( node.Parent.IsValid() )
		{
			node = node.Parent;
			if ( node.Tags.Has( "ground" ) )
				return true;
		}

		return false;
	}

	private static bool IsGroundHierarchyEnabled( GameObject go )
	{
		var node = go;
		while ( node.IsValid() )
		{
			if ( !node.Enabled )
				return false;
			node = node.Parent;
		}

		return true;
	}

	private static void LogEnabledGroundSamples()
	{
		if ( !WorldEnvironment.Instance.IsValid() )
			return;

		var samples = 0;
		foreach ( var go in WorldEnvironment.Instance.EnumerateGroundObjects() )
		{
			if ( !go.IsValid() || !go.Enabled || !go.Tags.Has( "ground" ) )
				continue;

			if ( samples >= 3 )
				break;

			samples++;
			Log.Info( $"{Tag} Enabled sample #{samples}: {DescribeGroundTile( go )}" );
		}

		if ( samples == 0 )
			Log.Warning( $"{Tag} Enabled sample: no enabled ground roots found." );
	}

	private static void LogPropComparison()
	{
		var scene = Game.ActiveScene;
		if ( !scene.IsValid() )
			return;

		var focus = ZooCameraController.Instance?.FocusPoint ?? Vector3.Zero;
		SpriteRenderer bestProp = null;
		var bestPropDist = float.MaxValue;

		foreach ( var renderer in scene.GetAllComponents<SpriteRenderer>() )
		{
			if ( !renderer.IsValid() || !renderer.Enabled )
				continue;

			var parent = renderer.GameObject.Parent;
			if ( parent is null || !parent.Name.StartsWith( "Terrain", StringComparison.Ordinal ) )
				continue;

			var dist = parent.WorldPosition.WithZ( 0f ).Distance( focus.WithZ( 0f ) );
			if ( dist >= bestPropDist )
				continue;

			bestPropDist = dist;
			bestProp = renderer;
		}

		if ( !bestProp.IsValid() )
		{
			Log.Info( $"{Tag} Compare: no terrain prop sprite near focus." );
			return;
		}

		var ground = FindNearestGroundTile( focus, 2000f ).Go;
		Log.Info( $"{Tag} Compare prop: {DescribeRenderer( bestProp, bestProp.GameObject.Parent )}" );
		if ( ground is not null )
			Log.Info( $"{Tag} Compare ground: {DescribeGroundTile( ground )}" );
	}

	private static void ProbeWorldPoint( Vector3 point, string label )
	{
		var nearest = FindNearestGroundTile( point, 1400f );
		if ( nearest.Go is null )
		{
			Log.Warning( $"{Tag} Probe '{label}' @ ({point.x:0.#},{point.y:0.#}): no ground tile within search radius." );
			return;
		}

		Log.Info( $"{Tag} Probe '{label}' @ ({point.x:0.#},{point.y:0.#}): dist={nearest.Distance:0.#} {DescribeGroundTile( nearest.Go )}" );
	}

	public static string DescribeGroundObject( GameObject go ) => DescribeGroundTile( go );

	public static string DescribeGroundTile( GameObject root )
	{
		if ( !root.IsValid() )
			return "invalid root";

		var renderer = WorldSprites.GetGroundSpriteRenderer( root );
		if ( !renderer.IsValid() )
			return $"root='{root.Name}' enabled={root.Enabled} pos={root.WorldPosition} NO_RENDERER";

		return DescribeRenderer( renderer, root );
	}

	private static string DescribeRenderer( SpriteRenderer renderer, GameObject root )
	{
		var spriteGo = renderer.GameObject;
		var tex = TryGetRendererTexture( renderer );
		var sortZ = PixelDepthSorter.SortZFor( root.WorldPosition.WithZ( 0f ), WorldSprites.WildernessLayer );
		var hasSorter = root.GetComponentsInChildren<PixelDepthSorter>( true ).Any();

		return $"root='{root.Name}' rootEnabled={root.Enabled} rootPos={FormatVec( root.WorldPosition )} " +
			$"parentEnabled={(root.Parent.IsValid() ? root.Parent.Enabled.ToString() : "n/a")} " +
			$"rootRot={FormatAngles( root.WorldRotation )} spriteGo='{spriteGo.Name}' spriteEnabled={spriteGo.Enabled} " +
			$"spritePos={FormatVec( spriteGo.WorldPosition )} spriteRot={FormatAngles( spriteGo.WorldRotation )} " +
			$"size=({renderer.Size.x:0.#},{renderer.Size.y:0.#}) sorted={renderer.IsSorted} billboard={renderer.Billboard} " +
			$"opaque={renderer.Opaque} alpha={renderer.Color.a:0.##} rendererEnabled={renderer.Enabled} " +
			$"depthSorter={hasSorter} expectedSortZ={sortZ:0.###} tex={DescribeTexture( tex )}";
	}

	private static string FormatVec( Vector3 v ) => $"({v.x:0.#},{v.y:0.#},{v.z:0.#})";

	private static string FormatAngles( Rotation rot )
	{
		var a = rot.Angles();
		return $"({a.pitch:0.#},{a.yaw:0.#},{a.roll:0.#})";
	}

	public static string DescribeTexture( Texture tex )
	{
		if ( tex is null || !tex.IsValid() )
			return "INVALID";
		if ( ReferenceEquals( tex, Texture.White ) )
			return "WHITE_PLACEHOLDER";

		return string.IsNullOrEmpty( tex.ResourcePath ) ? $"tex@{tex.GetHashCode()}" : tex.ResourcePath;
	}

	public static string TextureKey( Texture tex )
	{
		if ( tex is null || !tex.IsValid() )
			return "INVALID";
		if ( ReferenceEquals( tex, Texture.White ) )
			return "WHITE_PLACEHOLDER";

		return string.IsNullOrEmpty( tex.ResourcePath ) ? $"tex@{tex.GetHashCode()}" : tex.ResourcePath;
	}

	private static string FloorLabel( Texture tex )
	{
		var key = TextureKey( tex );
		if ( key is "INVALID" or "WHITE_PLACEHOLDER" )
			return key;

		var slash = key.LastIndexOf( '/' );
		var file = slash >= 0 ? key[(slash + 1)..] : key;
		if ( file.EndsWith( ".png", StringComparison.OrdinalIgnoreCase ) )
			file = file[..^4];
		if ( file.EndsWith( ".jpg", StringComparison.OrdinalIgnoreCase ) )
			file = file[..^4];
		return file;
	}

	private static void LogPercentBreakdown<TKey>(
		string tag,
		string label,
		Dictionary<TKey, int> counts,
		int total )
	{
		Log.Info( $"{tag}   {label}:" );
		foreach ( var ( key, count ) in counts.OrderByDescending( kv => kv.Value ) )
		{
			var pct = count * 100f / total;
			Log.Info( $"{tag}     {key}: {pct:F1}% ({count})" );
		}
	}

	/// <summary>Coarse plot-grid sample — shows regional primary mix at gameplay scale.</summary>
	private static void LogRegionalWeightHints(
		string tag,
		float worldHalf,
		PlotSystem plots,
		float plotSize,
		Biome starterBiome )
	{
		var plotCounts = new Dictionary<Biome, int>();
		var plotSamples = 0;
		var plotRadius = (int)MathF.Floor( worldHalf / plotSize );

		for ( var py = -plotRadius; py <= plotRadius; py++ )
		{
			for ( var px = -plotRadius; px <= plotRadius; px++ )
			{
				var center = PlotSystem.PlotCenter( px, py );
				if ( center.Length > worldHalf + plotSize * 0.5f )
					continue;
				if ( plots is not null && plots.IsWorldPointOnOwnedPlot( center ) )
					continue;

				plotSamples++;
				var biome = WildernessBiomeMap.BiomeAtWorld( center, starterBiome );
				plotCounts[biome] = plotCounts.GetValueOrDefault( biome ) + 1;
			}
		}

		if ( plotSamples <= 0 )
			return;

		Log.Info( $"{tag}   regional biome by plot ({plotSamples} plots):" );
		foreach ( var ( biome, count ) in plotCounts.OrderByDescending( kv => kv.Value ) )
			Log.Info( $"{tag}     {biome}: {count * 100f / plotSamples:F1}% ({count})" );
	}

	private static Texture TryGetRendererTexture( SpriteRenderer renderer )
	{
		if ( !renderer.Sprite.IsValid() || renderer.Sprite.Animations is null || renderer.Sprite.Animations.Count == 0 )
			return null;

		return renderer.Sprite.Animations[0].Frames?[0].Texture;
	}

	private static (GameObject Go, float Distance) FindNearestGroundTile( Vector3 point, float maxRadius )
	{
		if ( !WorldEnvironment.Instance.IsValid() )
			return (null, 0f);

		GameObject best = null;
		var bestDist = float.MaxValue;

		foreach ( var go in WorldEnvironment.Instance.EnumerateGroundObjects() )
		{
			if ( !go.IsValid() || !go.Tags.Has( "ground" ) )
				continue;

			if ( !WorldSprites.GetGroundSpriteRenderer( go ).IsValid() )
				continue;

			var pos = go.WorldPosition;
			var dist = Vector3.DistanceBetween( point, pos );
			if ( dist > maxRadius || dist >= bestDist )
				continue;

			best = go;
			bestDist = dist;
		}

		return (best, bestDist == float.MaxValue ? 0f : bestDist);
	}
}
