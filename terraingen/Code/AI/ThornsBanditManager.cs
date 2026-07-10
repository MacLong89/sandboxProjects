namespace Terraingen.AI;

using Terraingen.Animals;
using Terraingen.Combat;
using Terraingen.GameData;
using Terraingen.Multiplayer;
using Terraingen.Player;
using Terraingen.TerrainGen;

/// <summary>Host-only ambient bandit spawning (groups near players, similar cadence to animals).</summary>
[Title( "Thorns Bandit Manager" )]
[Category( "Thorns/AI" )]
[Icon( "groups" )]
public sealed class ThornsBanditManager : Component
{
	[Property] public int MaxWorldBandits { get; set; } = 10;

	// Max bandits per ambient encounter
	[Property]
	public int MaxBanditsPerAmbientEncounter { get; set; } = 3;

	// Seconds between ambient spawn rolls
	[Property]
	public float AmbientSpawnIntervalSeconds { get; set; } = 55f;

	// Chance per roll (0–1)
	[Property, Range( 0f, 1f )]
	public float AmbientSpawnChance { get; set; } = 0.35f;

	[Property] public float AmbientSpawnDistanceMin { get; set; } = 1640f;
	[Property] public float AmbientSpawnDistanceMax { get; set; } = 2800f;
	[Property] public float GroupSpawnRadius { get; set; } = 180f;

	public static ThornsBanditManager Instance { get; private set; }

	static readonly List<GameObject> PlayerRoots = new( 16 );
	TimeUntil _nextAmbientSpawnRoll;
	bool _ambientSpawnArmed;

	protected override void OnEnabled()
	{
		Instance = this;
		TryRearmAmbientSpawnAfterHotload();
	}

