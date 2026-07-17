namespace Fauna2.UI;

public enum UiPage
{
	None,
	Build,
	Market,
	Codex,
	Progression,
	Zoo,
	Stats,
}

public sealed class Toast
{
	public string Message { get; init; }
	public string Icon { get; init; }
	public TimeSince Age { get; init; }
}

public sealed class FloatPopup
{
	public string Text { get; init; }
	public string Style { get; init; }
	public TimeSince Age { get; init; }
}

public sealed class Celebration
{
	public string Title { get; init; }
	public string Subtitle { get; init; }
	public string Icon { get; init; }
	public TimeSince Age { get; init; }
}

/// <summary>
/// Local UI state shared between panels: which page is open, toast
/// notifications, and whether the pointer is over UI (so world clicks and
/// zoom don't fire through panels).
/// </summary>
public static class UiState
{
	public static UiPage ActivePage { get; private set; } = UiPage.None;

	/// <summary>Market panel tab: 0 adopt, 1 backpack, 2 owned, 3 catch tools.</summary>
	public static int MarketTab { get; private set; }

	/// <summary>Pre-filter adopt tab to a habitat biome (consumed when market opens).</summary>
	public static Biome? MarketBiomeFilter { get; private set; }
	public static HabitatSizeTier? MarketSizeFilter { get; private set; }

	public static BuildCategory? BuildCategoryOverride { get; private set; }

	public static bool PointerOverUI { get; set; }
	public static string PointerBlocker { get; set; } = "";
	public static bool BuildDebug { get; set; }
	public static bool DebugVisible { get; private set; }
	public static bool CatchMinigameOpen { get; private set; }
	public static bool ControlsHintVisible { get; private set; }
	public static bool WelcomeIntroVisible { get; private set; }
	private static bool _welcomeIntroForNewZoo;
	public static bool StartupToastsSuppressed { get; private set; }

	/// <summary>True while the zoo session is booting — hides gameplay HUD and shows the loading overlay.</summary>
	public static bool SessionLoading { get; private set; }

	/// <summary>Gameplay chrome may render (top bar, toolbar, inspect panels, etc.).</summary>
	public static bool GameplayHudVisible =>
		(GameManager.Instance?.GameStarted ?? false) && !SessionLoading;

	public static int Revision { get; private set; }
	public static bool XpPulse { get; private set; }
	private static TimeUntil _xpPulseEnd;

	public static string SelectedAnimalId { get; private set; }

	public static AnimalComponent SelectedAnimal =>
		string.IsNullOrEmpty( SelectedAnimalId ) ? null : AnimalRegistry.Find( SelectedAnimalId );

	private static readonly List<Toast> _toasts = new();
	public static IReadOnlyList<Toast> Toasts => _toasts;

	private static readonly List<FloatPopup> _floatPopups = new();
	public static IReadOnlyList<FloatPopup> FloatPopups => _floatPopups;

	private static Celebration _celebration;

	public static Celebration ActiveCelebration =>
		_celebration is not null && _celebration.Age < 3.5f ? _celebration : null;

	public static bool IsPageOpen => ActivePage != UiPage.None;

	public static bool BackpackOpen { get; private set; }

	public static bool HasMenuOpen => IsPageOpen || BackpackOpen;

	public static bool HasInspectOpen =>
		!string.IsNullOrEmpty( SelectedAnimalId )
		|| !string.IsNullOrEmpty( SelectedHabitatId )
		|| !string.IsNullOrEmpty( SelectedObstacleCellKey );

	/// <summary>Inspect panels or debug — not toolbar page menus (Build, Market, etc.).</summary>
	public static bool HasBlockingModal => DebugVisible || CatchMinigameOpen || WelcomeIntroVisible;

	// AUDIT FIX B11: Single gate for walk / zoom / world interact.
	// Previously ZooPlayerController only checked GameStarted, so players walked
	// during welcome intro, catch overlay, and session loading. Revert: delete
	// this property and remove CanWorldInput checks from controllers/interact.
	public static bool CanWorldInput =>
		GameManager.Instance?.GameStarted == true
		&& !SessionLoading
		&& !HasBlockingModal;

