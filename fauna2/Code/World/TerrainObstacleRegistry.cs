namespace Fauna2;

/// <summary>Local index of trees/rocks that block building until cleared.</summary>
public static class TerrainObstacleRegistry
{
	private static readonly Dictionary<string, TerrainObstacleComponent> _byCell = new();
	private static readonly List<TerrainObstacleComponent> _all = new();

	public static IReadOnlyList<TerrainObstacleComponent> All => _all;
	public static int Count => _all.Count;

	public static void Register( TerrainObstacleComponent obstacle )
	{
		if ( obstacle is null || string.IsNullOrEmpty( obstacle.CellKey ) )
			return;

		_byCell[obstacle.CellKey] = obstacle;
		if ( !_all.Contains( obstacle ) )
			_all.Add( obstacle );
	}

	public static void Unregister( TerrainObstacleComponent obstacle )
	{
		if ( obstacle is null ) return;

		_byCell.Remove( obstacle.CellKey );
		_all.Remove( obstacle );
	}

	public static TerrainObstacleComponent Find( string cellKey ) =>
		_byCell.TryGetValue( cellKey, out var obstacle ) ? obstacle : null;

	public static bool BlocksRect( Vector3 center, Vector2 size ) =>
		FindBlocking( center, size ) is not null;

	public static TerrainObstacleComponent FindBlocking( Vector3 center, Vector2 size )
	{
		var half = size * 0.5f;
		var minX = center.x - half.x;
		var maxX = center.x + half.x;
		var minY = center.y - half.y;
		var maxY = center.y + half.y;
		var radius = GameConstants.ObstacleBlockRadius;
		var radiusSq = radius * radius;

		foreach ( var obstacle in _all )
		{
			if ( !obstacle.IsValid() ) continue;

			var point = obstacle.WorldPosition;
			var closestX = point.x.Clamp( minX, maxX );
			var closestY = point.y.Clamp( minY, maxY );
			var dx = point.x - closestX;
			var dy = point.y - closestY;

			if ( dx * dx + dy * dy <= radiusSq )
				return obstacle;
		}

		return null;
	}

	public static TerrainObstacleComponent Nearest( Vector3 point, float maxDistance )
	{
		TerrainObstacleComponent best = null;
		var bestDist = maxDistance;

		foreach ( var obstacle in _all )
		{
			if ( !obstacle.IsValid() ) continue;

			var d = obstacle.WorldPosition.WithZ( 0 ).Distance( point.WithZ( 0 ) );
			if ( d < bestDist )
			{
				bestDist = d;
				best = obstacle;
			}
		}

		return best;
	}

	public static TerrainObstacleComponent PickAtMouse( Scene scene )
	{
		if ( TryPickFromRaycast( scene, out var obstacle ) )
			return obstacle;

		if ( TryPickFromGround( scene, out obstacle ) )
			return obstacle;

		return null;
	}

	private static bool TryPickFromRaycast( Scene scene, out TerrainObstacleComponent obstacle )
	{
		obstacle = null;

		var camera = scene.Camera;
		if ( !camera.IsValid() ) return false;

		var ray = camera.ScreenPixelToRay( Mouse.Position );
		var end = ray.Position + ray.Forward * 50_000f;
		var bestFraction = float.MaxValue;

		var trace = scene.Trace.Ray( ray.Position, end ).UseRenderMeshes( true );

		foreach ( var result in trace.RunAll() )
		{
			if ( !result.Hit || !result.GameObject.IsValid() ) continue;

			var found = FindInHierarchy( result.GameObject );
			if ( found is null ) continue;
			if ( result.Fraction >= bestFraction ) continue;

			bestFraction = result.Fraction;
			obstacle = found;
		}

		return obstacle is not null;
	}

	private static bool TryPickFromGround( Scene scene, out TerrainObstacleComponent obstacle )
	{
		obstacle = null;

		var camera = scene.Camera;
		if ( !camera.IsValid() ) return false;

		var ray = camera.ScreenPixelToRay( Mouse.Position );
		if ( MathF.Abs( ray.Forward.z ) < 0.0001f ) return false;

		var t = -ray.Position.z / ray.Forward.z;
		if ( t < 0f ) return false;

		var ground = ray.Project( t ).WithZ( 0 );
		obstacle = Nearest( ground, GameConstants.ObstaclePickRadius );
		return obstacle is not null;
	}

	private static TerrainObstacleComponent FindInHierarchy( GameObject go )
	{
		while ( go.IsValid() )
		{
			var obstacle = go.Components.Get<TerrainObstacleComponent>();
			if ( obstacle is not null ) return obstacle;

			go = go.Parent;
		}

		return null;
	}

	public static void Clear()
	{
		_byCell.Clear();
		_all.Clear();
	}
}
