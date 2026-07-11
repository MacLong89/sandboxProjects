namespace Fauna2;

/// <summary>Paints every build cell in a habitat footprint with the biome ground tile.</summary>
public static class HabitatGroundOverlay
{
	private static int _attachLogs;

	public static void Attach(
		GameObject parent,
		Vector2 size,
		Biome biome,
		float alpha = 1f,
		string context = null )
	{
		if ( parent is null || size.x <= 0f || size.y <= 0f )
			return;

		var authoredSize = size;
		size = HabitatSizing.EffectiveFootprint( size );

		var habitatRoot = parent.Parent.IsValid() ? parent.Parent : parent;
		var habitatWorld = habitatRoot.WorldPosition.WithZ( 0f );
		var texture = WildernessBiomeMap.GroundTile( biome );
		var tileSize = GameConstants.TileSize;
		var draw = GroundGrid.BuildableDrawSize * PixelArt.TileCoverage;

		var half = size * 0.5f;
		var minX = habitatWorld.x - half.x;
		var minY = habitatWorld.y - half.y;
		var maxX = habitatWorld.x + half.x;
		var maxY = habitatWorld.y + half.y;

		var cells = GroundGrid.TileCentersCoveringRect( minX, minY, maxX, maxY, tileSize ).ToList();
		var columns = cells.Select( c => c.x ).Distinct().Count();
		var rowCount = cells.Select( c => c.y ).Distinct().Count();
		var fenceCells = HabitatFenceGenerator.CreateHabitatFence( Vector3.Zero, size ).Count;
		var alignment = DescribeGridAlignment( habitatWorld, size, tileSize );

		var groundRoot = new GameObject( parent, true, "Ground" );
		var spawned = 0;
		Vector2? firstLocal = null;
		Vector2? lastLocal = null;
		Vector2? firstWorld = null;
		Vector2? lastWorld = null;

		foreach ( var cell in cells )
		{
			var local = new Vector3( cell.x - habitatWorld.x, cell.y - habitatWorld.y, 0f );
			SpawnTile( groundRoot, habitatRoot, texture, draw, local, alpha );
			spawned++;

			var local2 = new Vector2( local.x, local.y );
			firstLocal ??= local2;
			lastLocal = local2;
			firstWorld ??= cell;
			lastWorld = cell;
		}

		var grassHidden = WorldEnvironment.Instance?.HideOwnedGrassInRect( habitatWorld, size ) ?? 0;
		var expectedTiles = (int)MathF.Round( size.x / tileSize ) * (int)MathF.Round( size.y / tileSize );
		var texAspect = texture.IsValid() && texture.Height > 0 ? texture.Width / (float)texture.Height : 1f;
		var sortZ = PixelDepthSorter.SortZForHabitatFloor( habitatWorld );
		var label = string.IsNullOrWhiteSpace( context ) ? "attach" : context;

		LogAttach(
			label,
			biome,
			authoredSize,
			size,
			texture,
			habitatWorld,
			tileSize,
			draw,
			alpha,
			minX,
			minY,
			maxX,
			maxY,
			columns,
			rowCount,
			spawned,
			expectedTiles,
			fenceCells,
			grassHidden,
			alignment,
			firstLocal,
			lastLocal,
			firstWorld,
			lastWorld,
			texAspect,
			sortZ );

		if ( Fauna2Debug.Enabled && spawned > 0 )
			LogSampleTiles( groundRoot, draw, Math.Min( 3, spawned ) );
	}

	private static void SpawnTile(
		GameObject groundRoot,
		GameObject sortOrigin,
		Texture texture,
		float draw,
		Vector3 localPos,
		float alpha )
	{
		var tileRoot = WorldSprites.SpawnHabitatGroundTile(
			groundRoot,
			texture,
			draw,
			localPos,
			sortOrigin,
			"HabitatGroundTile" );

		if ( alpha < 0.99f )
		{
			var renderer = WorldSprites.GetGroundSpriteRenderer( tileRoot );
			if ( renderer.IsValid() )
				renderer.Color = renderer.Color.WithAlpha( alpha );
		}
	}