	/// <summary>Hide toolbar, objectives, alerts, hints, and toasts under the debug overlay or session boot.</summary>
	public static bool HideSecondaryHud => DebugVisible || SessionLoading || WelcomeIntroVisible;

	/// <summary>Hide the top stats bar under the debug overlay or session boot.</summary>
	public static bool HideTopBar => DebugVisible || SessionLoading;

	/// <summary>Dim the world behind modal overlays.</summary>
	public static bool ShowModalBackdrop => DebugVisible;

	public static void DismissFromBackdrop()
	{
		if ( !DebugVisible )
			return;

		DebugVisible = false;
		Revision++;
	}

	public static void ToggleDebug()
	{
		if ( DebugVisible )
		{
			DebugVisible = false;
			Revision++;
			return;
		}

		ClearPageState();
		ClearInspectState();
		BackpackOpen = false;
		DebugVisible = true;
		Revision++;
		BuildController.Instance?.CancelMode();
	}

	/// <summary>Bump HUD panels that show sequential goal progress.</summary>
	public static void NotifyGoalsChanged() => Revision++;

	public static void SelectAnimal( string animalId )
	{
		if ( SelectedAnimalId == animalId ) return;

		ClearPageState();
		DebugVisible = false;
		SelectedAnimalId = animalId;
		SelectedHabitatId = null;
		SelectedObstacleCellKey = null;
		Revision++;
		BuildController.Instance?.CancelMode();
	}

	public static void ClearAnimalSelection()
	{
		if ( string.IsNullOrEmpty( SelectedAnimalId ) ) return;
		SelectedAnimalId = null;
		Revision++;
	}

	public static string SelectedHabitatId { get; private set; }

	public static HabitatComponent SelectedHabitat =>
		string.IsNullOrEmpty( SelectedHabitatId ) ? null : HabitatRegistry.Find( SelectedHabitatId );

	public static void SelectHabitat( string habitatId )
	{
		if ( SelectedHabitatId == habitatId ) return;

		ClearPageState();
		DebugVisible = false;
		SelectedHabitatId = habitatId;
		SelectedAnimalId = null;
		SelectedObstacleCellKey = null;
		Revision++;
		BuildController.Instance?.CancelMode();
	}

	public static void ClearHabitatSelection()
	{
		if ( string.IsNullOrEmpty( SelectedHabitatId ) ) return;
		SelectedHabitatId = null;
		Revision++;
	}

	public static void ClearWorldSelection()
	{
		var changed = false;
		if ( !string.IsNullOrEmpty( SelectedAnimalId ) )
		{
			SelectedAnimalId = null;
			changed = true;
		}

		if ( !string.IsNullOrEmpty( SelectedHabitatId ) )
		{
			SelectedHabitatId = null;
			changed = true;
		}

		if ( !string.IsNullOrEmpty( SelectedObstacleCellKey ) )
		{
			SelectedObstacleCellKey = null;
			changed = true;
		}

		if ( changed )
			Revision++;
	}

	public static string SelectedObstacleCellKey { get; private set; }

	public static TerrainObstacleComponent SelectedObstacle =>
		string.IsNullOrEmpty( SelectedObstacleCellKey ) ? null : TerrainObstacleRegistry.Find( SelectedObstacleCellKey );

	public static bool ObstacleClearActive { get; private set; }

	public static float ObstacleClearPercent =>
		ObstacleClearActive
			? MathF.Min( 1f, _obstacleClearProgress / GameConstants.ObstacleClearDuration )
			: 0f;

	/// <summary>Obstacle being cleared — survives closing inspect panels / opening menus.</summary>
	public static TerrainObstacleComponent ClearingObstacle =>
		string.IsNullOrEmpty( _obstacleClearCellKey ) ? null : TerrainObstacleRegistry.Find( _obstacleClearCellKey );

