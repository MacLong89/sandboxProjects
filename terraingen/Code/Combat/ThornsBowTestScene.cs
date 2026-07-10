namespace Terraingen.Combat;

using Terraingen.AI;
using Terraingen.Core;
using Terraingen.Multiplayer;
using Terraingen.Player;
using Terraingen.TerrainGen;
using Terraingen.UI;
using Terraingen.UI.Core;

/// <summary>Shared flags + helpers for the flat-floor bow sandbox (<c>scenes/thorns_bow_test.scene</c>).</summary>
public static class ThornsBowTestScene
{
	public const string ScenePath = "scenes/thorns_bow_test.scene";

	public static bool IsActive { get; private set; }

	public static void SetActive( bool active ) => IsActive = active;

	public static void EnsureSceneInfrastructure( GameObject host )
	{
		if ( !host.IsValid() )
			return;

		ThornsCombatFeedbackHost.EnsureOn( host );
		ThornsGameplayUiHost.EnsureOnHost( host );
		ThornsBanditDirector.EnsureForScene( host.Scene );
	}

	public static async System.Threading.Tasks.Task SetupArenaAsync(
		Scene scene,
		Terrain terrain,
		bool spawnTargetBandits,
		int targetBanditCount,
		float targetDistanceFromSpawn )
	{
		try
		{
			var bandits = 0;
			if ( spawnTargetBandits && terrain.IsValid() )
			{
				bandits = HostSpawnTargetBandits(
					scene,
					terrain,
					targetBanditCount,
					targetDistanceFromSpawn );
			}

			var player = ThornsSceneObserver.FindLocalPlayerObject( scene );
			if ( player.IsValid() )
				SyncLocalPlayerUi( player );

			Log.Info(
				$"[Thorns Bow Test] Arena ready — bow + arrows equipped, {bandits} target bandit(s) at {targetDistanceFromSpawn:F0}u. " +
				"Craft tab → Weapons for bow recipe; use inventory craft mats to verify hand-crafting." );
		}
		catch ( Exception ex )
		{
			Log.Error( ex, "[Thorns Bow Test] Arena setup failed." );
		}

		await System.Threading.Tasks.Task.CompletedTask;
	}

	public static int HostSpawnTargetBandits(
		Scene scene,
		Terrain terrain,
		int targetBanditCount,
		float targetDistanceFromSpawn )
	{
		if ( !terrain.IsValid() || scene is null || !scene.IsValid() )
			return 0;

		var count = Math.Max( 1, targetBanditCount );
		var distance = Math.Max( 600f, targetDistanceFromSpawn );
		var anchor = SampleFlatSurface( terrain, 0f, distance );
		var groupId = Game.Random.Int( 1, 2_000_000_000 );
		return ThornsBanditCombatTestScene.HostSpawnBanditGroup(
			scene,
			anchor,
			count,
			groupId,
			useWandererArchetype: true );
	}

	static Vector3 SampleFlatSurface( Terrain terrain, float x, float y )
	{
		var rayStart = new Vector3( x, y, terrain.TerrainHeight * 2f );
		var ray = new Ray( rayStart, Vector3.Down );

		if ( terrain.RayIntersects( ray, terrain.TerrainHeight * 4f, out var localHit ) )
			return terrain.GameObject.WorldTransform.PointToWorld( localHit );

		return new Vector3( x, y, terrain.TerrainHeight * 0.35f );
	}

	public static void SyncLocalPlayerUi( GameObject player )
	{
		EnsureTestPlayerIdentity( player );
		_ = SyncLocalPlayerUiAsync( player );
	}

	public static void EnsureTestPlayerIdentity( GameObject player )
	{
		if ( !player.IsValid() || player.Scene is null || !player.Scene.IsValid() )
			return;

		ThornsTerrainExplorer.EnsureStandardGameplayComponents( player );

		var session = player.Components.Get<ThornsPlayerSession>() ?? player.Components.Create<ThornsPlayerSession>();
		session.HostEnsurePersistenceKey( Connection.Local );

		var gameplay = player.Components.Get<ThornsPlayerGameplay>();
		gameplay?.HostEnsureProgressInitialized();
		gameplay?.HostApplyBowTestKit();

		var health = player.Components.Get<ThornsPlayerHealth>();
		health?.HostReset();

		var receiver = ThornsPlayerDamageReceiver.EnsureOn( player );
		if ( receiver is not null && receiver.IsValid )
			receiver.IncomingDamageMultiplier = 1f;

		ThornsPlayerRootCache.Refresh( player.Scene );
	}

	static async System.Threading.Tasks.Task SyncLocalPlayerUiAsync( GameObject player )
	{
		if ( !player.IsValid() )
			return;

		ThornsGameplaySession.PrepareScene();
		await System.Threading.Tasks.Task.Yield();
		await System.Threading.Tasks.Task.Yield();

		ThornsGameplaySession.EnsureLocalPlayerControl( skipCameraReclaim: true );

		var gameplay = player.Components.Get<ThornsPlayerGameplay>();
		gameplay?.RefreshMenuSnapshot();
		player.Components.Get<ThornsFpPresentation>()?.RefreshFromActiveHotbar();
	}
}
