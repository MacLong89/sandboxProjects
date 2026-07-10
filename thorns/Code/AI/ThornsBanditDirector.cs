namespace Sandbox;

/// <summary>
/// Compatibility wrapper — player spatial queries delegate to <see cref="ThornsPopulationDirector"/>.
/// Retained for bandit AI/state-machine signatures and scene bootstrap.
/// </summary>
[Title( "Thorns — Bandit Director" )]
[Category( "Thorns/AI" )]
[Icon( "groups" )]
public sealed class ThornsBanditDirector : Component
{
	public static ThornsBanditDirector Instance { get; private set; }

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

		var wildlifeDir = ThornsWildlifeDirector.Instance;
		if ( wildlifeDir is not null && wildlifeDir.IsValid() )
			return;

		ThornsPopulationDirector.HostTickFixedStepBudgets();
	}

	public IReadOnlyList<GameObject> HostGetCachedPlayerRoots() =>
		ThornsPopulationDirector.HostGetCachedPlayerRoots();

	public void HostQueryPlayersNearPlanar( Vector3 selfFlat, float radiusWorld, List<GameObject> results ) =>
		ThornsPopulationDirector.HostQueryPlayersNearPlanar( selfFlat, radiusWorld, results );

	public float HostNearestAlivePlayerDistSqWithin( Vector3 selfFlat, float maxDistanceWorld ) =>
		ThornsPopulationDirector.HostNearestAlivePlayerDistSqWithin( selfFlat, maxDistanceWorld );
}
