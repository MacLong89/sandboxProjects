namespace Terraingen.Animals;

using Terraingen.Player;
using Terraingen.TerrainGen;

public static class ThornsAnimalDebug
{
	[ConVar( "animal_debug" )]
	public static bool Enabled { get; set; }

	[ConVar( "animal_verbose" )]
	public static bool Verbose { get; set; }

	[ConVar( "animal_log" )]
	public static bool BehaviorLog { get; set; }

	[ConVar( "animal_move_log" )]
	public static bool MoveLog { get; set; }

	/// <summary>Global multiplier on all animal move speeds (wander + sprint).</summary>
	[ConVar( "animal_speed_multiplier" )]
	public static float SpeedMultiplier { get; set; } = 1.3f;

	/// <summary>Override sprint multiplier for chase/flee/tamed follow. 0 = use each species value.</summary>
	[ConVar( "animal_sprint_multiplier" )]
	public static float SprintMultiplierOverride { get; set; }

	[ConVar( "animal_speed_log" )]
	public static bool SpeedLog { get; set; }

	[ConVar( "animal_speed_log_interval" )]
	public static float SpeedLogIntervalSeconds { get; set; } = 1.25f;

	[ConVar( "animal_combat_log" )]
	public static bool CombatLog { get; set; }

	public static float ResolveGlobalSpeedMultiplier() => Math.Max( 0.1f, SpeedMultiplier );

	public static float ResolveSprintMultiplier( ThornsAnimalSpeciesData species )
	{
		if ( SprintMultiplierOverride > 0f )
			return SprintMultiplierOverride;

		if ( species is not null && species.SprintSpeedMultiplier > 0f )
			return species.SprintSpeedMultiplier;

		return 1.85f;
	}

	public static void LogSpeedSample( ThornsAnimalBrain brain )
	{
		if ( brain is null || !brain.IsValid() )
			return;

		Log.Info( $"[Thorns Animals][Speed] {brain.BuildSpeedDebugLine()}" );
	}

	[ConCmd( "animal_speed_report" )]
	public static void SpeedReport()
	{
		var scene = Game.ActiveScene;
		if ( scene is null || !scene.IsValid() )
		{
			Log.Warning( "[Thorns Animals] animal_speed_report: no active scene." );
			return;
		}

		Log.Info(
			"=== animal_speed_report === "
			+ $"global×{ResolveGlobalSpeedMultiplier():F2} "
			+ $"sprintOverride={( SprintMultiplierOverride > 0f ? SprintMultiplierOverride.ToString( "F2" ) : "species" )} "
			+ $"(player walk={ThornsPlayerMovementDefaults.WalkSpeed:F0} sprint={ThornsPlayerMovementDefaults.WalkSpeed * ThornsPlayerMovementDefaults.SprintSpeedMultiplier:F0} in/s)" );

		var active = 0;
		foreach ( var brain in scene.GetAllComponents<ThornsAnimalBrain>() )
		{
			if ( brain is null || !brain.IsValid() || brain.IsDead )
				continue;

			if ( !brain.IsActiveLocomotionSample )
				continue;

			active++;
			Log.Info( $"[Thorns Animals][Speed] {brain.BuildSpeedDebugLine()}" );
		}

		if ( active == 0 )
			Log.Info( "[Thorns Animals][Speed] No animals in chase/flee/sprint-follow right now — provoke one and rerun." );

		Log.Info( "Tune live: animal_speed_multiplier 1.5 | animal_sprint_multiplier 2.0 | animal_speed_log 1" );
	}

	[ConCmd( "animal_nav_probe" )]
	public static void ProbeNavMesh()
	{
		var scene = Game.ActiveScene;
		if ( scene is null || !scene.IsValid() )
		{
			Log.Warning( "[Thorns Animals] Nav probe failed: no active scene." );
			return;
		}

		var terrain = ThornsTerrainCache.Resolve( scene );
		if ( !terrain.IsValid() )
		{
			Log.Warning( "[Thorns Animals] Nav probe failed: no terrain resolved." );
			return;
		}

		var nav = scene.NavMesh;
		var min = terrain.GameObject.WorldPosition;
		var center = min + new Vector3( terrain.TerrainSize * 0.5f, terrain.TerrainSize * 0.5f, 0f );
		if ( ThornsAnimalWorldUtil.TrySnapToTerrain( terrain, center, out var snappedCenter ) )
			center = snappedCenter;

		var radius = MathF.Min( terrain.TerrainSize * 0.25f, 4200f );
		ReadOnlySpan<Vector2> offsets = stackalloc Vector2[]
		{
			Vector2.Zero,
			new( radius, 0f ),
			new( -radius, 0f ),
			new( 0f, radius ),
			new( 0f, -radius ),
			new( radius, radius ),
			new( -radius, radius ),
			new( radius, -radius ),
			new( -radius, -radius ),
		};

		var usable = 0;
		Log.Info(
			$"[Thorns Animals] Nav probe: managerReady={ThornsAnimalManager.NavMeshReady}, " +
			$"managerUsable={ThornsAnimalManager.NavMeshUsableForAnimals}, " +
			$"navEnabled={nav?.IsEnabled == true}, generating={nav?.IsGenerating == true}, " +
			$"terrainCenter={center:F0}, radius={radius:F0}." );

		for ( var i = 0; i < offsets.Length; i++ )
		{
			var desired = center + new Vector3( offsets[i].x, offsets[i].y, 0f );
			if ( ThornsAnimalWorldUtil.TrySnapToTerrain( terrain, desired, out var snapped ) )
				desired = snapped;

			var closest = nav?.GetClosestPoint( desired, 1024f );
			var closeDist = closest.HasValue ? closest.Value.Distance( desired ) : -1f;
			var available = ThornsAnimalWorldUtil.IsNavMeshAvailableNear( scene, desired, 1024f );
			if ( available )
				usable++;

			Log.Info(
				$"[Thorns Animals] Nav probe sample {i}: desired={desired:F0} " +
				$"available={available} closest={(closest.HasValue ? closest.Value.ToString() : "none")} " +
				$"dist={(closest.HasValue ? closeDist.ToString( "F1" ) : "n/a")}." );
		}

		Log.Info( $"[Thorns Animals] Nav probe result: {usable}/{offsets.Length} sampled point(s) usable." );
	}
}
