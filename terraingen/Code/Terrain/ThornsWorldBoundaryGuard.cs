namespace Terraingen.TerrainGen;

using Terraingen;

/// <summary>Teleports players back inside the terrain footprint when they slip past invisible walls or fall into the void.</summary>
public static class ThornsWorldBoundaryGuard
{
	const float PlanarMarginInches = 64f;
	const float MinFallBelowSeaInches = 800f;

	public static bool TryKeepPlayerOnMap( GameObject body )
	{
		if ( !body.IsValid() )
			return false;

		var terrain = ThornsTerrainCache.Current ?? ThornsTerrainCache.Resolve( body.Scene );
		if ( !terrain.IsValid() )
			return false;

		var pos = body.WorldPosition;
		var clamped = ThornsTerrainSurface.ClampToTerrainBounds( terrain, pos, PlanarMarginInches );
		var seaZ = ThornsTerrainSurface.GetSeaLevelWorldZ( terrain, ThornsTerrainCache.Config );
		var planarEscape = (clamped.WithZ( 0f ) - pos.WithZ( 0f )).LengthSquared > 4f;
		var fellTooFar = pos.z < seaZ - MinFallBelowSeaInches;

		if ( !planarEscape && !fellTooFar )
			return false;

		if ( fellTooFar )
		{
			if ( ThornsTerrainSurface.TrySnapToTerrain( terrain, clamped, out var snapped ) )
				clamped = snapped;
			else
				clamped.z = seaZ + 24f;
		}

		body.WorldPosition = clamped;
		return true;
	}
}