	void TryRearmAmbientSpawnAfterHotload()
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || !Game.IsPlaying || Scene is null || !Scene.IsValid() )
			return;

		var bootstrap = Scene.GetAllComponents<ThornsTerrainBootstrap>().FirstOrDefault();
		if ( bootstrap is null || !bootstrap.IsWorldApplied )
			return;

		ThornsBanditDirector.EnsureForScene( Scene );
		_ambientSpawnArmed = true;
		if ( _nextAmbientSpawnRoll <= 0f )
			_nextAmbientSpawnRoll = AmbientSpawnIntervalSeconds * Game.Random.Float( 0.5f, 1.2f );
	}

	protected override void OnDisabled()
	{
		if ( Instance == this )
			Instance = null;
	}

	public void OnWorldReady( Terrain terrain, ThornsTerrainConfig config )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || ThornsMinimalTestSceneBootstrap.IsActive )
			return;

		ThornsBanditDirector.EnsureForScene( Scene );
		_ambientSpawnArmed = true;
		_nextAmbientSpawnRoll = AmbientSpawnIntervalSeconds * Game.Random.Float( 0.5f, 1.2f );
		_ = terrain;
		_ = config;
	}

	protected override void OnFixedUpdate()
	{
		if ( Instance != this || !ThornsMultiplayer.IsHostOrOffline || !Game.IsPlaying || !_ambientSpawnArmed )
			return;

		if ( _nextAmbientSpawnRoll )
			TickAmbientBanditSpawn();
	}

	void TickAmbientBanditSpawn()
	{
		_nextAmbientSpawnRoll = AmbientSpawnIntervalSeconds;

		if ( !ThornsAnimalManager.NavMeshReady )
			return;

		if ( Game.Random.Float( 0f, 1f ) > AmbientSpawnChance )
			return;

		if ( !CanSpawnMore() )
			return;

		if ( RemainingSpawnSlots() < 1 )
			return;

		RefreshPlayerRoots();
		if ( PlayerRoots.Count == 0 )
			return;

		var playerIndex = PlayerRoots.Count == 1 ? 0 : Game.Random.Int( 0, PlayerRoots.Count - 1 );
		var player = PlayerRoots[playerIndex];
		if ( !player.IsValid() )
			return;

		if ( !ThornsAnimalSpawnUtil.TryPickAmbientAnchorNearPlayer(
			     Scene,
			     player.WorldPosition,
			     AmbientSpawnDistanceMin,
			     AmbientSpawnDistanceMax,
			     AmbientSpawnDistanceMin,
			     out var anchor ) )
			return;

		if ( !ThornsAnimalSpawnUtil.TryPickDrySpawnPosition( Scene, anchor, GroupSpawnRadius, out var pos, out _ ) )
			return;

		if ( !ThornsAnimalSpawnUtil.IsPlanarDistanceFromAllPlayersAtLeast( Scene, pos, AmbientSpawnDistanceMin ) )
			return;

		var spawned = HostSpawnAmbientEncounter( pos, AmbientSpawnDistanceMin );

		if ( spawned > 0 )
		{
			Log.Info(
				$"[Thorns Bandits] Ambient encounter spawned {spawned} bandit(s) near '{player.Name}' at {pos:F0}." );
		}
	}

	public static bool CanSpawnMore()
	{
		var max = Instance?.MaxWorldBandits ?? 10;
		return ThornsBanditPopulation.CountLiveBandits() < max;
	}

	static int RemainingSpawnSlots()
	{
		var max = Instance?.MaxWorldBandits ?? 10;
		return Math.Max( 0, max - ThornsBanditPopulation.CountLiveBandits() );
	}

	int HostSpawnAmbientEncounter( Vector3 anchor, float minPlayerClearanceInches )
	{
		var remaining = RemainingSpawnSlots();
		if ( remaining < 1 )
			return 0;

		var cap = Math.Max( 1, MaxBanditsPerAmbientEncounter );
		var min = 1;
		var max = Math.Min( cap, remaining );
		if ( max < min )
			return 0;

		var count = min == max ? min : Game.Random.Int( min, max );
		var groupId = Game.Random.Int( 1, 2_000_000_000 );
		var spawned = 0;

		for ( var i = 0; i < count; i++ )
		{
			if ( !CanSpawnMore() )
				break;

			var ring = Game.Random.Float( 0f, MathF.PI * 2f );
			var rad = GroupSpawnRadius * 0.35f + Game.Random.Float( 0f, GroupSpawnRadius * 0.65f );
			var offset = new Vector3( MathF.Cos( ring ) * rad, MathF.Sin( ring ) * rad, 0f );
			var requested = anchor + offset;
			if ( !ThornsAnimalSpawnUtil.TryPickDrySpawnPosition( Scene, requested, GroupSpawnRadius, out var pos, out _ ) )
				continue;

			if ( minPlayerClearanceInches > 0f
			     && !ThornsAnimalSpawnUtil.IsPlanarDistanceFromAllPlayersAtLeast( Scene, pos, minPlayerClearanceInches ) )
				continue;

			ThornsBanditBrain.HostTryResolveSpawnClearOfBanditPeers( ref pos );

			var cfg = ThornsNpcHumanBanditSpawn.Wanderer();
			ThornsNpcHumanBanditSpawn.HostSpawnM4Citizen( Scene, pos, Game.Random, cfg, groupId, i, count );
			spawned++;
		}

		return spawned;
	}

	static void RefreshPlayerRoots()
	{
		var director = ThornsBanditDirector.Instance;
		if ( director is not null && director.IsValid() )
		{
			PlayerRoots.Clear();
			PlayerRoots.AddRange( director.HostGetCachedPlayerRoots() );
			return;
		}

		PlayerRoots.Clear();
		var scene = Game.ActiveScene;
		if ( scene is null || !scene.IsValid() )
			return;

		Terraingen.Core.ThornsPlayerRootCache.Refresh( scene );
		PlayerRoots.AddRange( Terraingen.Core.ThornsPlayerRootCache.RootsReadOnly );
	}
}
