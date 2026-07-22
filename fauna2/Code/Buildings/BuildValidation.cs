namespace Fauna2;

/// <summary>
/// Placement rules shared by the client ghost preview (instant feedback) and
/// the host (authoritative validation). Money/unlock checks live separately —
/// these are purely spatial rules.
/// </summary>
public static class BuildValidation
{
	public static bool CanPlace( PlaceableDefinition def, Vector3 position, out string error, out Vector3 resolvedPosition, float yawDegrees = 0f )
	{
		error = null;
		resolvedPosition = position;

		if ( def is null )
		{
			error = "Unknown object.";
			return false;
		}

		var plots = PlotSystem.Instance;
		if ( !plots.IsValid() )
		{
			error = "Zoo not ready.";
			return false;
		}

		plots.EnsureStarterPlot();

		var footprint = RotatedFootprint( def.EffectiveFootprint, yawDegrees );
		var placementPos = position;

		if ( def.IsPathTile && !TryResolvePathPlacement( position, footprint, out placementPos, out error ) )
			return false;

		resolvedPosition = placementPos;

		if ( def.IsEntrance )
		{
			if ( !plots.IsWorldPointOnOwnedPlot( position ) )
			{
				error = plots.PlotCount == 0
					? "Land ownership not ready — try again."
					: "Must be built on owned land.";
				return false;
			}
		}
		else if ( !plots.ContainsRect( placementPos, footprint, requireAllCorners: true ) )
		{
			error = plots.PlotCount == 0
				? "Land ownership not ready — try again."
				: "Must be built on owned land.";
			return false;
		}

		if ( TerrainObstacleRegistry.FindBlocking( placementPos, footprint ) is { } blocker )
		{
			error = $"Clear the {blocker.DisplayName} under this spot first.";
			return false;
		}

		if ( def.IsHabitat )
		{
			if ( HabitatRegistry.OverlapsAny( placementPos, footprint ) )
			{
				error = "Overlaps another habitat.";
				return false;
			}
		}
		else if ( def.IsEntrance )
		{
			if ( PlaceableRegistry.Entrance.IsValid() )
			{
				error = "Your zoo already has an entrance.";
				return false;
			}

			if ( !IsNearOwnedPlotEdge( placementPos, yawDegrees ) )
			{
				error = "Place the entrance on the outer border of your land (where the grass meets the wilderness).";
				return false;
			}
		}
		else if ( def.IsPathTile )
		{
			if ( !PathNetwork.HasEntrance )
			{
				error = "Build a zoo entrance before laying paths.";
				return false;
			}

			if ( HabitatRegistry.OverlapsAny( placementPos, footprint ) )
			{
				error = "Build paths outside the habitat fence.";
				return false;
			}

			if ( !PathNetwork.WouldPathConnect( placementPos, footprint ) )
			{
				error = "Paths must connect to your entrance or an existing path.";
				return false;
			}

			if ( PlaceableRegistry.HasPathAtTile( placementPos, footprint ) )
			{
				error = "Path already here.";
				return false;
			}
		}
		else if ( def.IsGuestAmenity )
		{
			if ( !PathNetwork.HasGuestAccess )
			{
				error = "Connect paths to your entrance before building guest facilities.";
				return false;
			}

			if ( !PathNetwork.IsNearConnectedPath( placementPos, footprint ) )
			{
				error = "Restrooms, restaurants and shops must be placed next to or one tile away from a guest path.";
				return false;
			}

			if ( PlaceableRegistry.FootprintOverlapsAny( placementPos, footprint, yawDegrees ) )
			{
				error = "Overlaps another building or path.";
				return false;
			}
		}
		else if ( !def.IsHabitat && !def.IsEntrance && PlaceableRegistry.FootprintOverlapsAny( placementPos, footprint, yawDegrees ) )
		{
			error = "Overlaps another building or path.";
			return false;
		}

		return true;
	}

	public static bool CanPlace( PlaceableDefinition def, Vector3 position, out string error, float yawDegrees = 0f ) =>
		CanPlace( def, position, out error, out _, yawDegrees );

	private static bool TryResolvePathPlacement(
		Vector3 cursor,
		Vector2 footprint,
		out Vector3 placementPos,
		out string error )
	{
		error = null;
		placementPos = BuildSnap.SnapPlacement( cursor, footprint );

		if ( !HabitatRegistry.OverlapsAny( placementPos, footprint ) )
			return true;

		var tile = GameConstants.TileSize;
		var origin = placementPos;
		var cursorFlat = cursor.WithZ( 0 );

		for ( var radius = 1; radius <= 4; radius++ )
		{
			Vector3? best = null;
			var bestDist = float.MaxValue;

			for ( var gx = -radius; gx <= radius; gx++ )
			{
				for ( var gy = -radius; gy <= radius; gy++ )
				{
					if ( Math.Max( Math.Abs( gx ), Math.Abs( gy ) ) != radius )
						continue;

					var candidate = origin + new Vector3( gx * tile, gy * tile, 0 );
					if ( HabitatRegistry.OverlapsAny( candidate, footprint ) )
						continue;

					var dist = candidate.WithZ( 0 ).Distance( cursorFlat );
					if ( dist >= bestDist )
						continue;

					bestDist = dist;
					best = candidate;
				}
			}

			if ( best.HasValue )
			{
				placementPos = best.Value;
				return true;
			}
		}

		error = "Build paths outside the habitat fence.";
		return false;
	}

	/// <summary>Entrance must sit on the outer border of owned land (facing wilderness).</summary>
	public static bool IsNearOwnedPlotEdge( Vector3 position, float yawDegrees = 0f )
	{
		var plots = PlotSystem.Instance;
		if ( !plots.IsValid() ) return false;

		var footprint = RotatedFootprint( GameConstants.EntranceFootprint, yawDegrees );
		return plots.IsValidEntrancePlacement( position, footprint );
	}

	public static bool IsUnlocked( PlaceableDefinition def )
	{
		var state = ZooState.Instance;
		return state.IsValid() && def is not null
			&& state.Level >= def.UnlockLevel
			&& state.Prestige >= def.RequiredPrestige;
	}

	public static bool IsUnlocked( AnimalDefinition def )
	{
		var state = ZooState.Instance;
		return state.IsValid() && def is not null
			&& state.Level >= def.UnlockLevel
			&& state.Prestige >= def.RequiredPrestige;
	}

	public static string UnlockHint( PlaceableDefinition def )
	{
		if ( def is null ) return "Unavailable";
		if ( def.RequiredPrestige > 0 )
			return $"Unlocks at level {def.UnlockLevel} · {def.RequiredPrestige} prestige";
		return $"Unlocks at level {def.UnlockLevel}";
	}

	public static string UnlockHint( AnimalDefinition def )
	{
		if ( def is null ) return "Unavailable";
		var text = $"Unlocks at level {def.UnlockLevel}";
		if ( def.RequiredPrestige > 0 )
			text += $" · {def.RequiredPrestige} prestige";
		return text;
	}

	public static Vector2 RotatedFootprint( Vector2 footprint, float yawDegrees )
	{
		var quarterTurns = (int)MathF.Round( yawDegrees / 90f ) % 4;
		if ( quarterTurns is 1 or 3 )
			return new Vector2( footprint.y, footprint.x );

		return footprint;
	}
}