	private static string _obstacleClearCellKey;
	private static float _obstacleClearProgress;
	private static int _obstacleClearSoundsPlayed;
	private static TimeUntil _obstacleClearUiTick;
	private static TimeUntil _obstacleClearRetryTick;
	private const float ObstacleClearCompleteGrace = 1.75f;

	public static void SelectObstacle( string cellKey )
	{
		if ( SelectedObstacleCellKey == cellKey ) return;

		ClearPageState();
		DebugVisible = false;
		SelectedAnimalId = null;
		SelectedHabitatId = null;
		SelectedObstacleCellKey = cellKey;
		Revision++;
		BuildController.Instance?.CancelMode();
	}

	public static void ClearObstacleSelection()
	{
		if ( string.IsNullOrEmpty( SelectedObstacleCellKey ) )
			return;

		SelectedObstacleCellKey = null;
		Revision++;
	}

	public static void BeginObstacleClear()
	{
		if ( ObstacleClearActive || string.IsNullOrEmpty( SelectedObstacleCellKey ) )
			return;

		var obstacle = SelectedObstacle;
		if ( obstacle is null || !obstacle.IsValid() )
			return;

		// Gate locally — host also checks, but previously the UI always ran to 100%
		// then host soft-failed silently when click-selected trees were outside range.
		if ( !IsLocalPlayerNearObstacle( obstacle, GameConstants.ObstacleClearRadius ) )
		{
			ZooState.Instance?.Notify( "Move closer to clear this.", "warning" );
			return;
		}

		_obstacleClearCellKey = SelectedObstacleCellKey;
		ObstacleClearActive = true;
		_obstacleClearProgress = 0f;
		_obstacleClearSoundsPlayed = 0;
		_obstacleClearUiTick = 0f;
		_obstacleClearRetryTick = 0f;
		Revision++;

		// AUDIT FIX B7: Arm host clear session when the local UI timer starts.
		// RequestClear alone used to complete with no host duration/proximity check.
		TerrainObstacleSystem.Instance?.RequestBeginClear( _obstacleClearCellKey );
	}

	public static void CancelObstacleClear()
	{
		if ( !ObstacleClearActive && _obstacleClearProgress <= 0f )
			return;

		ObstacleClearActive = false;
		_obstacleClearCellKey = null;
		_obstacleClearProgress = 0f;
		_obstacleClearSoundsPlayed = 0;
		Revision++;
	}

	private static void TryPlayObstacleClearSound( TerrainObstacleType type )
	{
		if ( _obstacleClearSoundsPlayed >= 3 )
			return;

		ZooSoundEffects.PlayObstacleClear( type );
		_obstacleClearSoundsPlayed++;
	}

	private static void TickObstacleClearSounds( TerrainObstacleType type )
	{
		var duration = GameConstants.ObstacleClearDuration;
		while ( _obstacleClearSoundsPlayed < 3
			&& _obstacleClearProgress >= duration * _obstacleClearSoundsPlayed / 3f )
		{
			TryPlayObstacleClearSound( type );
		}
	}

	private static bool IsLocalPlayerNearObstacle( TerrainObstacleComponent obstacle, float radius )
	{
		if ( obstacle is null || !obstacle.IsValid() )
			return false;

		var local = PlayerState.Local;
		if ( local is null || !local.IsValid() )
			return false;

		var feet = local.FeetPosition;
		var dx = feet.x - obstacle.WorldPosition.x;
		var dy = feet.y - obstacle.WorldPosition.y;
		return (dx * dx + dy * dy) <= radius * radius;
	}

	private static void FinishObstacleClearUi( string cellKey )
	{
		CancelObstacleClear();
		if ( SelectedObstacleCellKey == cellKey )
			SelectedObstacleCellKey = null;
		Revision++;
	}

