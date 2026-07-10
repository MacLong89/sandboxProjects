namespace Fauna2;

/// <summary>
/// Enables/disables distant ground sprite tiles based on the active camera view.
/// Preloads a padded ring beyond the viewport so tiles are ready before they scroll on-screen.
/// </summary>
public static class GroundVisibilityCuller
{
	private const float IdleCullInterval = 0.08f;
	private const float FocusMoveThreshold = 40f;
	private const float OrthoChangeThreshold = 24f;

	private static TimeSince _nextIdleCull;
	private static Vector3 _lastFocus;
	private static float _lastOrthoHeight = -1f;
	private static readonly Dictionary<(int X, int Y), List<GameObject>> _chunks = new();
	private static readonly HashSet<(int X, int Y)> _enabledChunks = new();

	public static void SetTargets( IEnumerable<GameObject> objects )
	{
		_chunks.Clear();
		_enabledChunks.Clear();
		_lastOrthoHeight = -1f;

		if ( objects is null )
			return;

		foreach ( var go in objects )
		{
			if ( !IsGroundSprite( go ) )
				continue;

			var pos = go.WorldPosition;
			var key = ChunkKey( pos.x, pos.y );
			if ( !_chunks.TryGetValue( key, out var list ) )
			{
				list = new List<GameObject>( 32 );
				_chunks[key] = list;
			}

			list.Add( go );
		}
	}

	public static void ClearTargets()
	{
		_chunks.Clear();
		_enabledChunks.Clear();
		_lastOrthoHeight = -1f;
	}

	public static void Tick()
	{
		if ( _chunks.Count == 0 )
			return;

		var camera = ZooCameraController.Instance;
		if ( camera is null )
			return;

		var focus = camera.FocusPoint;
		var ortho = camera.GetOrthoHeight();
		if ( ShouldCullImmediately( focus, ortho ) )
			_nextIdleCull = 0f;
		else if ( _nextIdleCull > 0f )
			return;

		_nextIdleCull = IdleCullInterval;
		_lastFocus = focus;
		_lastOrthoHeight = ortho;
		ApplyView( focus, ortho );
	}

	public static void ApplyInitialVisibility()
	{
		_enabledChunks.Clear();
		_nextIdleCull = 0f;
		_lastOrthoHeight = -1f;

		var camera = ZooCameraController.Instance;
		if ( camera is null )
		{
			Log.Warning( "[Fauna2 Ground] Cull init: camera null — disabling all ground tiles." );
			SetAllChunksEnabled( false );
			GroundDiagnostics.MarkPostCullAudit();
			return;
		}

		var focus = camera.FocusPoint;
		var ortho = camera.GetOrthoHeight();
		_lastFocus = focus;
		_lastOrthoHeight = ortho;
		ApplyView( focus, ortho );
		GroundDiagnostics.MarkPostCullAudit();
	}

	public static void LogState( string reason )
	{
		var total = 0;
		var enabled = 0;
		foreach ( var objects in _chunks.Values )
		{
			foreach ( var go in objects )
			{
				if ( !go.IsValid() ) continue;
				total++;
				if ( go.Enabled ) enabled++;
			}
		}

		Log.Info( $"[Fauna2 Ground] Cull {reason}: chunks={_chunks.Count}, enabledChunks={_enabledChunks.Count}, " +
			$"groundSprites={total}, enabled={enabled}, disabled={total - enabled}" );
	}

	private static bool ShouldCullImmediately( Vector3 focus, float ortho )
	{
		if ( _lastOrthoHeight < 0f )
			return true;

		if ( MathF.Abs( ortho - _lastOrthoHeight ) >= OrthoChangeThreshold )
			return true;

		return focus.WithZ( 0f ).Distance( _lastFocus.WithZ( 0f ) ) >= FocusMoveThreshold;
	}

