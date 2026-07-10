#nullable disable

namespace Sandbox;

/// <summary>Grass decor parameters replicated inside <see cref="ThornsTerrainNetSpec"/> so every peer can rebuild identical local-only props.</summary>
public sealed class ThornsTerrainDecorGrassNet
{
	public bool ScatterGrassFoliage { get; set; } = true;

	public int ScatterGrassPatchCount { get; set; } = 350;

	public int ScatterGrassPerPatchMin { get; set; } = 2;

	public int ScatterGrassPerPatchMax { get; set; } = 6;

	public float ScatterGrassPatchRadiusMin { get; set; } = 12f;

	public float ScatterGrassPatchRadiusMax { get; set; } = 64f;

	public float ScatterGrassUniformScaleMin { get; set; } = 18f;

	public float ScatterGrassUniformScaleMax { get; set; } = 42f;

	public float ScatterGrassGroundOffset { get; set; } = 5f;

	public string ScatterGrassModelPathPrefix { get; set; } = ThornsFoliageScatter.ClutterGrassDecorPrefix;

	public int ScatterGrassVariantCount { get; set; } = 1;

	public int ScatterGrassDebugSampleCount { get; set; }

	public static ThornsTerrainDecorGrassNet EngineDefaults() => new();

	public void CopyFrom( ThornsTerrainSystem t )
	{
		ScatterGrassFoliage = t.ScatterGrassFoliage;
		ScatterGrassPatchCount = t.ScatterGrassPatchCount;
		ScatterGrassPerPatchMin = t.ScatterGrassPerPatchMin;
		ScatterGrassPerPatchMax = t.ScatterGrassPerPatchMax;
		ScatterGrassPatchRadiusMin = t.ScatterGrassPatchRadiusMin;
		ScatterGrassPatchRadiusMax = t.ScatterGrassPatchRadiusMax;
		ScatterGrassUniformScaleMin = t.ScatterGrassUniformScaleMin;
		ScatterGrassUniformScaleMax = t.ScatterGrassUniformScaleMax;
		ScatterGrassGroundOffset = t.ScatterGrassGroundOffset;
		ScatterGrassModelPathPrefix = string.IsNullOrWhiteSpace( t.ScatterGrassModelPathPrefix )
			? ThornsFoliageScatter.ClutterGrassDecorPrefix
			: t.ScatterGrassModelPathPrefix.Trim();
		ScatterGrassVariantCount = t.ScatterGrassVariantCount;
		ScatterGrassDebugSampleCount = t.ScatterGrassDebugSampleCount;
	}
}

/// <summary>Mushroom decor parameters replicated inside <see cref="ThornsTerrainNetSpec"/>.</summary>
public sealed class ThornsTerrainDecorMushroomNet
{
	public bool ScatterMushroomFoliage { get; set; }

	public int ScatterMushroomClusterCount { get; set; } = 80;

	public int ScatterMushroomsPerClusterMin { get; set; } = 2;

	public int ScatterMushroomsPerClusterMax { get; set; } = 7;

	public float ScatterMushroomClusterRadiusMin { get; set; } = 24f;

	public float ScatterMushroomClusterRadiusMax { get; set; } = 118f;

	public float ScatterMushroomUniformScaleMin { get; set; } = 220f;

	public float ScatterMushroomUniformScaleMax { get; set; } = 460f;

	public float ScatterMushroomGroundOffset { get; set; }

	public int ScatterMushroomDebugSampleCount { get; set; }

	public string ScatterMushroomModelPath { get; set; } = ThornsFoliageScatter.DefaultMushroomModelPath;

	public static ThornsTerrainDecorMushroomNet EngineDefaults() => new();

	public void CopyFrom( ThornsTerrainSystem t )
	{
		ScatterMushroomFoliage = t.ScatterMushroomFoliage;
		ScatterMushroomClusterCount = t.ScatterMushroomClusterCount;
		ScatterMushroomsPerClusterMin = t.ScatterMushroomsPerClusterMin;
		ScatterMushroomsPerClusterMax = t.ScatterMushroomsPerClusterMax;
		ScatterMushroomClusterRadiusMin = t.ScatterMushroomClusterRadiusMin;
		ScatterMushroomClusterRadiusMax = t.ScatterMushroomClusterRadiusMax;
		ScatterMushroomUniformScaleMin = t.ScatterMushroomUniformScaleMin;
		ScatterMushroomUniformScaleMax = t.ScatterMushroomUniformScaleMax;
		ScatterMushroomGroundOffset = t.ScatterMushroomGroundOffset;
		ScatterMushroomDebugSampleCount = t.ScatterMushroomDebugSampleCount;
		ScatterMushroomModelPath = string.IsNullOrWhiteSpace( t.ScatterMushroomModelPath )
			? ThornsFoliageScatter.DefaultMushroomModelPath
			: t.ScatterMushroomModelPath.Trim();
	}
}