	private static void TickObstacleClear()
	{
		if ( !ObstacleClearActive )
			return;

		var obstacle = ClearingObstacle;
		if ( obstacle is null || !obstacle.IsValid() )
		{
			// Host destroy succeeded (or object vanished) — treat as success.
			FinishObstacleClearUi( _obstacleClearCellKey );
			return;
		}

		_obstacleClearProgress += Time.Delta;
		TickObstacleClearSounds( obstacle.ObstacleType );
		if ( _obstacleClearUiTick )
		{
			_obstacleClearUiTick = 0.1f;
			Revision++;
		}

		var duration = GameConstants.ObstacleClearDuration;
		if ( _obstacleClearProgress < duration )
		{
			// Walked away mid-clear — abort early instead of failing silently at 100%.
			if ( !IsLocalPlayerNearObstacle( obstacle, GameConstants.ObstacleClearRadius ) )
			{
				ZooState.Instance?.Notify( "Moved too far — clear cancelled.", "warning" );
				CancelObstacleClear();
				return;
			}

			return;
		}

		var clearedKey = obstacle.CellKey;

		// Retry clear (and re-arm begin without resetting host timer) until the
		// obstacle is destroyed. One-shot RequestClear was racing RPC lag / begin arm.
		if ( _obstacleClearRetryTick )
		{
			_obstacleClearRetryTick = 0.12f;
			TerrainObstacleSystem.Instance?.RequestBeginClear( clearedKey );
			TerrainObstacleSystem.Instance?.RequestClear( clearedKey );
			Revision++;
		}

		if ( _obstacleClearProgress < duration + ObstacleClearCompleteGrace )
			return;

		ZooState.Instance?.Notify( "Couldn't clear that — stand closer and try again.", "warning" );
		FinishObstacleClearUi( clearedKey );
	}

	public static void OpenBuild( BuildCategory category )
	{
		ClearInspectState();
		DebugVisible = false;
		BuildCategoryOverride = category;
		ActivePage = UiPage.Build;
		Revision++;
		BuildController.Instance?.CancelMode();
	}

	/// <summary>Close menus and enter placement mode for a specific buildable.</summary>
	public static void BeginPlace( string placeableId )
	{
		CloseModals();

		var def = Defs.Placeable( placeableId );
		if ( def is null )
		{
			PushToast( "That buildable isn't available yet.", "block" );
			return;
		}

		if ( !BuildValidation.IsUnlocked( def ) )
		{
			PushToast( $"{def.DisplayName} unlocks at level {def.UnlockLevel}.", "lock" );
			OpenBuild( def.Category );
			return;
		}

		Close();
		BuildController.Instance?.BeginPlace( def );
	}

	/// <summary>Enter placement for the starter-biome small habitat.</summary>
	public static void BeginPlaceStarterHabitat()
	{
		CloseModals();

		var habitat = StarterGoalGuide.RecommendedHabitat();
		if ( habitat is null || !BuildValidation.IsUnlocked( habitat ) )
		{
			OpenBuild( BuildCategory.Habitats );
			return;
		}

		Close();
		BuildController.Instance?.BeginPlace( habitat );
	}

	/// <summary>Close menus and enter move mode for an existing animal.</summary>
	public static void BeginMoveAnimal( string animalId )
	{
		CloseModals();

		var animal = AnimalRegistry.Find( animalId );
		if ( animal is null )
		{
			PushToast( "That animal is no longer here.", "block" );
			return;
		}

		BuildController.Instance?.BeginMoveAnimal( animal );
	}

	public static void Toggle( UiPage page )
	{
		if ( ActivePage == page )
		{
			Close();
			return;
		}

		ClearInspectState();
		DebugVisible = false;
		BackpackOpen = false;
		ActivePage = page;
		BuildCategoryOverride = page == UiPage.Build ? SuggestBuildCategory() : null;
		if ( page != UiPage.Market )
			MarketTab = 0;
		Revision++;
		BuildController.Instance?.CancelMode();

		if ( page == UiPage.Stats )
		{
			var harms = ZooStatsReport.GetRatingHarms();
			Log.Info( $"[Fauna2 UI] Opened Stats — placeables={PlaceableRegistry.Count} guests={GuestSystem.Instance?.GuestCount ?? 0} harms={harms.Count}" );
		}
	}

