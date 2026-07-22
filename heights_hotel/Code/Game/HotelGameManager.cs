using System;
using System.Linq;
using Sandbox;

namespace HeightsHotel;

/// <summary>
/// Boots the hotel sim, drives ticks, autosaves, and creates the HUD at runtime.
/// HUD is created in code (like Deep/Offshore) so play does not depend on scene-serialized PanelComponents.
/// </summary>
public sealed class HotelGameManager : Component
{
	public static HotelGameManager Instance { get; private set; }

	public HotelSimulation Sim { get; private set; }
	public SaveService Saves { get; } = new();

	float _autosaveTimer;
	const float AutosaveInterval = 30f;
	bool _hudBuilt;

	protected override void OnAwake()
	{
		Instance = this;
		Log.Info( "[HeightsHotel] HotelGameManager OnAwake" );
		var (state, warning) = Saves.LoadOrNew();
		Sim = new HotelSimulation( state );
		Sim.ApplyOfflineProgress();
		if ( !string.IsNullOrEmpty( warning ) )
			Sim.ShowStatus( warning );
		else if ( !string.IsNullOrEmpty( Sim.State.StatusMessage ) )
			Sim.ShowStatus( Sim.State.StatusMessage );
		Log.Info( $"[HeightsHotel] Sim ready. rooms={Sim.RoomCount} cash={Sim.State.CashCents}" );
	}

	protected override void OnStart()
	{
		EnsureHud();
		EnsureCamera();
		Mouse.Visibility = MouseVisibility.Visible;
		Log.Info( "[HeightsHotel] HotelGameManager OnStart — mouse unlocked" );
	}

	protected override void OnDestroy()
	{
		if ( Instance == this )
			Instance = null;
		if ( Sim is not null )
			TrySave();
	}

	protected override void OnUpdate()
	{
		// Always show the cursor — this is a mouse-driven management game.
		Mouse.Visibility = MouseVisibility.Visible;

		if ( !_hudBuilt )
			EnsureHud();

		EnsureCamera();
		Sim?.Advance( Time.Delta );
		_autosaveTimer += Time.Delta;
		if ( _autosaveTimer >= AutosaveInterval )
		{
			_autosaveTimer = 0;
			TrySave();
		}
	}

	void EnsureHud()
	{
		if ( _hudBuilt )
			return;

		try
		{
			var existingHud = Scene.GetAllComponents<HotelGame>().FirstOrDefault();
			if ( existingHud is not null && existingHud.IsValid() )
			{
				var host = existingHud.GameObject;
				var screen = host.Components.Get<ScreenPanel>();
				if ( screen is null )
				{
					screen = host.Components.Create<ScreenPanel>();
					Log.Info( "[HeightsHotel] Added ScreenPanel beside existing HotelGame" );
				}

				ConfigureScreenPanel( screen );
				BindCamera( screen );
				_hudBuilt = true;
				Log.Info( "[HeightsHotel] Using existing HotelGame HUD" );
				return;
			}

			var hudGo = new GameObject( true, "HeightsHotelHUD" );
			var screenPanel = hudGo.Components.Create<ScreenPanel>();
			ConfigureScreenPanel( screenPanel );
			hudGo.Components.Create<HotelGame>();
			BindCamera( screenPanel );
			_hudBuilt = true;
			Log.Info( $"[HeightsHotel] Created runtime HUD. cam={screenPanel.TargetCamera.IsValid()}" );
		}
		catch ( Exception e )
		{
			Log.Error( $"[HeightsHotel] Failed to create HUD: {e}" );
		}
	}

	void ConfigureScreenPanel( ScreenPanel screen )
	{
		// The editor game viewport is often much shorter than 1080p. Automatic
		// screen scaling was shrinking 13–18px text into unreadable pixels.
		// Render in actual screen pixels so cards and labels retain their size.
		screen.AutoScreenScale = false;
		screen.Scale = 1f;
		screen.Opacity = 1f;
		screen.ZIndex = 100;
		screen.Enabled = true;
	}

	void EnsureCamera()
	{
		var cam = Scene.GetAllComponents<CameraComponent>().FirstOrDefault( c => c.IsMainCamera );
		if ( cam is null || !cam.IsValid() )
		{
			var camGo = new GameObject( true, "HeightsHotelCamera" );
			cam = camGo.Components.Create<CameraComponent>();
			cam.IsMainCamera = true;
			cam.Orthographic = true;
			cam.OrthographicHeight = 200f;
			cam.BackgroundColor = new Color( 0.07f, 0.11f, 0.18f );
			cam.ZNear = 1f;
			cam.ZFar = 10000f;
			Log.Info( "[HeightsHotel] Created fallback main camera" );
		}

		foreach ( var screen in Scene.GetAllComponents<ScreenPanel>() )
		{
			if ( !screen.IsValid() )
				continue;

			// Also enforce this after hot reloads where the existing panel survives.
			screen.AutoScreenScale = false;
			if ( screen.TargetCamera != cam )
				screen.TargetCamera = cam;
		}
	}

	void BindCamera( ScreenPanel screen )
	{
		var cam = Scene.GetAllComponents<CameraComponent>().FirstOrDefault( c => c.IsMainCamera );
		if ( cam.IsValid() )
			screen.TargetCamera = cam;
	}

	public void TrySave()
	{
		try
		{
			if ( Sim?.State is not null )
				Saves.Save( Sim.State );
		}
		catch ( Exception e )
		{
			Log.Warning( $"Autosave failed: {e.Message}" );
		}
	}

	public SimCommandResult Build( RoomType type, int x, int y )
	{
		var r = Sim.TryBuild( type, x, y );
		if ( r.Ok ) TrySave();
		return r;
	}

	public SimCommandResult Upgrade( int x, int y )
	{
		var r = Sim.TryUpgrade( x, y );
		if ( r.Ok ) TrySave();
		return r;
	}

	public SimCommandResult Hire( StaffRole role )
	{
		var r = Sim.TryHire( role );
		if ( r.Ok ) TrySave();
		return r;
	}

	public SimCommandResult Fire( int id )
	{
		var r = Sim.TryFire( id );
		if ( r.Ok ) TrySave();
		return r;
	}

	public SimCommandResult Assign( int employeeId, int x, int y )
	{
		var r = Sim.TryAssignEmployee( employeeId, x, y );
		if ( r.Ok ) TrySave();
		return r;
	}

	public SimCommandResult AutoAssign(
		int x,
		int y,
		out bool noAvailableStaff,
		StaffRole? requiredRole = null )
	{
		var r = Sim.TryAutoAssignEmployee( x, y, out noAvailableStaff, requiredRole );
		if ( r.Ok ) TrySave();
		return r;
	}

	public SimCommandResult Unassign( int employeeId )
	{
		var r = Sim.TryUnassignEmployee( employeeId );
		if ( r.Ok ) TrySave();
		return r;
	}

	public SimCommandResult Demolish( int x, int y )
	{
		var r = Sim.TryDemolish( x, y );
		if ( r.Ok ) TrySave();
		return r;
	}

	public SimCommandResult DispatchService( StaffRole role, int x, int y )
	{
		var r = Sim.TryDispatchService( role, x, y );
		if ( r.Ok ) TrySave();
		return r;
	}

	public SimCommandResult ClaimGoal( string goalId )
	{
		var r = Sim.TryClaimGoal( goalId );
		if ( r.Ok ) TrySave();
		return r;
	}

	public void StartNewGame()
	{
		Sim = new HotelSimulation( HotelSimulation.CreateNewGame( Random.Shared.Next() ) );
		TrySave();
	}
}
