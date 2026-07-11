namespace Terraingen.Multiplayer;

using Terraingen.TerrainGen;
using Terraingen.UI.Menu;

/// <summary>
/// Holds local-owner presentation until terrain is applied, matching s&box join boot patterns.
/// </summary>
[Title( "Thorns World Boot Gate" )]
[Category( "Thorns/Multiplayer" )]
public sealed class ThornsWorldBootGate : Component
{
	const float MinHoldAfterTerrainReadySeconds = 0.5f;
	const float ForceCompleteAfterSeconds = 45f;

	public static bool IsLocalBootComplete { get; private set; } = true;

	/// <summary>True while the local owner should defer camera/HUD activation.</summary>
	public static bool BlocksLocalOwnerPresentation =>
		Game.IsPlaying && (
			( _bootActive && !IsLocalBootComplete )
			|| ThornsMenuJoinFlow.IsProgressVisible
			|| ThornsLocalHostSpawnCoordinator.IsDeferredPending );

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

		// Terrain is already live — do not reopen the boot gate (join clients hit this repeatedly).
		if ( IsLocalBootComplete && ThornsTerrainBootstrap.Instance?.IsWorldApplied == true )
			return;

		_bootActive = true;
		IsLocalBootComplete = false;
		_terrainReady = false;
		_terrainReadyAtRealtime = 0;
		_bootStartedRealtime = Time.Now;
		ThornsJoinFlowDebug.LogMilestone( "BeginLocalBoot" );
		ThornsLoadingScreenUtil.Show( "Loading world…" );
	}

	public static void NotifyWorldApplied( Scene scene )
	{
		if ( !_bootActive || scene is null || !scene.IsValid )
			return;

		if ( IsWorldReadyForPlay( scene ) )
			MarkTerrainReady();
	}

	public static void ResetBootState()
	{
		_bootActive = false;
		IsLocalBootComplete = true;
		_terrainReady = false;
		ThornsLoadingScreenUtil.Dismiss();
	}

	public static void EnsureDriver()
	{
		var scene = Game.ActiveScene;
		if ( scene is null || !scene.IsValid )
			return;

		foreach ( var driver in scene.GetAllComponents<ThornsWorldBootGate>() )
		{
			if ( driver.IsValid() )
				return;
		}

		var root = scene.GetAllComponents<ThornsNetworkGameManager>().FirstOrDefault()?.GameObject;
		if ( !root.IsValid() )
			root = scene.GetAllComponents<ThornsTerrainBootstrap>().FirstOrDefault()?.GameObject;

		if ( !root.IsValid() )
		{
			root = new GameObject( true, "Thorns World Boot Gate" );
			root.SetParent( null );
		}

		if ( !root.Components.Get<ThornsWorldBootGate>().IsValid() )
			root.Components.Create<ThornsWorldBootGate>();
	}

	protected override void OnStart() => EnsureDriver();

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
		Log.Info( $"[Thorns Terrain] World boot complete ({reason})." );
		ThornsJoinFlowDebug.LogMilestone( $"CompleteBoot ({reason}) — {ThornsJoinFlowDebug.DescribeEnterGates( compact: true )}" );
		ThornsSessionEnterController.NotifyWorldBootComplete();
	}

	static bool IsWorldReadyForPlay( Scene scene )
	{
		var bootstrap = scene.GetAllComponents<ThornsTerrainBootstrap>().FirstOrDefault();
		return bootstrap?.IsWorldApplied == true;
	}
}