	private static void LogAttach(
		string context,
		Biome biome,
		Vector2 authoredSize,
		Vector2 footprint,
		Texture texture,
		Vector3 center,
		float tileSize,
		float draw,
		float alpha,
		float minX,
		float minY,
		float maxX,
		float maxY,
		int columns,
		int rows,
		int spawned,
		int expectedTiles,
		int fenceCells,
		int grassHidden,
		GridAlignmentReport alignment,
		Vector2? firstLocal,
		Vector2? lastLocal,
		Vector2? firstWorld,
		Vector2? lastWorld,
		float texAspect,
		float sortZ )
	{
		_attachLogs++;
		var tag = $"[Fauna2 HabitatGround] #{_attachLogs} ctx={context}";
		var summary =
			$"biome={biome} authored={authoredSize.x:0}x{authoredSize.y:0} footprint={footprint.x:0}x{footprint.y:0} " +
			$"center=({center.x:0.##},{center.y:0.##}) parent={DescribeParent( center )} " +
			$"bounds=({minX:0.##},{minY:0.##})-({maxX:0.##},{maxY:0.##}) grid={columns}x{rows} tiles={spawned} " +
			$"expected~{expectedTiles} fence={fenceCells} grassHidden={grassHidden} sortZ={sortZ:0.###} " +
			$"tile={tileSize:0.##} draw={draw:0.##} alpha={alpha:0.##} tex={GroundDiagnostics.TextureKey( texture )} texAspect={texAspect:0.###} " +
			$"snap={alignment.SnapMode} mod=({alignment.ModX:0.##},{alignment.ModY:0.##}) " +
			$"firstLocal={FormatVec( firstLocal )} lastLocal={FormatVec( lastLocal )} " +
			$"firstWorld={FormatVec( firstWorld )} lastWorld={FormatVec( lastWorld )}";

		if ( alignment.Misaligned || spawned != expectedTiles )
			Log.Warning( $"{tag} {summary}" );
		else if ( _attachLogs <= 12 || Fauna2Debug.Enabled )
			Log.Info( $"{tag} {summary}" );
	}

	private static void LogSampleTiles( GameObject groundRoot, float draw, int count )
	{
		var logged = 0;
		foreach ( var child in groundRoot.Children )
		{
			if ( !child.IsValid() || logged >= count )
				break;

			var renderer = WorldSprites.GetGroundSpriteRenderer( child );
			if ( !renderer.IsValid() )
				continue;

			logged++;
			Log.Info( $"[Fauna2 HabitatGround] sample #{logged} local=({child.LocalPosition.x:0.##},{child.LocalPosition.y:0.##}) " +
			         $"world=({child.WorldPosition.x:0.##},{child.WorldPosition.y:0.##}) " +
			         $"renderer.Size=({renderer.Size.x:0.##},{renderer.Size.y:0.##}) draw={draw:0.##} enabled={child.Enabled}" );
		}
	}

	private static GridAlignmentReport DescribeGridAlignment( Vector3 center, Vector2 footprint, float tileSize )
	{
		var intersectionX = BuildSnap.UsesIntersectionSnap( footprint.x, tileSize );
		var intersectionY = BuildSnap.UsesIntersectionSnap( footprint.y, tileSize );
		var modX = PositiveMod( center.x, tileSize );
		var modY = PositiveMod( center.y, tileSize );
		var half = tileSize * 0.5f;

		var misaligned =
			( intersectionX && modX > 0.5f && modX < tileSize - 0.5f )
			|| ( intersectionY && modY > 0.5f && modY < tileSize - 0.5f )
			|| ( !intersectionX && MathF.Abs( modX - half ) > 0.5f )
			|| ( !intersectionY && MathF.Abs( modY - half ) > 0.5f );

		var snapMode = intersectionX && intersectionY
			? "intersection"
			: !intersectionX && !intersectionY
				? "center"
				: $"mixed(x={( intersectionX ? "intersection" : "center" )},y={( intersectionY ? "intersection" : "center" )})";

		return new GridAlignmentReport( snapMode, modX, modY, misaligned );
	}

	private static float PositiveMod( float value, float modulus )
	{
		var r = value % modulus;
		return r < 0f ? r + modulus : r;
	}

	private static string DescribeParent( Vector3 habitatWorld ) =>
		$"({habitatWorld.x:0.##},{habitatWorld.y:0.##})";

	private static string FormatVec( Vector2? v ) =>
		v is { } c ? $"({c.x:0.##},{c.y:0.##})" : "none";

	private readonly record struct GridAlignmentReport( string SnapMode, float ModX, float ModY, bool Misaligned );
}
