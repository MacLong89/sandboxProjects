namespace Fauna2;

/// <summary>
/// Placement snap rules for the 64-unit build grid.
/// 1×1 objects center on a tile; even-sized footprints center on a tile intersection.
/// </summary>
public static class BuildSnap
{
	public static float TileSize => GameConstants.TileSize;

	public static Vector3 SnapPlacement( Vector3 worldPosition, Vector2 footprintWorld )
	{
		var tile = TileSize;
		return new Vector3(
			SnapAxis( worldPosition.x, footprintWorld.x, tile ),
			SnapAxis( worldPosition.y, footprintWorld.y, tile ),
			worldPosition.z );
	}

	public static Vector3 SnapPlacement( Vector3 worldPosition, PlaceableDefinition def ) =>
		def is null ? worldPosition : SnapPlacement( worldPosition, def.EffectiveFootprint );

	/// <summary>Single snap path shared by build ghost, click placement, and host spawn.</summary>
	public static Vector3 ResolvePlacement(
		Vector3 worldPosition,
		PlaceableDefinition def,
		float yawDegrees = 0f,
		PlotSystem plots = null )
	{
		if ( def is null )
			return worldPosition;

		if ( def.IsEntrance )
			return SnapEntrancePlacement( worldPosition, plots ?? PlotSystem.Instance, yawDegrees );

		var footprint = BuildValidation.RotatedFootprint( def.EffectiveFootprint, yawDegrees );
		return SnapPlacement( worldPosition, footprint );
	}

	/// <summary>Snap to the nearest valid 4×6 entrance placement along the territory border.</summary>
	public static Vector3 SnapEntrancePlacement( Vector3 worldPosition, PlotSystem plots, float yawDegrees = 0f )
	{
		var footprint = BuildValidation.RotatedFootprint( GameConstants.EntranceFootprint, yawDegrees );

		if ( plots.IsValid() )
		{
			if ( plots.FindNearestEntrancePlacement( worldPosition, footprint ) is { } nearest )
				return nearest;

			var probe = plots.IsWorldPointOnOwnedPlot( worldPosition )
				? worldPosition
				: plots.ClampToOwnedTerritory( worldPosition, inset: TileSize * 0.5f );

			if ( plots.FindNearestEntrancePlacement( probe, footprint ) is { } clampedNearest )
				return clampedNearest;
		}

		return SnapPlacement( worldPosition, footprint );
	}

	public static float SnapAxis( float worldAxis, float footprintWorld, float tileSize = 0f )
	{
		if ( tileSize <= 0f ) tileSize = TileSize;

		if ( UsesIntersectionSnap( footprintWorld, tileSize ) )
			return MathF.Round( worldAxis / tileSize ) * tileSize;

		return MathF.Floor( worldAxis / tileSize ) * tileSize + tileSize * 0.5f;
	}

	public static bool UsesIntersectionSnap( float footprintWorld, float tileSize = 0f )
	{
		if ( tileSize <= 0f ) tileSize = TileSize;
		if ( footprintWorld <= tileSize * 0.01f ) return false;

		var tileCount = footprintWorld / tileSize;
		var rounded = (int)MathF.Round( tileCount );
		if ( MathF.Abs( tileCount - rounded ) > 0.01f ) return false;

		return rounded > 0 && rounded % 2 == 0;
	}

	/// <summary>True when two world points snap to the same 1×1 path tile center.</summary>
	public static bool SamePathTile( Vector3 a, Vector3 b )
	{
		var oneTile = new Vector2( TileSize, TileSize );
		var ca = SnapPlacement( a, oneTile );
		var cb = SnapPlacement( b, oneTile );
		return ca.WithZ( 0 ).Distance( cb.WithZ( 0 ) ) < 0.01f;
	}
}
