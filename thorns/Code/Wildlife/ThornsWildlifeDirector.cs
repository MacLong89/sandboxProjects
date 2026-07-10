namespace Sandbox;

/// <summary>
/// Compatibility wrapper — player spatial cache and LOD queries delegate to <see cref="ThornsPopulationDirector"/>.
/// Retained for wildlife AI/state-machine signatures and scene components.
/// </summary>
[Title( "Thorns — Wildlife director" )]
[Category( "Thorns/Wildlife" )]
[Icon( "forest" )]
[Order( 1 )]
public sealed class ThornsWildlifeDirector : Component
{
	public static ThornsWildlifeDirector Instance { get; private set; }

	[Property] public float PlayerRefreshSeconds { get; set; } = 2f;

	protected override void OnEnabled()
	{
		Instance = this;
		SyncRefreshSecondsToPopulationDirector();
	}

	protected override void OnDisabled()
	{
		if ( Instance == this )
			Instance = null;
	}

	protected override void OnStart() => SyncRefreshSecondsToPopulationDirector();

	void SyncRefreshSecondsToPopulationDirector()
	{
		var pop = ThornsPopulationDirector.Instance;
		if ( pop is not null && pop.IsValid() )
			pop.PlayerRefreshSeconds = PlayerRefreshSeconds;
	}

	protected override void OnFixedUpdate()
	{
		if ( !Game.IsPlaying || !Networking.IsHost )
			return;

		if ( ThornsPopulationDirector.Instance is not null && ThornsPopulationDirector.Instance.IsValid() )
			return;

		ThornsPopulationDirector.HostTickFixedStepBudgets();
	}

	public IReadOnlyList<GameObject> HostGetCachedPlayerRoots() =>
		ThornsPopulationDirector.HostGetCachedPlayerRoots();

	public void HostQueryPlayersNearPlanar( Vector3 selfFlat, float radiusWorld, List<GameObject> results ) =>
		ThornsPopulationDirector.HostQueryPlayersNearPlanar( selfFlat, radiusWorld, results );

	public float HostNearestPlayerDistSq( Vector3 wildlifeFlat ) =>
		ThornsPopulationDirector.HostNearestPlayerDistSqForWildlifeLod( wildlifeFlat );
}
