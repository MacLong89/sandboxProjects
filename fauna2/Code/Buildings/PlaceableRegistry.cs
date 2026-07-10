namespace Fauna2;



/// <summary>Local registry of placed (non-habitat) objects on every machine.</summary>

public static class PlaceableRegistry

{

	private static readonly List<PlaceableComponent> _all = new();

	private static readonly List<PlaceableComponent> _paths = new();



	private static PlaceableComponent _entrance;

	private static int _restroomCount;

	private static int _restaurantCount;

	private static int _shopCount;

	private static int _restaurantGuestCapacity;

	private static int _shopGuestCapacity;

	private static float _totalAppeal;
	private static float _totalEducation;
	private static float _totalComfort;

	private static float _pathOperatingCost;

	private static float _utilityOperatingCost;

	private static float _decorationOperatingCost;

	private static float _natureOperatingCost;

	private static float _entranceOperatingCost;



	public static IReadOnlyList<PlaceableComponent> All => _all;

	public static IReadOnlyList<PlaceableComponent> PathList => _paths;

	public static int Count => _all.Count;



	public static void Register( PlaceableComponent p )

	{

		if ( _all.Contains( p ) ) return;



		_all.Add( p );

		ApplyDefinitionCounts( p.Definition, 1 );



		if ( IsPath( p ) )

		{

			_paths.Add( p );

			PathNetwork.Invalidate();
			ZooStatsReport.InvalidateInsightCache();

		}



		if ( IsEntrance( p ) )

		{

			_entrance = p;

			PathNetwork.Invalidate();
			ZooStatsReport.InvalidateInsightCache();

		}

	}



	public static void Unregister( PlaceableComponent p )

	{

		if ( !_all.Remove( p ) ) return;



		ApplyDefinitionCounts( p.Definition, -1 );



		if ( IsPath( p ) )

		{

			_paths.Remove( p );

			PathNetwork.Invalidate();
			ZooStatsReport.InvalidateInsightCache();

		}



		if ( _entrance == p )

		{

			_entrance = null;

			PathNetwork.Invalidate();
			ZooStatsReport.InvalidateInsightCache();

		}

	}



	public static IEnumerable<PlaceableComponent> InsideRect( Vector3 center, Vector2 size )
	{
		foreach ( var p in _all )
		{
			if ( !p.IsValid() ) continue;

			var pos = p.GameObject.WorldPosition;
			if ( MathF.Abs( pos.x - center.x ) <= size.x * 0.5f &&
				 MathF.Abs( pos.y - center.y ) <= size.y * 0.5f )
				yield return p;
		}
	}

	/// <summary>Whether a footprint AABB overlaps any existing placeable (paths included).</summary>
	public static bool FootprintOverlapsAny( Vector3 center, Vector2 footprint, float yawDegrees = 0f, PlaceableComponent ignore = null )
	{
		footprint = BuildValidation.RotatedFootprint( footprint, yawDegrees );

		foreach ( var placeable in _all )
		{
			if ( placeable == ignore || !placeable.IsValid() ) continue;

			var otherCenter = placeable.GameObject.WorldPosition;
			var otherFootprint = placeable.Definition?.EffectiveFootprint ?? Vector2.Zero;
			otherFootprint = BuildValidation.RotatedFootprint( otherFootprint, placeable.GameObject.WorldRotation.Yaw() );

			var dx = MathF.Abs( otherCenter.x - center.x );
			var dy = MathF.Abs( otherCenter.y - center.y );
			if ( dx < (footprint.x + otherFootprint.x) * 0.5f && dy < (footprint.y + otherFootprint.y) * 0.5f )
				return true;
		}

		return false;
	}

	public static PlaceableComponent Nearest( Vector3 point, float maxDistance )

	{

		PlaceableComponent best = null;

		var bestDist = maxDistance;



		foreach ( var p in _all )

		{

			if ( !p.IsValid() ) continue;



			var d = p.GameObject.WorldPosition.WithZ( 0 ).Distance( point.WithZ( 0 ) );

			if ( d < bestDist )

			{

				bestDist = d;

				best = p;

			}

		}



		return best;

	}



	public static float TotalAppeal() => _totalAppeal;
	public static float TotalEducation() => _totalEducation * (ResearchSystem.Instance?.DecorationMultiplier ?? 1f);
	public static float TotalComfort() => _totalComfort * (ResearchSystem.Instance?.DecorationMultiplier ?? 1f);



	public static bool IsPath( PlaceableComponent placeable ) =>

		placeable.IsValid() && placeable.Definition?.IsPathTile == true;



	public static bool IsEntrance( PlaceableComponent placeable ) =>

		placeable.IsValid() && placeable.Definition?.IsEntrance == true;



	public static PlaceableComponent Entrance => _entrance.IsValid() ? _entrance : null;



	public static int RestroomCount => _restroomCount;



	public static int RestaurantCount => _restaurantCount;



	public static int ShopCount => _shopCount;



	public static int RestaurantGuestCapacity => _restaurantGuestCapacity;



	public static int ShopGuestCapacity => _shopGuestCapacity;



	public static float PlaceableOperatingCostPerMinute() =>

		_pathOperatingCost + _utilityOperatingCost + _decorationOperatingCost + _natureOperatingCost + _entranceOperatingCost;



	public static RestaurantComponent PickRestaurantAtMouse( Scene scene ) =>

		PickCollectibleAtMouse( scene );



	public static RestaurantComponent PickCollectibleAtMouse( Scene scene )

