namespace Terraingen.Combat;

using Terraingen.AI;
using Terraingen.Animals;
using Terraingen.Core;
using Terraingen.Multiplayer;
using Terraingen.Player;
using Terraingen.TerrainGen;
using Terraingen.UI;
using Terraingen.UI.Core;

/// <summary>Shared flags + helpers for the flat-floor bandit combat sandbox (interior furniture test scene).</summary>
public static class ThornsBanditCombatTestScene
{
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

	public static void HostSpawnEncounterNearPlayer(
		Scene scene,
		GameObject player,
		int count,
		float spawnDistance,
		float groupRadius )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || !player.IsValid() || scene is null || !scene.IsValid() )
			return;

		ThornsBanditDirector.EnsureForScene( scene );

		var forward = player.WorldRotation.Forward.WithZ( 0f );
		if ( forward.Length < 0.01f )
			forward = Vector3.Forward;

		forward = forward.Normal;
		var anchor = player.WorldPosition + forward * Math.Max( 120f, spawnDistance );
		anchor = new Vector3( anchor.x, anchor.y, player.WorldPosition.z );
		count = Math.Max( 1, count );
		groupRadius = Math.Max( 40f, groupRadius );
		var groupId = Game.Random.Int( 1, 2_000_000_000 );
		var spawned = 0;

		for ( var i = 0; i < count; i++ )
		{
			var ring = Game.Random.Float( 0f, MathF.PI * 2f );
			var rad = groupRadius * 0.35f + Game.Random.Float( 0f, groupRadius * 0.65f );
			var offset = new Vector3( MathF.Cos( ring ) * rad, MathF.Sin( ring ) * rad, 0f );
			var spawnPos = anchor + offset;
			ThornsBanditBrain.HostTryResolveSpawnClearOfBanditPeers( ref spawnPos );

			var cfg = ThornsNpcHumanBanditSpawn.Wanderer();
			ThornsNpcHumanBanditSpawn.HostSpawnM4Citizen( scene, spawnPos, Game.Random, cfg, groupId, i, count );
			spawned++;
		}

		Log.Info( $"[Thorns Bandit Combat Test] Spawned {spawned} bandit(s) near '{player.Name}' at {anchor:F0}." );
	}

	/// <summary>Spawns a ring of bandits around <paramref name="anchor"/> (outpost-defender or wanderer config).</summary>
	public static int HostSpawnBanditGroup(
		Scene scene,
		Vector3 anchor,
		int count,
		int groupId,
		bool useWandererArchetype = false )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || scene is null || !scene.IsValid() )
			return 0;

		ThornsBanditDirector.EnsureForScene( scene );

		count = Math.Max( 1, count );
		var cfg = useWandererArchetype
			? ThornsNpcHumanBanditSpawn.Wanderer()
			: DefenderGroupConfig( anchor );
		var spawned = 0;

		for ( var i = 0; i < count; i++ )
		{
			var angle = i * (360f / count);
			var offset = new Vector3(
				MathF.Cos( angle * MathF.PI / 180f ) * 220f,
				MathF.Sin( angle * MathF.PI / 180f ) * 220f,
				0f );
			var spawnPos = anchor + offset;
			ThornsBanditBrain.HostTryResolveSpawnClearOfBanditPeers( ref spawnPos );

			if ( !ThornsNpcHumanBanditSpawn.HostSpawnM4Citizen( scene, spawnPos, Game.Random, cfg, groupId, i, count ).IsValid() )
				continue;

			spawned++;
		}

		return spawned;
	}

	static ThornsNpcHumanBanditSpawn.Config DefenderGroupConfig( Vector3 anchor ) =>
		new()
		{
			ObjectName = "IronWolvesDefender",
			Tag = "npc_guild_garrison",
			BanditType = ThornsBanditType.CityDefender,
			Archetype = ThornsBanditArchetypeConfig.CityDefender(),
			UseLeashAnchor = true,
			LeashAnchorWorld = anchor,
			LeashRadius = 900f,
			WanderRadius = 580f,
			AnchorWanderGoalsToCurrentPosition = false,
			AttackRange = ThornsBanditCombat.HumanNpcMaxEngagementRangeWorld,
			LoseRadius = ThornsBanditCombat.HumanNpcMaxEngagementRangeWorld * 1.15f
		};

	public static void SyncLocalPlayerUi( GameObject player )
	{
		EnsureTestPlayerIdentity( player );
		_ = SyncLocalPlayerUiAsync( player );
	}

	/// <summary>Stable account key + player cache for tames/companion AI in flat combat sandboxes.</summary>
	public static void EnsureTestPlayerIdentity( GameObject player )
	{
		if ( !player.IsValid() || player.Scene is null || !player.Scene.IsValid() )
			return;

		ThornsTerrainExplorer.EnsureStandardGameplayComponents( player );

		var session = player.Components.Get<ThornsPlayerSession>() ?? player.Components.Create<ThornsPlayerSession>();
		session.HostEnsurePersistenceKey( Connection.Local );

		var gameplay = player.Components.Get<ThornsPlayerGameplay>();
		gameplay?.HostEnsureProgressInitialized();
		gameplay?.HostApplyMobTestCombatKit();

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
