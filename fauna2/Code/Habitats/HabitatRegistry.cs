namespace Fauna2;

/// <summary>
/// Local registry of habitats, populated on every machine since habitats are
/// networked objects. Used for placement validation, animal AI and scoring.
/// </summary>
public static class HabitatRegistry
{
	private static readonly List<HabitatComponent> _all = new();

	public static IReadOnlyList<HabitatComponent> All => _all;
	public static int Count => _all.Count;

	public static void Register( HabitatComponent habitat )
	{
		if ( !_all.Contains( habitat ) ) _all.Add( habitat );
	}

	public static void Unregister( HabitatComponent habitat ) => _all.Remove( habitat );

	public static HabitatComponent Find( string habitatId ) =>
		string.IsNullOrEmpty( habitatId ) ? null : _all.FirstOrDefault( h => h.HabitatId == habitatId );

	/// <summary>Find the habitat whose footprint contains a world point.</summary>
	public static HabitatComponent FindAt( Vector3 position ) =>
		_all.FirstOrDefault( h => h.ContainsPoint( position ) );

	/// <summary>Find habitat from a picked fence/visual child object.</summary>
	public static HabitatComponent FindFromHierarchy( GameObject go )
	{
		while ( go.IsValid() )
		{
			var habitat = go.Components.Get<HabitatComponent>();
			if ( habitat is not null ) return habitat;

			go = go.Parent;
		}

		return null;
	}

	/// <summary>Find habitat when clicking near its perimeter fence.</summary>
	public static HabitatComponent FindFenceAt( Vector3 position, float band = 56f )
	{
		HabitatComponent best = null;
		var bestDist = float.MaxValue;

		foreach ( var habitat in _all )
		{
			var dist = DistanceToPerimeter( position, habitat );
			if ( dist > band || dist >= bestDist ) continue;

			bestDist = dist;
			best = habitat;
		}

		return best;
	}

	static float DistanceToPerimeter( Vector3 position, HabitatComponent habitat )
	{
		var pos = habitat.GameObject.WorldPosition;
		var hx = habitat.Size.x * 0.5f;
		var hy = habitat.Size.y * 0.5f;
		var dx = MathF.Abs( position.x - pos.x );
		var dy = MathF.Abs( position.y - pos.y );

		var outsideX = MathF.Max( 0f, dx - hx );
		var outsideY = MathF.Max( 0f, dy - hy );

		if ( outsideX > 0f || outsideY > 0f )
			return MathF.Sqrt( outsideX * outsideX + outsideY * outsideY );

		return MathF.Min( hx - dx, hy - dy );
	}

	/// <summary>Would a rect at this position overlap any existing habitat?</summary>
	public static bool OverlapsAny( Vector3 center, Vector2 size, HabitatComponent ignore = null )
	{
		foreach ( var h in _all )
		{
			if ( h == ignore ) continue;
			var dx = MathF.Abs( h.GameObject.WorldPosition.x - center.x );
			var dy = MathF.Abs( h.GameObject.WorldPosition.y - center.y );
			if ( dx < (h.Size.x + size.x) * 0.5f && dy < (h.Size.y + size.y) * 0.5f )
				return true;
		}
		return false;
	}

	public static float AverageScore() =>
		_all.Count == 0 ? 0f : _all.Average( h => h.Score );

	public static void Clear() => _all.Clear();
}