	{

		var camera = scene.Camera;

		if ( !camera.IsValid() ) return null;



		var ray = camera.ScreenPixelToRay( Mouse.Position );

		var end = ray.Position + ray.Forward * 50_000f;

		var bestFraction = float.MaxValue;

		RestaurantComponent best = null;



		foreach ( var result in scene.Trace.Ray( ray.Position, end ).UseRenderMeshes( true ).RunAll() )

		{

			if ( !result.Hit || !result.GameObject.IsValid() ) continue;



			var found = FindCollectibleInHierarchy( result.GameObject );

			if ( found is null ) continue;

			if ( result.Fraction >= bestFraction ) continue;



			bestFraction = result.Fraction;

			best = found;

		}



		if ( best is not null )

			return best;



		if ( !TryMouseGroundPoint( scene, out var ground ) )

			return null;



		return NearestCollectible( ground, GameConstants.RestaurantPickRadius );

	}



	private static RestaurantComponent FindCollectibleInHierarchy( GameObject go )

	{

		while ( go.IsValid() )

		{

			var placeable = go.Components.Get<PlaceableComponent>();

			if ( placeable?.Definition?.ProvidesCollectibleRevenue == true )

				return placeable.Components.Get<RestaurantComponent>();



			go = go.Parent;

		}



		return null;

	}



	private static RestaurantComponent NearestCollectible( Vector3 point, float maxDistance )

	{

		RestaurantComponent best = null;

		var bestDist = maxDistance;



		foreach ( var placeable in _all )

		{

			if ( !placeable.IsValid() || placeable.Definition?.ProvidesCollectibleRevenue != true )

				continue;



			var d = CollectibleBuildingHelper.DistanceToFootprint( point, placeable );

			if ( d >= bestDist )

				continue;



			bestDist = d;

			best = placeable.Components.Get<RestaurantComponent>();

		}



		return best;

	}



	private static bool TryMouseGroundPoint( Scene scene, out Vector3 ground )

	{

		ground = default;



		var camera = scene.Camera;

		if ( !camera.IsValid() ) return false;



		var ray = camera.ScreenPixelToRay( Mouse.Position );

		if ( MathF.Abs( ray.Forward.z ) < 0.0001f ) return false;



		var t = -ray.Position.z / ray.Forward.z;

		if ( t < 0f ) return false;



		ground = ray.Project( t ).WithZ( 0 );

		return true;

	}



	public static IEnumerable<PlaceableComponent> Paths => PathList;



	public static PlaceableComponent NearestPath( Vector3 point, float maxDistance )

	{

		PlaceableComponent best = null;

		var bestDist = maxDistance;



		foreach ( var p in _paths )

		{

			if ( !p.IsValid() ) continue;



			var d = p.GameObject.WorldPosition.WithZ( 0 ).Distance( point.WithZ( 0 ) );

			if ( d < bestDist )

			{

				bestDist = d;

				best = p;

			}

		}



		return best;

	}



	/// <summary>Whether a path tile already occupies this snapped build cell.</summary>

	public static bool HasPathAtTile( Vector3 point, Vector2 footprint )

	{

		var snapped = BuildSnap.SnapPlacement( point, footprint );



		foreach ( var path in _paths )

		{

			if ( !path.IsValid() ) continue;



			var pathFootprint = path.Definition?.EffectiveFootprint ?? footprint;

			var pathCenter = BuildSnap.SnapPlacement( path.GameObject.WorldPosition, pathFootprint );

			if ( snapped.WithZ( 0 ).Distance( pathCenter.WithZ( 0 ) ) < 0.01f )

				return true;

		}



		return false;

	}



	public static void Clear()

	{

		_all.Clear();

		_paths.Clear();

		_entrance = null;

		_restroomCount = 0;

		_restaurantCount = 0;

		_shopCount = 0;

		_restaurantGuestCapacity = 0;

		_shopGuestCapacity = 0;

		_totalAppeal = 0f;
		_totalEducation = 0f;
		_totalComfort = 0f;

		_pathOperatingCost = 0f;

		_utilityOperatingCost = 0f;

		_decorationOperatingCost = 0f;

		_natureOperatingCost = 0f;

		_entranceOperatingCost = 0f;

		PathNetwork.Invalidate();
		ZooStatsReport.InvalidateInsightCache();

	}



	private static void ApplyDefinitionCounts( PlaceableDefinition def, int sign )

	{

		if ( def is null ) return;



		if ( def.ProvidesRestroom ) _restroomCount += sign;

		if ( def.ProvidesRestaurant )

		{

			_restaurantCount += sign;

			_restaurantGuestCapacity += sign * def.EffectiveGuestsServed;

		}



		if ( def.ProvidesShop )

		{

			_shopCount += sign;

			_shopGuestCapacity += sign * def.EffectiveGuestsServed;

		}



		_totalAppeal += sign * def.AppealBonus;
		_totalEducation += sign * def.EducationValue;
		_totalComfort += sign * def.ComfortValue;



		if ( def.IsEntrance )

		{

			_entranceOperatingCost += sign * GameConstants.OperatingCostPerEntrancePerMinute;

			return;

		}



		_pathOperatingCost += sign * (def.Category == BuildCategory.Paths ? GameConstants.OperatingCostPerPathPerMinute : 0f);

		_utilityOperatingCost += sign * (def.Category == BuildCategory.Utility ? GameConstants.OperatingCostPerUtilityPerMinute : 0f);

		_decorationOperatingCost += sign * (def.Category == BuildCategory.Decorations ? GameConstants.OperatingCostPerDecorationPerMinute : 0f);

		_natureOperatingCost += sign * (def.Category == BuildCategory.Nature ? GameConstants.OperatingCostPerNaturePerMinute : 0f);

	}

}

