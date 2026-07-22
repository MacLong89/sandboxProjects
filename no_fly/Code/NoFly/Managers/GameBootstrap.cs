namespace NoFly;

/// <summary>
/// Scene bootstrap — creates game systems, HUD, and camera if missing.
/// </summary>
public sealed class GameBootstrap : Component
{
	protected override void OnAwake()
	{
		if ( !Components.Get<NoFlyGame>().IsValid() )
			Components.Create<NoFlyGame>();

		EnsureHud();
		EnsureCamera();
	}

	void EnsureHud()
	{
		var existing = Scene.GetAllComponents<ScreenPanel>().FirstOrDefault();
		GameObject hudRoot;
		if ( existing.IsValid() )
		{
			hudRoot = existing.GameObject;
		}
		else
		{
			hudRoot = new GameObject( true, "NOFLY_HUD" );
			hudRoot.SetParent( GameObject );
			hudRoot.Components.Create<ScreenPanel>();
		}

		void AddUi<T>() where T : PanelComponent, new()
		{
			if ( !hudRoot.Components.Get<T>().IsValid() )
				hudRoot.Components.Create<T>();
		}

		AddUi<NoFlyHud>();
		AddUi<ForgeryPanel>();
		AddUi<LuggageHidePanel>();
		AddUi<DocumentInspectPanel>();
		AddUi<ScannerPanel>();
		AddUi<ShopPanel>();
	}

	void EnsureCamera()
	{
		if ( Scene.GetAllComponents<PlayerCamera>().Any() ) return;
		var cam = new GameObject( true, "NOFLY_Camera" );
		cam.SetParent( GameObject );
		cam.Components.Create<PlayerCamera>();
	}
}

public sealed class AudioDirector : Component
{
	[Sync( SyncFlags.FromHost )] public string LastCue { get; set; }

	RoundState _lastState;

	protected override void OnUpdate()
	{
		var game = NoFlyGame.Instance;
		if ( game is null ) return;
		if ( game.State == _lastState ) return;
		_lastState = game.State;

		LastCue = game.State switch
		{
			RoundState.AirportOpen => "airport_open",
			RoundState.Boarding => "boarding",
			RoundState.Chase => "alarm",
			RoundState.Results => game.Winner == WinSide.Smuggler ? "smuggler_win" : "tsa_win",
			_ => LastCue
		};

		// Placeholder: engine Sound.Play if resources available later
		Log.Info( $"[NO FLY AUDIO] {LastCue}" );
	}
}
