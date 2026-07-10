namespace Fauna2;

/// <summary>Render diagnostics — logs when Fauna2Debug is enabled.</summary>
public static class Fauna2RenderDiagnostics
{
	private static int _spawnLogs;
	private static int _depthSortLogs;

	public static void ResetSession()
	{
		_spawnLogs = 0;
		_depthSortLogs = 0;
		WorldSprites.ResetDiagnostics();
		Fauna2Debug.Info( "Render", "Session reset" );
	}

	public static void LogSpriteSpawn(
		SpriteRenderer renderer,
		string name,
		Texture texture,
		GameObject parent,
		float worldSize,
		bool depthSort,
		float layer )
	{
		if ( !Fauna2Debug.Enabled || _spawnLogs >= 12 ) return;
		_spawnLogs++;
		Fauna2Debug.Info( "Render",
			$"sprite #{_spawnLogs} '{name}' parent={parent?.Name} size={worldSize:0.#} " +
			$"texValid={texture.IsValid()} depth={depthSort} layer={layer:0.#}" );
	}

	public static void LogRendererState( string tag, SpriteRenderer renderer )
	{
		if ( !Fauna2Debug.Enabled || !renderer.IsValid() ) return;
		Fauna2Debug.Info( "Render",
			$"{tag} {DescribeRenderer( renderer )}" );
	}

	public static void LogDepthSort( SpriteRenderer sprite, Vector3 feet, float sortZ, float layer, string tag = "depth" )
	{
		if ( !Fauna2Debug.Enabled || !sprite.IsValid() || _depthSortLogs >= 16 ) return;
		_depthSortLogs++;
		Fauna2Debug.Info( "Render",
			$"depth #{_depthSortLogs} [{tag}] go={sprite.GameObject.Name} feet={feet} sortZ={sortZ:0.###} " +
			$"layer={layer:0.##} worldPos={sprite.GameObject.WorldPosition} sorted={sprite.IsSorted}" );
	}

	public static void DumpScene( Scene scene, string tag )
	{
		if ( !Fauna2Debug.Enabled || !scene.IsValid() ) return;

		var sprites = scene.GetAllComponents<SpriteRenderer>().Where( r => r.IsValid() ).ToList();
		var ground = sprites.Count( r => r.GameObject.Tags.Has( "ground" ) );
		var sortedGround = sprites.Count( r => r.GameObject.Tags.Has( "ground" ) && r.IsSorted );
		var unsortedGround = ground - sortedGround;

		Fauna2Debug.Info( "Render",
			$"dump ({tag}) spriteRenderers={sprites.Count} groundTagged={ground} " +
			$"sortedGround={sortedGround} unsortedGround={unsortedGround} " +
			$"sortedSprites={sprites.Count( r => r.IsSorted )} " +
			$"unsortedSprites={sprites.Count( r => !r.IsSorted )}" );

		if ( unsortedGround > 0 && sortedGround > 0 )
			Fauna2Debug.Warn( "Render", $"Mixed ground sort flags: sorted={sortedGround} unsorted={unsortedGround}." );

		if ( sortedGround > 0 )
		{
			var flatGround = sprites.Count( r =>
				r.GameObject.Tags.Has( "ground" ) && r.IsSorted && r.Billboard == SpriteRenderer.BillboardMode.None );
			if ( flatGround == 0 )
				Fauna2Debug.Warn( "Render", $"{sortedGround} ground tiles still sorted+billboard — southern grass may clip props." );
		}

		var sampleGround = sprites.FirstOrDefault( r => r.GameObject.Tags.Has( "ground" ) && r.GameObject.Name.Contains( "Grass" ) )
			?? sprites.FirstOrDefault( r => r.GameObject.Tags.Has( "ground" ) );
		if ( sampleGround.IsValid() )
			LogRendererState( "sample-ground", sampleGround );

		var sampleProp = sprites.FirstOrDefault( r =>
			r.GameObject.Parent?.Tags.Has( "terrain_obstacle" ) == true );
		if ( sampleProp.IsValid() )
			LogRendererState( "sample-prop", sampleProp );

		AuditLayeringAt( scene, PlayerState.Local?.FeetPosition ?? Vector3.Zero );
	}

	public static void AuditLayeringAt( Scene scene, Vector3 feet )
	{
		if ( !Fauna2Debug.Enabled || !scene.IsValid() ) return;

		var sprites = scene.GetAllComponents<SpriteRenderer>().Where( r => r.IsValid() ).ToList();
		var ground = NearestGround( sprites, feet );
		var prop = NearestProp( sprites, feet );
		var player = sprites.FirstOrDefault( r =>
			r.GameObject.Name.Contains( "player_sprite", StringComparison.OrdinalIgnoreCase )
			|| r.GameObject.Parent?.Tags.Has( "player" ) == true );

		Fauna2Debug.Info( "Render", $"layer audit at feet={feet}" );

		if ( ground.IsValid() )
			Fauna2Debug.Info( "Render", $"  ground: {DescribeRenderer( ground )}" );
		else
			Fauna2Debug.Warn( "Render", "  ground: none near player" );

		if ( prop.IsValid() )
			Fauna2Debug.Info( "Render", $"  prop: {DescribeRenderer( prop )}" );

		if ( player.IsValid() )
			Fauna2Debug.Info( "Render", $"  player: {DescribeRenderer( player )}" );

		if ( ground.IsValid() && ground.IsSorted )
			Fauna2Debug.Warn( "Render", "  ground tile is sorted — expect floor-over-prop bugs south of this row." );

		if ( ground.IsValid() && prop.IsValid() && ground.WorldPosition.z >= prop.GameObject.WorldPosition.z - 5f )
			Fauna2Debug.Warn( "Render",
				$"  ground Z ({ground.GameObject.WorldPosition.z:0.##}) too close to prop Z ({prop.GameObject.WorldPosition.z:0.##})" );
	}

	private static SpriteRenderer NearestGround( List<SpriteRenderer> sprites, Vector3 feet )
	{
		SpriteRenderer best = null;
		var bestDist = float.MaxValue;

		foreach ( var sprite in sprites )
		{
			if ( !sprite.GameObject.Tags.Has( "ground" ) ) continue;
			var dist = sprite.GameObject.WorldPosition.WithZ( 0f ).Distance( feet.WithZ( 0f ) );
			if ( dist >= bestDist ) continue;
			bestDist = dist;
			best = sprite;
		}

		return best;
	}

	private static SpriteRenderer NearestProp( List<SpriteRenderer> sprites, Vector3 feet )
	{
		SpriteRenderer best = null;
		var bestDist = float.MaxValue;

		foreach ( var sprite in sprites )
		{
			if ( sprite.GameObject.Tags.Has( "ground" ) ) continue;
			if ( sprite.GameObject.Parent?.Tags.Has( "terrain_obstacle" ) != true ) continue;

			var dist = sprite.GameObject.WorldPosition.WithZ( 0f ).Distance( feet.WithZ( 0f ) );
			if ( dist >= bestDist ) continue;
			bestDist = dist;
			best = sprite;
		}

		return best;
	}

	private static string DescribeRenderer( SpriteRenderer renderer )
	{
		if ( !renderer.IsValid() ) return "invalid";

		var go = renderer.GameObject;
		return $"go={go.Name} worldPos={go.WorldPosition} sorted={renderer.IsSorted} opaque={renderer.Opaque} " +
			$"size={renderer.Size} tags=[{string.Join( ",", go.Tags )}]";
	}
}
