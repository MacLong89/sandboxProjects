namespace Sandbox;

/// <summary>
/// Slope helpers for terrain scatter and terraingen foliage (heightmap gradient vs world degrees).
/// </summary>
public static class ThornsTerrainSlope
{
	public const float DefaultMaxTreeSlopeDegrees = 15f;

	/// <summary>
	/// Max <see cref="Terraingen.TerrainGen.TerrainAnalysis"/> gradient magnitude for a ground slope in degrees.
	/// </summary>
	public static float HeightmapGradientFromDegrees(
		float slopeDegrees,
		int heightmapWidth,
		int heightmapHeight,
		float terrainWorldSizeInches,
		float terrainMaxHeightInches )
	{
		slopeDegrees = Math.Clamp( slopeDegrees, 0f, 89f );
		if ( terrainMaxHeightInches < 1f || terrainWorldSizeInches < 1f )
			return 0.09f;

		var tan = MathF.Tan( slopeDegrees * (MathF.PI / 180f) );
		const float runCells = 2f;
		var cellRunX = runCells * terrainWorldSizeInches / Math.Max( 1, heightmapWidth - 1 );
		var cellRunY = runCells * terrainWorldSizeInches / Math.Max( 1, heightmapHeight - 1 );
		var cellRun = Math.Min( cellRunX, cellRunY );
		return tan * cellRun / terrainMaxHeightInches;
	}

	/// <summary>Planar slope at a local chunk position from the replicated heightmap (degrees).</summary>
	public static bool IsPlanarSlopeWithinDegrees(
		ReadOnlySpan<float> heights,
		int rx,
		int rz,
		float worldW,
		float worldD,
		bool centerOnWorldOrigin,
		float localX,
		float localY,
		float maxSlopeDegrees )
	{
		var cellX = worldW / Math.Max( 1, rx - 1 );
		var cellY = worldD / Math.Max( 1, rz - 1 );
		var sample = Math.Max( 1f, Math.Min( cellX, cellY ) );

		var h0 = ThornsTerrainGeometry.SampleHeightLocalZUp(
			heights, rx, rz, worldW, worldD, centerOnWorldOrigin, localX, localY );
		var hx = ThornsTerrainGeometry.SampleHeightLocalZUp(
			heights, rx, rz, worldW, worldD, centerOnWorldOrigin, localX + sample, localY );
		var hy = ThornsTerrainGeometry.SampleHeightLocalZUp(
			heights, rx, rz, worldW, worldD, centerOnWorldOrigin, localX, localY + sample );

		if ( float.IsNaN( h0 ) || float.IsInfinity( h0 )
		     || float.IsNaN( hx ) || float.IsInfinity( hx )
		     || float.IsNaN( hy ) || float.IsInfinity( hy ) )
			return false;

		var dzdx = (hx - h0) / sample;
		var dzdy = (hy - h0) / sample;
		var degrees = MathF.Atan( MathF.Sqrt( dzdx * dzdx + dzdy * dzdy ) ) * (180f / MathF.PI);
		return degrees <= maxSlopeDegrees + 0.01f;
	}
}
