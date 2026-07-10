namespace Sandbox;

/// <summary>
/// Canonical population authority — player spatial cache, registries, spawn budgets, LOS/perception cadence.
/// Wildlife/bandit directors are compatibility wrappers; registries are internal implementation details.
/// </summary>
[Title( "Thorns — Population Director" )]
[Category( "Thorns/Population" )]
[Icon( "hub" )]
[Order( 0 )]
public sealed partial class ThornsPopulationDirector : Component
{
	public static ThornsPopulationDirector Instance { get; private set; }

	[Property] public float PlayerRefreshSeconds { get; set; } = 2f;

	readonly List<GameObject> _spatialScratch = new();

	protected override void OnEnabled()
	{
		Instance = this;
	}

	protected override void OnDisabled()
	{
		if ( Instance == this )
			Instance = null;
	}

	/// <summary>Canonical owner for shared fixed-step population budgets (LOS, peer spatial, metrics).</summary>
	protected override void OnFixedUpdate()
	{
		HostTickFixedStepBudgets();
	}

	/// <summary>Host-only: resets LOS budget, rebuilds wildlife peer spatial index, rolls perception metrics window.</summary>
	public static void HostTickFixedStepBudgets()
	{
		if ( !Game.IsPlaying || !Networking.IsHost )
			return;

		ThornsWildlifeLosBudget.HostResetForNewFixed();
		ThornsWildlifePopulation.HostRebuildPeerSpatialIndexForFixedStep();
		ThornsAiPerceptionMetrics.TickWindowIfNeeded();
	}

	static bool HostIsAuthoritativeForPlayerCache() =>
		!Networking.IsActive || Networking.IsHost;

	// ── Population registry access (canonical registration layer) ──

	public static void HostRegisterWildlife( ThornsWildlifeBrain brain ) =>
		ThornsWildlifePopulation.Register( brain );

	public static void HostUnregisterWildlife( ThornsWildlifeBrain brain ) =>
		ThornsWildlifePopulation.Unregister( brain );

	public static void HostRegisterBandit( ThornsBanditBrain brain ) =>
		ThornsBanditPopulation.HostRegister( brain );

	public static void HostUnregisterBandit( ThornsBanditBrain brain ) =>
		ThornsBanditPopulation.HostUnregister( brain );

	public static int HostWildlifeGlobalCount => ThornsWildlifePopulation.HostGlobalCount;

	public static IReadOnlyList<ThornsWildlifeBrain> HostWildlifeBrainsReadOnly =>
		ThornsWildlifePopulation.HostBrainsReadOnly;

	public static IReadOnlyList<ThornsBanditBrain> HostBanditBrainsReadOnly =>
		ThornsBanditPopulation.HostBrainsReadOnly;

	public static int HostBanditGlobalCount => HostBanditBrainsReadOnly.Count;

	internal static List<ThornsWildlifeBrain> HostBorrowWildlifePeerQueryScratch() =>
		ThornsWildlifePopulation.HostBorrowPeerQueryScratch();

	public static int HostGetPopulationCount( ThornsPopulationKind kind ) =>
		kind switch
		{
			ThornsPopulationKind.Wildlife => HostWildlifeGlobalCount,
			ThornsPopulationKind.BanditWanderer => HostBanditGlobalCount,
			ThornsPopulationKind.FutureNpc => ThornsPopulationFutureRegistry.HostGuildNpcCount
			                                   + ThornsPopulationFutureRegistry.HostTraderCount,
			ThornsPopulationKind.EventNpc => ThornsPopulationFutureRegistry.HostBossCount,
			_ => 0,
		};

	public static ThornsPopulationBudgetSnapshot HostGetPopulationBudget( ThornsPopulationKind kind ) =>
		new()
		{
			Kind = kind,
			LiveCount = HostGetPopulationCount( kind ),
			GlobalCap = -1,
		};

	// ── Future population channels (stubs — see ThornsPopulationFutureRegistry) ──

	public static void HostRegisterFutureNpc( Component npc )
	{
		if ( npc is IThornsGuildNpcPopulationEntity guild )
			ThornsPopulationFutureRegistry.HostRegisterGuildNpc( guild );
		else if ( npc is IThornsTraderPopulationEntity trader )
			ThornsPopulationFutureRegistry.HostRegisterTrader( trader );
	}