	/// <summary>Which build tab to open first — entrance/paths are early objectives.</summary>
	public static BuildCategory SuggestBuildCategory()
	{
		if ( !PathNetwork.HasEntrance || !PathNetwork.HasGuestAccess )
			return BuildCategory.Paths;

		if ( HabitatRegistry.Count == 0 )
			return BuildCategory.Habitats;

		return BuildCategory.Habitats;
	}

	/// <summary>Open a page without toggling it closed when already active.</summary>
	public static void OpenPage( UiPage page )
	{
		if ( ActivePage == page && BuildCategoryOverride is null )
			return;

		ClearInspectState();
		DebugVisible = false;
		BackpackOpen = false;
		ActivePage = page;
		BuildCategoryOverride = null;
		if ( page != UiPage.Market )
			MarketTab = 0;
		Revision++;
		BuildController.Instance?.CancelMode();
	}

	/// <summary>Open the market on a specific tab (0 adopt, 1 backpack, 2 owned, 3 tools).</summary>
	public static void OpenMarketTab( int tab )
	{
		ClearInspectState();
		DebugVisible = false;
		BackpackOpen = false;
		ActivePage = UiPage.Market;
		BuildCategoryOverride = null;
		MarketTab = tab.Clamp( 0, 3 );
		Revision++;
		BuildController.Instance?.CancelMode();
	}

	public static void OpenMarketForStarterAnimal()
	{
		OpenMarketForHabitat( ZooState.Instance?.StarterBiome ?? Biome.Grassland );
		MarketSizeFilter = HabitatSizeTier.Small;
	}

	/// <summary>Open adopt tab filtered to animals that fit this habitat biome.</summary>
	public static void OpenMarketForHabitat( Biome biome )
	{
		ClearInspectState();
		DebugVisible = false;
		BackpackOpen = false;
		ActivePage = UiPage.Market;
		BuildCategoryOverride = null;
		MarketTab = 0;
		MarketBiomeFilter = biome;
		Revision++;
		BuildController.Instance?.CancelMode();
	}

	public static HabitatSizeTier? ConsumeMarketSizeFilter()
	{
		var tier = MarketSizeFilter;
		MarketSizeFilter = null;
		return tier;
	}

	public static Biome? ConsumeMarketBiomeFilter()
	{
		var biome = MarketBiomeFilter;
		MarketBiomeFilter = null;
		return biome;
	}

	public static void SetMarketTab( int tab )
	{
		MarketTab = tab.Clamp( 0, 3 );
		Revision++;
	}

	/// <summary>Close the active page menu only.</summary>
	public static void Close()
	{
		if ( !ClearPageState() )
			return;

		Revision++;
	}

	/// <summary>Close page menus before world placement without cancelling build mode.</summary>
	public static void CloseForPlacement()
	{
		ClearInspectState();
		DebugVisible = false;

		var changed = ClearPageState();
		if ( BackpackOpen )
		{
			BackpackOpen = false;
			changed = true;
		}

		if ( !changed )
			return;

		Revision++;
	}

	public static void ToggleBackpack()
	{
		if ( BackpackOpen )
		{
			CloseBackpack();
			return;
		}

		ClearInspectState();
		DebugVisible = false;
		ClearPageState();
		BackpackOpen = true;
		Revision++;
		BuildController.Instance?.CancelMode();
	}

	public static void CloseBackpack()
	{
		if ( !BackpackOpen )
			return;

		BackpackOpen = false;
		Revision++;
	}

	/// <summary>Close page menus, inspect panels, and debug together.</summary>
	public static void CloseModals()
	{
		var changed = ClearPageState() | ClearInspectState() | ClearDebugState();
		if ( BackpackOpen )
		{
			BackpackOpen = false;
			changed = true;
		}

		if ( !changed )
			return;

		Revision++;
		BuildController.Instance?.CancelMode();
	}

	public static void ShowControlsHintIfNeeded()
	{
		if ( !GameSettings.Current.ShowControlsHint || WelcomeIntroVisible ) return;
		ControlsHintVisible = true;
		Revision++;
	}

