namespace Sandbox;

/// <summary>
/// Holds local-owner presentation (camera, HUD, viewmodel, movement) until terraingen terrain is ready,
/// then keeps the loading overlay for a short beat so boot hitches happen before gameplay appears.
/// </summary>
[Title( "Thorns — World Boot Gate" )]
[Category( "Thorns" )]
[Icon( "hourglass_top" )]
public sealed class ThornsWorldBootGate : Component
{
	const float MinHoldAfterTerrainReadySeconds = 1.0f;
	const float ForceCompleteAfterSeconds = 45f;

	public static bool IsLocalBootComplete { get; private set; } = true;

	/// <summary>True while the local owner should stay on the loading screen (no camera/HUD/viewmodel).</summary>
	public static bool BlocksLocalOwnerPresentation =>
		Game.IsPlaying && _bootActive && !IsLocalBootComplete;

	static bool _bootActive;
	static double _bootStartedRealtime;
	static double _terrainReadyAtRealtime;
	static bool _terrainReady;

	public static void BeginLocalBoot()
	{
		if ( !Game.IsPlaying )
			return;

		if ( _bootActive && !IsLocalBootComplete )
			return;

		_bootActive = true;
		IsLocalBootComplete = false;
		_terrainReady = false;
		_terrainReadyAtRealtime = 0;
		_bootStartedRealtime = Time.Now;
		ThornsLoadingScreenHero.Show( "Loading world…", "Preparing terrain" );
	}

	public static void NotifyTerrainRebuildFinished( Scene scene )
	{
		if ( !_bootActive || scene is null || !scene.IsValid() )
			return;

		if ( IsWorldReadyForPlay( scene ) )
			MarkTerrainReady();
	}

	protected override void OnUpdate()
	{
		if ( !Game.IsPlaying || !_bootActive || IsLocalBootComplete )
			return;

		TickBoot( Scene );
	}

	static void TickBoot( Scene scene )
	{
		if ( !_terrainReady && IsWorldReadyForPlay( scene ) )
			MarkTerrainReady();

		if ( Time.Now - _bootStartedRealtime >= ForceCompleteAfterSeconds )
		{
			CompleteBoot( "timeout" );
			return;
		}

		if ( !_terrainReady )
			return;

		if ( Time.Now - _terrainReadyAtRealtime < MinHoldAfterTerrainReadySeconds )
			return;

		CompleteBoot( "ready" );
	}

	static void MarkTerrainReady()
	{
		if ( _terrainReady )
			return;

		_terrainReady = true;
		_terrainReadyAtRealtime = Time.Now;
	}

	static void CompleteBoot( string reason )
	{
		if ( IsLocalBootComplete )
			return;

		_bootActive = false;
		IsLocalBootComplete = true;
		ThornsLoadingScreenHero.Clear();
		Log.Info( $"[Thorns] World boot complete ({reason})." );
		ThornsMinimapHud.NotifyWorldBootComplete();
	}

	static bool IsWorldReadyForPlay( Scene scene )
	{
		if ( scene is null || !scene.IsValid() )
			return false;

		var expectsTerrain = false;
		foreach ( var ts in scene.GetAllComponents<ThornsTerrainSystem>() )
		{
			if ( !ts.IsValid() || !ts.Enabled )
				continue;

			expectsTerrain = true;
			if ( ts.IsHostWorldGenPending )
				return false;
		}

		var sawChunk = false;
		foreach ( var chunk in scene.GetAllComponents<ThornsTerrainChunk>() )
		{
			if ( !chunk.IsValid() )
				continue;

			sawChunk = true;
			if ( !chunk.HasReplicatedTerrainSpec() )
				continue;

			if ( IsChunkTerrainCollisionReady( chunk.GameObject ) )
				return true;
		}

		if ( expectsTerrain && !sawChunk )
			return false;

		return !expectsTerrain;
	}

	static bool IsChunkTerrainCollisionReady( GameObject chunkRoot )
	{
		if ( chunkRoot is null || !chunkRoot.IsValid() )
			return false;

		foreach ( var child in chunkRoot.Children )
		{
			if ( !child.IsValid() || child.Name != ThornsTerraingenTerrainRuntime.TerrainChildName )
				continue;

			var terrain = child.Components.Get<Terrain>( FindMode.EnabledInSelf );
			if ( !terrain.IsValid() )
				continue;

			var storage = terrain.Storage;
			return storage is not null && storage.HeightMap is not null && storage.HeightMap.Length > 0;
		}

		return false;
	}

	protected override void OnDestroy()
	{
		if ( IsLocalBootComplete )
			return;

		_bootActive = false;
		IsLocalBootComplete = true;
		ThornsLoadingScreenHero.Clear();
	}
}