	public static void HostUnregisterFutureNpc( Component npc )
	{
		if ( npc is IThornsGuildNpcPopulationEntity guild )
			ThornsPopulationFutureRegistry.HostUnregisterGuildNpc( guild );
		else if ( npc is IThornsTraderPopulationEntity trader )
			ThornsPopulationFutureRegistry.HostUnregisterTrader( trader );
	}

	public static void HostRegisterEventNpc( Component npc )
	{
		if ( npc is IThornsBossPopulationEntity boss )
			ThornsPopulationFutureRegistry.HostRegisterBoss( boss );
	}

	public static void HostUnregisterEventNpc( Component npc )
	{
		if ( npc is IThornsBossPopulationEntity boss )
			ThornsPopulationFutureRegistry.HostUnregisterBoss( boss );
	}

	public static int HostCountWildlifeNear( Vector3 world, float radius ) =>
		ThornsWildlifePopulation.HostCountNear( world, radius );

	public static void HostQueryWildlifePeersNearPlanar(
		Vector3 flat,
		float radiusWorld,
		List<ThornsWildlifeBrain> results,
		ThornsWildlifeBrain excludeSelf = null ) =>
		ThornsWildlifePopulation.HostQueryPeersNearPlanar( flat, radiusWorld, results, excludeSelf );

	// ── Spawn budget API ──

	public static bool HostTryRequestSpawnSlot(
		ThornsPopulationKind kind,
		in ThornsPopulationSpawnRequest request,
		out string denyReason )
	{
		denyReason = null;

		switch ( kind )
		{
			case ThornsPopulationKind.Wildlife:
				return HostTryRequestWildlifeSpawnSlot( request, out denyReason );

			case ThornsPopulationKind.BanditWanderer:
				return HostTryRequestBanditWandererSpawnSlot( request, out denyReason );

			case ThornsPopulationKind.FutureNpc:
			case ThornsPopulationKind.EventNpc:
				denyReason = $"{kind} spawn budget not wired yet";
				return false;

			default:
				denyReason = $"unknown population kind {kind}";
				return false;
		}
	}

	public static void HostReleaseSpawnSlot( ThornsPopulationKind kind ) => _ = kind;

	static bool HostTryRequestWildlifeSpawnSlot( in ThornsPopulationSpawnRequest request, out string denyReason )
	{
		denyReason = null;

		if ( HostWildlifeGlobalCount >= request.GlobalCap )
		{
			denyReason = $"global cap wildlife={HostWildlifeGlobalCount}/{request.GlobalCap}";
			return false;
		}

		if ( request.PerPlayerNearbyCap > 0 && request.PerPlayerNearbyRadius > 0f &&
		     request.AnchorWorldPosition is { } anchor )
		{
			var near = HostCountWildlifeNear( anchor, request.PerPlayerNearbyRadius );
			if ( near >= request.PerPlayerNearbyCap )
			{
				denyReason =
					$"per-player nearby cap near={near}/{request.PerPlayerNearbyCap} radius={request.PerPlayerNearbyRadius}";
				return false;
			}
		}

		return true;
	}

	static bool HostTryRequestBanditWandererSpawnSlot( in ThornsPopulationSpawnRequest request, out string denyReason )
	{
		denyReason = null;
		var scene = request.Scene;
		if ( scene is null || !scene.IsValid() )
		{
			denyReason = "bandit wanderer spawn requires valid scene";
			return false;
		}

		var count = HostCountBanditWanderers( scene );
		if ( count >= request.GlobalCap )
		{
			denyReason = $"global wanderer cap bandits={count}/{request.GlobalCap}";
			return false;
		}

		return true;
	}

	public static int HostCountBanditWanderers( Scene scene )
	{
		if ( scene is null || !scene.IsValid() )
			return 0;

		var n = 0;
		foreach ( var brain in scene.GetAllComponents<ThornsBanditBrain>() )
		{
			if ( brain.IsValid() &&
			     string.Equals( brain.GameObject.Name, "ThornsWandererBandit", StringComparison.Ordinal ) )
				n++;
		}

		return n;
	}

	// ── LOS / perception budget passthrough ──

	public static int HostLosTracesUsedThisFixed => ThornsWildlifeLosBudget.HostTracesUsedThisFixed;

	public static int HostLosFixedStepSerial => ThornsWildlifeLosBudget.HostFixedStepSerial;

	public static bool HostTryConsumeLosTrace() => ThornsWildlifeLosBudget.TryConsumeTrace();
}