	private static void ApplyView( Vector3 focus, float orthoHeight )
	{
		var aspect = Screen.Height > 1 ? Screen.Width / (float)Screen.Height : 16f / 9f;

		var enableHalfY = orthoHeight * GameConstants.GroundCullEnableMargin;
		var enableHalfX = enableHalfY * aspect;
		var disableHalfY = orthoHeight * GameConstants.GroundCullDisableMargin;
		var disableHalfX = disableHalfY * aspect;

		var enableChunks = CollectChunkKeys(
			focus,
			enableHalfX,
			enableHalfY,
			GameConstants.GroundCullPreloadChunkRing );
		var retainChunks = CollectChunkKeys(
			focus,
			disableHalfX,
			disableHalfY,
			GameConstants.GroundCullRetainChunkRing );

		var nextEnabled = new HashSet<(int X, int Y)>();
		foreach ( var ( key, objects ) in _chunks )
		{
			var shouldEnable = enableChunks.Contains( key )
			                   || (_enabledChunks.Contains( key ) && retainChunks.Contains( key ));

			if ( shouldEnable )
				SetChunkEnabled( key, true, objects );
			else
				SetChunkEnabled( key, false, objects );

			if ( shouldEnable )
				nextEnabled.Add( key );
		}

		_enabledChunks.Clear();
		foreach ( var key in nextEnabled )
			_enabledChunks.Add( key );
	}

	private static HashSet<(int X, int Y)> CollectChunkKeys(
		Vector3 focus,
		float halfX,
		float halfY,
		int chunkRing )
	{
		var minCx = (int)MathF.Floor( (focus.x - halfX) / GameConstants.GroundCullChunkSize ) - chunkRing;
		var maxCx = (int)MathF.Floor( (focus.x + halfX) / GameConstants.GroundCullChunkSize ) + chunkRing;
		var minCy = (int)MathF.Floor( (focus.y - halfY) / GameConstants.GroundCullChunkSize ) - chunkRing;
		var maxCy = (int)MathF.Floor( (focus.y + halfY) / GameConstants.GroundCullChunkSize ) + chunkRing;

		var keys = new HashSet<(int X, int Y)>();
		for ( var cx = minCx; cx <= maxCx; cx++ )
		{
			for ( var cy = minCy; cy <= maxCy; cy++ )
			{
				var key = (cx, cy);
				if ( _chunks.ContainsKey( key ) )
				 keys.Add( key );
			}
		}

		return keys;
	}

	private static void SetAllChunksEnabled( bool enabled )
	{
		foreach ( var ( key, objects ) in _chunks )
			SetChunkEnabled( key, enabled, objects );

		_enabledChunks.Clear();
		if ( enabled )
		{
			foreach ( var key in _chunks.Keys )
				_enabledChunks.Add( key );
		}
	}

	private static SpriteRenderer GetGroundSprite( GameObject go ) =>
		WorldSprites.GetGroundSpriteRenderer( go );

	private static bool IsGroundSprite( GameObject go ) =>
		go.IsValid()
		&& go.Tags.Has( "ground" )
		&& GetGroundSprite( go ).IsValid();

	private static (int X, int Y) ChunkKey( float x, float y ) =>
		((int)MathF.Floor( x / GameConstants.GroundCullChunkSize ),
			(int)MathF.Floor( y / GameConstants.GroundCullChunkSize ));

	private static void SetChunkEnabled( (int X, int Y) key, bool enabled, List<GameObject> objects )
	{
		foreach ( var go in objects )
		{
			if ( !go.IsValid() )
				continue;

			if ( go.Enabled == enabled )
				continue;

			go.Enabled = enabled;
			if ( enabled )
				WakeGroundTile( go );
		}
	}

	private static void WakeGroundTile( GameObject root )
	{
		foreach ( var sorter in root.GetComponentsInChildren<PixelDepthSorter>( true ) )
		{
			if ( sorter.IsValid() )
				sorter.ForceApplyDepth();
		}
	}
}
