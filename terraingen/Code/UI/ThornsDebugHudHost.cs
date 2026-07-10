namespace Terraingen.UI;

using System.Linq;
using Terraingen.UI.Core;
using Terraingen.Core;
using Terraingen.Clutter;
using Terraingen.Foliage;
using Terraingen.Minerals;
using Terraingen.World.Environment;

/// <summary>
/// Screen HUD for world coordinates and foliage debug status.
/// </summary>
[Title( "Thorns Debug HUD" )]
[Category( "Terrain" )]
[Icon( "info" )]
public sealed class ThornsDebugHudHost : Component
{
	[Property] public bool ShowHud { get; set; }

	[Property, Range( 0.05f, 1f ), Title( "HUD coord refresh (seconds)" )]
	public float CoordinateRefreshSeconds { get; set; } = 0.12f;

	[Property] public string PlayerObjectName { get; set; } = "Terrain Explorer";

	GameObject _trackedObject;
	CameraComponent _mainCamera;
	ThornsFoliageFoundation _foliage;
	ThornsClutterFoundation _clutter;
	ClientGrassRenderer _clientGrass;
	ThornsMineralFoundation _minerals;
	TimeUntil _nextSlowRefresh;
	TimeUntil _nextFoliageTextRefresh;
	TimeUntil _nextCoordinateRefresh;
	bool _runtimeActive;

	protected override void OnStart()
	{
		if ( !Game.IsPlaying )
			return;

		_runtimeActive = ShowHud || ThornsPerfSettings.DebugHud;
		if ( !_runtimeActive )
			return;

		if ( GameplayUiOwnsScreen() )
		{
			Log.Info( "[Thorns UI] Debug HUD text-only — gameplay menu host owns ScreenPanel." );
			RefreshSlowReferences();
			return;
		}

		RefreshSlowReferences();
		Components.Create<ScreenPanel>();
		Components.Create<ThornsDebugHud>();
	}

	protected override void OnUpdate()
	{
		if ( !_runtimeActive )
			return;

		try
		{
			TickDebugHud();
		}
		catch ( Exception e )
		{
			Log.Warning( e, "[Thorns UI] Debug HUD update failed." );
		}
	}

	void TickDebugHud()
	{
		var pos = ResolveTrackedPosition();
		ThornsDebugState.WorldPosition = pos;

		if ( !_nextCoordinateRefresh )
		{
			_nextCoordinateRefresh = CoordinateRefreshSeconds;
			ThornsDebugState.PositionLabel = $"X: {pos.x:F1}   Y: {pos.y:F1}   Z: {pos.z:F1}";
		}

		if ( !_nextFoliageTextRefresh )
			return;

		_nextFoliageTextRefresh = 0.25f;

		ThornsDebugState.SkyLine =
			ThornsEnvironmentDebug.ShowDebugHud && ThornsTimeOfDaySystem.TryGet( Scene, out var envTime ) && envTime.IsValid()
				? ThornsEnvironmentDebug.BuildHudLine( envTime.CurrentState )
				: "";

		if ( HasComponent( _foliage ) )
		{
			ThornsDebugState.FoliageLine = _foliage.GetHudSummary();
			var extra = HasComponent( _minerals ) ? SafeDebugSummary( _minerals ) : null;
			if ( HasComponent( _clientGrass ) )
				extra = extra is null ? SafeDebugSummary( _clientGrass ) : $"{extra} | {SafeDebugSummary( _clientGrass )}";
			ThornsDebugState.DetailLine = extra is not null
				? $"{_foliage.GetHudDetail()} | {extra}"
				: HasComponent( _clutter )
					? $"{_foliage.GetHudDetail()} | {SafeDebugSummary( _clutter )}"
					: _foliage.GetHudDetail();
			ThornsDebugState.SubsystemLine = SafeSubsystemSummary();
		}
		else
		{
			ThornsDebugState.FoliageLine = "Foliage: no ThornsFoliageFoundation in scene";
			ThornsDebugState.DetailLine = HasComponent( _clientGrass )
				? SafeDebugSummary( _clientGrass )
				: HasComponent( _clutter ) ? SafeDebugSummary( _clutter ) : "";
			ThornsDebugState.SubsystemLine = SafeSubsystemSummary();
		}
	}

	static bool GameplayUiOwnsScreen() =>
		Game.ActiveScene?.GetAllComponents<ThornsGameplayUiHost>().Any( h => h.IsValid ) == true;

	static string SafeDebugSummary( ClientGrassRenderer grass ) =>
		grass is not null && grass.IsValid() ? grass.GetDebugSummary() : "";

	static string SafeDebugSummary( ThornsClutterFoundation clutter ) =>
		clutter is not null && clutter.IsValid() ? clutter.GetDebugSummary() : "";

	static string SafeDebugSummary( ThornsMineralFoundation minerals ) =>
		minerals is not null && minerals.IsValid() ? minerals.GetDebugSummary() : "";

	static string SafeSubsystemSummary()
	{
		try
		{
			return ThornsSubsystemDiagnostics.BuildSummary( Game.ActiveScene );
		}
		catch ( Exception e )
		{
			Log.Warning( e, "[Thorns UI] Subsystem diagnostics failed." );
			return "";
		}
	}

	static bool HasComponent( Component component ) => component is not null && component.IsValid();

	Vector3 ResolveTrackedPosition()
	{
		if ( _nextSlowRefresh || !HasComponent( _foliage ) || !HasComponent( _clutter ) )
			RefreshSlowReferences();

		if ( IsValidObject( _trackedObject ) )
			return _trackedObject.WorldPosition;

		return ThornsSceneObserver.Resolve( Scene, ref _trackedObject, ref _mainCamera, ref _nextSlowRefresh );
	}

	static bool IsValidObject( GameObject obj ) => obj is not null && obj.IsValid();

	void RefreshSlowReferences()
	{
		_nextSlowRefresh = 1f;
		ThornsSceneObserver.Refresh( Scene, ref _trackedObject, ref _mainCamera, ref _nextSlowRefresh );

		if ( !IsValidObject( _trackedObject ) )
		{
			foreach ( var player in Scene.GetAllObjects( true ) )
			{
				if ( !player.Name.Equals( PlayerObjectName, StringComparison.OrdinalIgnoreCase ) )
					continue;

				if ( player.IsValid() )
				{
					_trackedObject = player;
					break;
				}
			}
		}

		if ( !HasComponent( _foliage ) )
			_foliage = Scene.GetAllComponents<ThornsFoliageFoundation>().FirstOrDefault();

		if ( !HasComponent( _clutter ) )
			_clutter = Scene.GetAllComponents<ThornsClutterFoundation>().FirstOrDefault();

		if ( !HasComponent( _clientGrass ) )
			_clientGrass = Scene.GetAllComponents<ClientGrassRenderer>().FirstOrDefault();

		if ( !HasComponent( _minerals ) )
			_minerals = Scene.GetAllComponents<ThornsMineralFoundation>().FirstOrDefault();
	}
}
