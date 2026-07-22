namespace Offshore;

/// <summary>
/// Screen-locked ocean: seamless PNG tiled across the bottom half.
/// Foreground strip layers over the dock; a duplicate strip sits 100px higher and behind the dock.
/// </summary>
public sealed class WaterBackdrop
{
	readonly List<SpriteActor> _front = new();
	readonly List<SpriteActor> _back = new();
	float _scroll;
	float _tileWidth;
	string _path = "env/water_ocean";

	const float WaterlineZ = 0f;
	/// <summary>Nudge crest slightly below mid-screen under the dock walkway.</summary>
	const float OceanZOffset = -12f;
	/// <summary>
	/// Tall enough to cover past ortho bottom even when OrthographicHeight is a half-extent (~±560).
	/// </summary>
	const float OceanHeight = 720f;
	const float OceanCenterZ = WaterlineZ + OceanZOffset - OceanHeight * 0.5f;
	/// <summary>Duplicate strip raised 100px (behind the pier).</summary>
	const float BackZLift = 100f;
	const float OceanBackCenterZ = OceanCenterZ + BackZLift;

	const float OceanFrontY = 4f;  // in front of dock (DockY ≈ 22)
	const float OceanBackY = 40f;  // behind dock
	/// <summary>Shift front crest so it doesn't stack identically with the back strip.</summary>
	const float FrontScrollOffset = 100f;
	const float ScrollSpeed = 36f;
	const int TileCount = 12;

	public void Build( Scene scene, List<GameObject> ownedBucket )
	{
		_front.Clear();
		_back.Clear();
		_scroll = 0f;

		_path = "env/water_ocean";
		_tileWidth = SpriteSizer.FitHeight( _path, OceanHeight ).x;
		if ( _tileWidth < 1f )
		{
			_path = "env/water_wave";
			_tileWidth = SpriteSizer.FitHeight( _path, OceanHeight ).x;
		}
		if ( _tileWidth < 1f )
			_tileWidth = OceanHeight * (768f / 720f);

		var start = -(TileCount / 2);
		for ( var i = 0; i < TileCount; i++ )
		{
			var idx = start + i;
			var x = idx * _tileWidth;
			// Back first (built earlier), then front — Y depth is what actually sorts.
			_back.Add( Fit( scene, ownedBucket, $"OceanBack{i}", _path, OceanHeight, x, OceanBackCenterZ, OceanBackY ) );
			_front.Add( Fit( scene, ownedBucket, $"OceanFront{i}", _path, OceanHeight, x, OceanCenterZ, OceanFrontY ) );
		}
	}

	public void Update( float dt, TimeOfDayService time, WeatherService weather )
	{
		if ( _tileWidth < 1f )
			return;

		var speed = ScrollSpeed * (0.8f + weather.Wind * 0.5f + weather.WaveIntensity * 0.25f);
		_scroll += dt * speed;
		_scroll %= _tileWidth;
		if ( _scroll < 0f )
			_scroll += _tileWidth;

		var wt = time.WaterTint;
		var tint = new Color(
			Math.Clamp( wt.r * 0.25f + 0.7f, 0.55f, 1f ),
			Math.Clamp( wt.g * 0.3f + 0.75f, 0.6f, 1f ),
			Math.Clamp( wt.b * 0.35f + 0.85f, 0.7f, 1f ),
			1f );

		PlaceStrip( _back, OceanBackY, OceanBackCenterZ, tint, 0f );
		PlaceStrip( _front, OceanFrontY, OceanCenterZ, tint, FrontScrollOffset );
	}

	void PlaceStrip( List<SpriteActor> tiles, float y, float centerZ, Color tint, float xOffset )
	{
		var start = -(TileCount / 2);
		for ( var i = 0; i < tiles.Count; i++ )
		{
			var tile = tiles[i];
			if ( tile is null ) continue;

			var idx = start + i;
			var x = -_scroll + idx * _tileWidth + xOffset;
			tile.GameObject.WorldPosition = new Vector3( x, y, centerZ );
			tile.LockNativeAspect = false;
			tile.Size = new Vector2( _tileWidth, OceanHeight );
			tile.Tint = tint;
		}
	}

	static SpriteActor Fit( Scene scene, List<GameObject> bucket, string name, string path, float height, float x, float z, float y )
	{
		var go = scene.CreateObject();
		go.Name = name;
		go.WorldPosition = new Vector3( x, y, z );
		bucket.Add( go );
		var actor = go.AddComponent<SpriteActor>();
		actor.SetFitHeight( path, height );
		return actor;
	}
}
