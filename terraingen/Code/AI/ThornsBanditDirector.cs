namespace Terraingen.AI;

using Terraingen.Animals;
using Terraingen.Combat;
using Terraingen.Core;
using Terraingen.Multiplayer;
using Terraingen.Player;

/// <summary>Host player spatial cache for bandit perception and spawn anchoring.</summary>
[Title( "Thorns Bandit Director" )]
[Category( "Thorns/AI" )]
[Icon( "groups" )]
public sealed class ThornsBanditDirector : Component
{
	public static ThornsBanditDirector Instance { get; private set; }

	[Property] public float PlayerRefreshSeconds { get; set; } = 2f;
	[Property] public float LodRefreshSeconds { get; set; } = 0.75f;

	static readonly List<GameObject> PlayerRoots = new( 16 );
	static readonly List<ThornsBanditBrain> BanditSimScratch = new( 64 );
	static readonly HashSet<ThornsBanditBrain> ClosePriorityTicked = new( 32 );

	TimeUntil _nextRefresh;
	TimeUntil _nextLodRefresh;
	int _banditSimCursor;

	protected override void OnEnabled() => Instance = this;

	protected override void OnDisabled()
	{
		if ( Instance == this )
			Instance = null;
	}

	protected override void OnFixedUpdate()
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || !Game.IsPlaying )
			return;

		if ( _nextRefresh )
			RefreshPlayerRoots();

		if ( _nextLodRefresh )
			UpdateBanditLodTiers();

		ThornsBanditSpatialGrid.Rebuild( ThornsBanditPopulation.HostBrainsReadOnly );
		RunStaggeredBanditSimulation();
	}

	void RunStaggeredBanditSimulation()
	{
		BanditSimScratch.Clear();
		ClosePriorityTicked.Clear();
		var brains = ThornsBanditPopulation.HostBrainsReadOnly;
		for ( var i = 0; i < brains.Count; i++ )
		{
			var brain = brains[i];
			if ( brain.IsValid() && !brain.IsDead )
				BanditSimScratch.Add( brain );
		}

		var closeR2 = ThornsBanditCombatTuning.CloseNoticeDistance * ThornsBanditCombatTuning.CloseNoticeDistance;
		if ( BanditSimScratch.Count > 0 && closeR2 > 0f )
		{
			ThornsPlayerRootCache.RefreshIfStale( Scene );
			for ( var i = 0; i < BanditSimScratch.Count; i++ )
			{
				var brain = BanditSimScratch[i];
				if ( !brain.IsValid() || brain.IsDead )
					continue;

				if ( HostNearestAlivePlayerDistSqWithin( brain.GameObject.WorldPosition.WithZ( 0f ), ThornsBanditCombatTuning.CloseNoticeDistance ) >= closeR2 )
					continue;

				brain.HostRunSimulationTick();
				ClosePriorityTicked.Add( brain );
			}
		}

		ThornsNpcTickScheduler.RunRoundRobin(
			BanditSimScratch,
			ref _banditSimCursor,
			ThornsNpcTickScheduler.MaxBanditSimulationsPerFrame,
			brain => ThornsNpcTickScheduler.ShouldSkipBanditSimulation( brain )
			         || ClosePriorityTicked.Contains( brain ),
			( brain, _ ) => brain.HostRunSimulationTick() );
	}

	void UpdateBanditLodTiers()
	{
		_nextLodRefresh = LodRefreshSeconds;
		ThornsPlayerRootCache.RefreshIfStale( Scene );

		var players = ThornsPlayerRootCache.RootsReadOnly;
		var brains = ThornsBanditPopulation.HostBrainsReadOnly;
		for ( var i = 0; i < brains.Count; i++ )
		{
			var brain = brains[i];
			if ( !brain.IsValid() || brain.IsDead )
				continue;

			var pos = brain.GameObject.WorldPosition.WithZ( 0f );
			var minDistSq = float.MaxValue;
			for ( var p = 0; p < players.Count; p++ )
			{
				var player = players[p];
				if ( !player.IsValid() )
					continue;

				var distSq = (pos - player.WorldPosition.WithZ( 0f )).LengthSquared;
				if ( distSq < minDistSq )
					minDistSq = distSq;
			}

			var tier = minDistSq < float.MaxValue
				? ThornsNpcLod.TierForDistanceSquared( minDistSq )
				: ThornsNpcLodTier.Sleeping;

			if ( minDistSq <= ThornsBanditCombatTuning.CloseNoticeDistance * ThornsBanditCombatTuning.CloseNoticeDistance )
				tier = ThornsNpcLodTier.Full;

			if ( brain.State is ThornsBanditAiState.Combat or ThornsBanditAiState.Chase or ThornsBanditAiState.Investigate
			     or ThornsBanditAiState.Reposition or ThornsBanditAiState.Retreat )
			{
				if ( tier == ThornsNpcLodTier.Sleeping )
					tier = ThornsNpcLodTier.Reduced;
			}
			else if ( tier == ThornsNpcLodTier.Sleeping && minDistSq < float.MaxValue )
			{
				var cfg = brain.Archetype;
				if ( cfg is null )
					continue;

				var perceiveRadius = Math.Max( cfg.HearGunshotRangeWorld, cfg.EngagementRangeWorld * 1.05f );
				if ( minDistSq <= perceiveRadius * perceiveRadius )
					tier = ThornsNpcLodTier.Reduced;
			}

			brain.LodTier = tier;
		}
	}

	void RefreshPlayerRoots()
	{
		_nextRefresh = PlayerRefreshSeconds;
		ThornsPlayerRootCache.Refresh( Scene );
		PlayerRoots.Clear();
		PlayerRoots.AddRange( ThornsPlayerRootCache.RootsReadOnly );
	}

	public IReadOnlyList<GameObject> HostGetCachedPlayerRoots()
	{
		if ( PlayerRoots.Count == 0 )
			RefreshPlayerRoots();

		return PlayerRoots;
	}

	public void HostQueryPlayersNearPlanar( Vector3 selfFlat, float radiusWorld, List<GameObject> results )
	{
		results.Clear();
		var r2 = radiusWorld * radiusWorld;

		foreach ( var root in HostGetCachedPlayerRoots() )
		{
			if ( !root.IsValid() )
				continue;

			var delta = root.WorldPosition.WithZ( 0 ) - selfFlat;
			if ( delta.LengthSquared <= r2 )
				results.Add( root );
		}
	}

	public float HostNearestAlivePlayerDistSqWithin( Vector3 selfFlat, float maxDistanceWorld )
	{
		var best = float.MaxValue;
		var r2 = maxDistanceWorld * maxDistanceWorld;

		foreach ( var root in HostGetCachedPlayerRoots() )
		{
			if ( !root.IsValid() )
				continue;

			var d = (root.WorldPosition.WithZ( 0 ) - selfFlat).LengthSquared;
			if ( d < best )
				best = d;
		}

		return best > r2 ? float.MaxValue : best;
	}

	public static void EnsureForScene( Scene scene )
	{
		if ( scene is null || !scene.IsValid() )
			return;

		if ( scene.GetAllComponents<ThornsBanditDirector>().FirstOrDefault() is not null )
			return;

		var go = scene.CreateObject();
		go.Name = "ThornsBanditDirector";
		go.Components.Create<ThornsBanditDirector>();
	}
}