	public static void RequestWelcomeIntroForNewZoo()
	{
		_welcomeIntroForNewZoo = true;
	}

	public static void ShowWelcomeIntroIfNeeded()
	{
		var show = _welcomeIntroForNewZoo || GameSettings.Current.ShowWelcomeIntro;
		_welcomeIntroForNewZoo = false;

		if ( !show )
		{
			FinishStartupPresentation();
			return;
		}

		WelcomeIntroVisible = true;
		_toasts.Clear();
		Revision++;
	}

	public static void DismissWelcomeIntro()
	{
		if ( !WelcomeIntroVisible && !GameSettings.Current.ShowWelcomeIntro )
			return;

		WelcomeIntroVisible = false;
		ControlsHintVisible = false;
		GameSettings.Current.ShowWelcomeIntro = false;
		GameSettings.Current.ShowControlsHint = false;
		GameSettings.Save();
		Revision++;
		FinishStartupPresentation();
	}

	public static void ReleaseStartupNotifications() => FinishStartupPresentation();

	private static void FinishStartupPresentation()
	{
		StartupToastsSuppressed = false;
		DailyBonusSystem.Instance?.PresentDeferredBonus();

		if ( Networking.IsHost && ZooMilestones.Instance.IsValid() && !ZooMilestones.Instance.EconomyTutorialShown )
			ZooMilestones.Instance.EconomyTutorialShown = true;
	}

	public static void DismissControlsHint()
	{
		if ( !ControlsHintVisible && !GameSettings.Current.ShowControlsHint ) return;

		ControlsHintVisible = false;
		GameSettings.Current.ShowControlsHint = false;
		GameSettings.Save();
		Revision++;
	}

	/// <summary>Reset gameplay UI when the main menu is showing.</summary>
	public static void EnsureMenuMode()
	{
		if ( GameManager.Instance?.GameStarted ?? false )
			return;

		SessionLoading = false;
		_worldReadyPending = false;
		CloseModals();
	}

	/// <summary>Cover the screen while zoo systems and world visuals finish booting.</summary>
	public static void BeginSessionLoading()
	{
		if ( SessionLoading )
			return;

		SessionLoading = true;
		_worldReadyPending = false;
		_loadingStarted = 0f;
		StartupToastsSuppressed = true;
		WelcomeIntroVisible = false;
		_welcomeIntroForNewZoo = false;
		_toasts.Clear();
		CloseModals();
		Revision++;
	}

	/// <summary>World visuals finished — release the loading overlay once the minimum display time elapses.</summary>
	public static void NotifyWorldReady()
	{
		if ( !SessionLoading )
			return;

		_worldReadyPending = true;
		TryCompleteSessionLoading();
	}

	private static void TryCompleteSessionLoading()
	{
		if ( !SessionLoading || !_worldReadyPending || _loadingStarted < MinLoadingSeconds )
			return;

		SessionLoading = false;
		_worldReadyPending = false;
		Revision++;
		GameManager.Instance?.OnSessionLoadingComplete();
	}

	private static TimeSince _loadingStarted;
	private static bool _worldReadyPending;
	private const float MinLoadingSeconds = 0.85f;

	/// <summary>Minimum loading overlay duration — exposed for the progress bar.</summary>
	public static float LoadingMinSeconds => MinLoadingSeconds;

	private const float MaxLoadingSeconds = 12f;

	private static bool ClearPageState()
	{
		if ( ActivePage == UiPage.None && BuildCategoryOverride is null )
			return false;

		ActivePage = UiPage.None;
		BuildCategoryOverride = null;
		MarketTab = 0;
		return true;
	}

	private static bool ClearInspectState()
	{
		if ( string.IsNullOrEmpty( SelectedAnimalId )
			&& string.IsNullOrEmpty( SelectedHabitatId )
			&& string.IsNullOrEmpty( SelectedObstacleCellKey ) )
			return false;

		SelectedAnimalId = null;
		SelectedHabitatId = null;
		SelectedObstacleCellKey = null;
		return true;
	}

