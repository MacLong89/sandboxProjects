namespace Terraingen.UI;

using Terraingen.GameData;
using Terraingen.TerrainGen;

/// <summary>
/// Shared world ↔ map UV mapping for the heightfield texture, full map, and minimap.
/// Map UI uses u→east, v→down with world +Y at the top of the image.
/// </summary>
public static class ThornsMapProjection
{
	/// <summary>Water tint threshold — matches <see cref="TerrainMaterialPainter"/> and natural water sampling.</summary>
	public const float VisualWaterLevelOffset = 0.012f;

	public static float GetVisualWaterLevelNormalized( ThornsTerrainConfig config ) =>
		(config?.SeaLevelNormalized ?? 0.06f) + VisualWaterLevelOffset;

	/// <summary>Shared map/minimap tint for lakes, ocean basins, and off-world areas beyond terrain bounds.</summary>
	public static Color MapWaterColor => new Color( 0.40f, 0.58f, 0.74f );

	/// <summary>Off-map backdrop — identical to <see cref="MapWaterColor"/> so minimap edges read as open ocean.</summary>
	public static Color MapOceanColor => MapWaterColor;

	public static bool TryGetTerrainBounds( Terrain terrain, out float minX, out float minY, out float maxX, out float maxY )
	{
		minX = minY = 0f;
		maxX = maxY = 1f;

		if ( !terrain.IsValid() )
			return false;

		var origin = terrain.GameObject.WorldPosition;
		var size = terrain.TerrainSize;
		minX = origin.x;
		minY = origin.y;
		maxX = origin.x + size;
		maxY = origin.y + size;
		return true;
	}

	public static void ApplyBoundsToSnapshot( ThornsMapSnapshotDto snap, Terrain terrain )
	{
		if ( snap is null || !TryGetTerrainBounds( terrain, out var minX, out var minY, out var maxX, out var maxY ) )
			return;

		snap.WorldMinX = minX;
		snap.WorldMinY = minY;
		snap.WorldMaxX = maxX;
		snap.WorldMaxY = maxY;
	}

	public static bool HasValidBounds( ThornsMapSnapshotDto snap ) =>
		snap is not null
		&& snap.WorldMaxX > snap.WorldMinX + 1f
		&& snap.WorldMaxY > snap.WorldMinY + 1f;

	/// <summary>Live terrain footprint when available; otherwise last synced snapshot bounds.</summary>
	public static bool TryResolveActiveBounds( ThornsMapSnapshotDto snap, out float minX, out float minY, out float maxX, out float maxY )
	{
		var terrain = ThornsTerrainCache.Resolve( Game.ActiveScene );
		if ( terrain.IsValid() && TryGetTerrainBounds( terrain, out minX, out minY, out maxX, out maxY ) )
			return true;

		if ( HasValidBounds( snap ) )
		{
			minX = snap.WorldMinX;
			minY = snap.WorldMinY;
			maxX = snap.WorldMaxX;
			maxY = snap.WorldMaxY;
			return true;
		}

		minX = minY = maxX = maxY = 0f;
		return false;
	}

	/// <summary>World XY → 0–1 map widget space (top = +world Y).</summary>
	public static bool TryWorldToMap01( ThornsMapSnapshotDto snap, float worldX, float worldY, out float u, out float v )
	{
		if ( !TryResolveActiveBounds( snap, out var minX, out var minY, out var maxX, out var maxY ) )
		{
			u = v = 0f;
			return false;
		}

		WorldToMap01( minX, minY, maxX, maxY, worldX, worldY, out u, out v );
		return true;
	}

	/// <summary>World XY → 0–1 map widget space (top = +world Y).</summary>
	public static void WorldToMap01( ThornsMapSnapshotDto snap, float worldX, float worldY, out float u, out float v )
	{
		if ( !TryWorldToMap01( snap, worldX, worldY, out u, out v ) )
			u = v = 0f;
	}

	/// <summary>World XY → 0–1 map widget space using explicit terrain bounds.</summary>
	public static void WorldToMap01(
		float worldMinX,
		float worldMinY,
		float worldMaxX,
		float worldMaxY,
		float worldX,
		float worldY,
		out float u,
		out float v )
	{
		var spanX = Math.Max( 1f, worldMaxX - worldMinX );
		var spanY = Math.Max( 1f, worldMaxY - worldMinY );
		u = (worldX - worldMinX) / spanX;
		v = 1f - (worldY - worldMinY) / spanY;
	}

	/// <summary>Map image pixel → heightfield UV (matches <see cref="Foliage.ThornsFoliageBiomeSampler"/> world mapping).</summary>
	public static void MapPixelToHeightfieldUv( int pixelX, int pixelY, int resolution, out float u, out float v )
	{
		var max = Math.Max( 1, resolution - 1 );
		u = pixelX / (float)max;
		v = 1f - pixelY / (float)max;
	}

	/// <summary>Map image pixel → world XY on the terrain footprint.</summary>
	public static void MapPixelToWorld( Terrain terrain, int pixelX, int pixelY, int resolution, out float worldX, out float worldY )
	{
		MapPixelToHeightfieldUv( pixelX, pixelY, resolution, out var u, out var v );
		if ( !TryGetTerrainBounds( terrain, out var minX, out var minY, out var maxX, out var maxY ) )
		{
			worldX = worldY = 0f;
			return;
		}

		worldX = MathX.Lerp( minX, maxX, u );
		worldY = MathX.Lerp( minY, maxY, v );
	}

	/// <summary>Normalized terrain height at world XY using the live heightfield.</summary>
	public static bool TrySampleNormalizedHeight(
		HeightmapField field,
		Terrain terrain,
		float worldX,
		float worldY,
		out float normalizedHeight )
	{
		normalizedHeight = 0f;
		if ( field is null || !TryGetTerrainBounds( terrain, out var minX, out var minY, out var maxX, out var maxY ) )
			return false;

		var spanX = Math.Max( 1f, maxX - minX );
		var spanY = Math.Max( 1f, maxY - minY );
		var u = (worldX - minX) / spanX;
		var v = (worldY - minY) / spanY;
		if ( u < 0f || u > 1f || v < 0f || v > 1f )
			return false;

		normalizedHeight = field.SampleBilinear( u, v );
		return true;
	}
}
