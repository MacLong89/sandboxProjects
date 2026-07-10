#nullable disable

using Terraingen.Clutter;
using Terraingen.Foliage;

namespace Sandbox;

/// <summary>
/// Trade-off between draw distance (distant POIs / ridgelines) and foliage/clutter cost.
/// <see cref="ThornsTerrainSystem.VisibilityTier"/> drives pawn <see cref="CameraComponent.ZFar"/> and terraingen foliage caps.
/// </summary>
public enum ThornsVisibilityTier
{
	/// <summary>Short clip + aggressive foliage/clutter caps (~300 m camera).</summary>
	Performance,

	/// <summary>Default gameplay — see towns and mountain silhouettes without explorer distances.</summary>
	Balanced,

	/// <summary>Longer clip and tree draw for screenshots / exploration.</summary>
	Scenic,
}

public readonly struct ThornsVisibilityValues
{
	public float PawnCameraZFarInches { get; init; }
	public float FoliageCullMaxInches { get; init; }
	public float TreeLodHideMaxInches { get; init; }
	public float ClutterRadiusMaxInches { get; init; }
}

public static class ThornsVisibilityPresets
{
	public static ThornsVisibilityValues Get( ThornsVisibilityTier tier ) => tier switch
	{
		ThornsVisibilityTier.Performance => new ThornsVisibilityValues
		{
			PawnCameraZFarInches = 12000f,
			FoliageCullMaxInches = 95000f,
			TreeLodHideMaxInches = 72000f,
			ClutterRadiusMaxInches = 55f * ThornsClutterConfig.InchesPerMeter,
		},
		ThornsVisibilityTier.Scenic => new ThornsVisibilityValues
		{
			PawnCameraZFarInches = 100000f,
			FoliageCullMaxInches = 120000f,
			TreeLodHideMaxInches = 105000f,
			ClutterRadiusMaxInches = 85f * ThornsClutterConfig.InchesPerMeter,
		},
		_ => new ThornsVisibilityValues
		{
			PawnCameraZFarInches = 72000f,
			FoliageCullMaxInches = 110000f,
			TreeLodHideMaxInches = 92000f,
			ClutterRadiusMaxInches = 70f * ThornsClutterConfig.InchesPerMeter,
		},
	};

	public static void ApplyFoliage( ThornsFoliageConfig config, ThornsVisibilityTier tier )
	{
		if ( config is null )
			return;

		ThornsTerraingenGameplayPresets.ApplyFoliage( config );

		var v = Get( tier );
		config.CullDistanceInches = Math.Min( config.CullDistanceInches, v.FoliageCullMaxInches );
		config.TreeLodHideDistanceInches = Math.Min( config.TreeLodHideDistanceInches, v.TreeLodHideMaxInches );
	}

	public static void ApplyClutter( ThornsClutterConfig config, ThornsVisibilityTier tier )
	{
		if ( config is null )
			return;

		ThornsTerraingenGameplayPresets.ApplyClutter( config );

		var v = Get( tier );
		config.ClutterRadius = Math.Min( config.ClutterRadius, v.ClutterRadiusMaxInches );
		config.GrassRenderRadius = Math.Min( config.GrassRenderRadius, v.ClutterRadiusMaxInches );
	}

	public static void ApplyToLocalPawnCamera( Scene scene, ThornsVisibilityTier tier )
	{
		if ( scene is null || !scene.IsValid() || !Game.IsPlaying )
			return;

		var zFar = Get( tier ).PawnCameraZFarInches;
		foreach ( var cam in scene.GetAllComponents<ThornsPawnCamera>() )
		{
			if ( !cam.IsValid() )
				continue;

			cam.ApplyClipPlanes( zFar );
		}
	}
}