	private static bool ClearDebugState()
	{
		if ( !DebugVisible )
			return false;

		DebugVisible = false;
		return true;
	}

	public static BuildCategory? ConsumeBuildCategoryOverride()
	{
		var cat = BuildCategoryOverride;
		BuildCategoryOverride = null;
		return cat;
	}

	private static TimeSince _lastClickSound;

	public static void PlayClick()
	{
		if ( _lastClickSound < 0.05f )
			return;

		_lastClickSound = 0f;
		ZooSoundEffects.PlayUiClick();
	}

	public static void PushToast( string message, string icon = "info" )
	{
		if ( StartupToastsSuppressed )
			return;

		_toasts.Insert( 0, new Toast { Message = message, Icon = icon, Age = 0 } );
		if ( _toasts.Count > 6 ) _toasts.RemoveAt( _toasts.Count - 1 );
		Revision++;
	}

	public static void PushFloat( string text, string style = "money" )
	{
		_floatPopups.Add( new FloatPopup { Text = text, Style = style, Age = 0 } );
		if ( _floatPopups.Count > 8 ) _floatPopups.RemoveAt( 0 );
		Revision++;
	}

	public static void PulseXp()
	{
		XpPulse = true;
		_xpPulseEnd = 0.6f;
		Revision++;
	}

	public static void ShowCelebration( string title, string subtitle, string icon = "celebration" )
	{
		if ( StartupToastsSuppressed )
			return;

		_celebration = new Celebration { Title = title, Subtitle = subtitle, Icon = icon, Age = 0 };
		Revision++;
	}

	public static void OpenCatchMinigame()
	{
		CatchMinigameOpen = true;
		Revision++;
	}

	public static void CloseCatchMinigame()
	{
		if ( !CatchMinigameOpen ) return;
		CatchMinigameOpen = false;
		Revision++;
	}

	public static void Update()
	{
		_pointerRefreshed = false;
		var changed = false;

		if ( _toasts.RemoveAll( t => t.Age > 6f ) > 0 )
			changed = true;

		if ( _floatPopups.RemoveAll( p => p.Age > 2.2f ) > 0 )
			changed = true;

		if ( XpPulse && !_xpPulseEnd )
		{
			XpPulse = false;
			changed = true;
		}

		if ( _celebration is not null && _celebration.Age > 3.5f )
		{
			_celebration = null;
			changed = true;
		}

		TickObstacleClear();
		TryCompleteSessionLoading();

		if ( SessionLoading && _loadingStarted > MaxLoadingSeconds )
		{
			Log.Warning( "[Fauna2 UI] Session loading timed out — releasing HUD." );
			SessionLoading = false;
			_worldReadyPending = false;
			Revision++;
			GameManager.Instance?.OnSessionLoadingComplete();
		}

		if ( changed )
			Revision++;
	}

	public static void RefreshPointerState( Scene scene )
	{
		if ( _pointerRefreshed )
			return;

		_pointerRefreshed = true;
		_pointerFrame++;
		var pos = Mouse.Position;
		if ( pos == _lastMousePos && ( _pointerFrame & 1 ) == 0 )
			return;

		_lastMousePos = pos;
		var blocked = false;
		PointerBlocker = "";

		foreach ( var panelComponent in scene.GetAllComponents<PanelComponent>() )
		{
			var root = panelComponent.Panel;
			if ( root is null || !root.IsValid() || !root.IsVisible ) continue;

			foreach ( var node in root.Descendants )
			{
				if ( !node.IsVisibleSelf ) continue;
				if ( !node.WantsMouseInput() ) continue;
				if ( !node.IsInside( pos ) ) continue;

				blocked = true;
				PointerBlocker = $"{panelComponent.GetType().Name}/{node.ElementName}.{node.Classes}";
				break;
			}

			if ( blocked ) break;
		}

		PointerOverUI = blocked;
	}

	private static Vector2 _lastMousePos;
	private static int _pointerFrame;
	private static bool _pointerRefreshed;
}
