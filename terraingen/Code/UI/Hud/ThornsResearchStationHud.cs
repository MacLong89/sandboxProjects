namespace Terraingen.UI.Hud;

using Sandbox.UI;
using Terraingen.GameData;
using Terraingen.Player;
using Terraingen.UI;
using Terraingen.UI.Core;

/// <summary>Ascension research ladder — full-screen menu chrome, opened from placed Research Stations.</summary>
public sealed class ThornsResearchStationHud
{
	readonly Panel _backdrop;
	readonly Panel _root;
	readonly Panel _levelsScroll;
	readonly Panel _progressFill;
	readonly Label _title;
	readonly Label _progress;
	readonly Label _active;
	readonly Label _hint;

	double _nextRefreshRealtime;

	public bool IsOpen => _backdrop.IsValid() && _backdrop.Style.Display == DisplayMode.Flex;

	public Panel Backdrop => _backdrop;

	Action<UiRevisionChannel, int> _onRevision;

	public ThornsResearchStationHud( Panel parent )
	{
		(_backdrop, _root) = ThornsMenuChrome.CreateFullscreenOverlayShell( parent, "research-station-shell" );
		_backdrop.AddClass( "research-station-backdrop" );
		_backdrop.Style.Display = DisplayMode.None;
		ThornsUiLayer.ApplyModalSurface( _backdrop, ThornsUiPriority.InventoryBuild );

		var header = ThornsUiFactory.AddPanel( _root, "research-station-header thorns-station-header" );
		header.Style.FlexDirection = FlexDirection.Row;
		header.Style.AlignItems = Align.FlexStart;
		header.Style.JustifyContent = Justify.SpaceBetween;
		header.Style.FlexShrink = 0;
		header.Style.MarginBottom = Length.Pixels( 12 );
		header.Style.Width = Length.Percent( 100 );

		var headerMain = ThornsUiFactory.AddPanel( header, "research-station-header-main" );
		_title = ThornsUiFactory.AddLabel( headerMain, "ASCENSION RESEARCH", "research-station-title thorns-header thorns-station-title" );
		_hint = ThornsUiFactory.AddPassiveLabel(
			headerMain,
			"Sequential technology ladder · ESC to close",
			"research-station-hint thorns-muted thorns-station-hint" );

		ThornsUiFactory.AddClickable( header, "close research-station-close thorns-station-close", "×", () =>
			ThornsPlayerGameplay.Local?.RequestCloseResearchStation() );

		var summary = ThornsUiFactory.AddPanel( _root, "research-station-summary thorns-menu-framed thorns-menu-frame-section" );
		summary.Style.FlexShrink = 0;

		_progress = ThornsUiFactory.AddPassiveLabel( summary, "", "research-station-meta thorns-station-meta" );
		var progressTrack = ThornsUiFactory.AddPanel( summary, "research-station-progress-track" );
		_progressFill = ThornsUiFactory.AddPanel( progressTrack, "research-station-progress-fill" );
		_active = ThornsUiFactory.AddPassiveLabel( summary, "", "research-station-meta research-station-active thorns-station-meta" );

		var body = ThornsTheme.CreateStationColumn( _root, "research-station-body" );
		body.Style.FlexGrow = 1;
		ThornsTheme.CreateSectionHeader( body, "TECHNOLOGY LEVELS" );

		_levelsScroll = ThornsUiFactory.AddPanel( body, "research-levels-scroll" );
		_levelsScroll.Style.FlexDirection = FlexDirection.Column;
		_levelsScroll.Style.FlexGrow = 1;
		_levelsScroll.Style.MinHeight = Length.Pixels( 0 );
		_levelsScroll.Style.Overflow = OverflowMode.Scroll;

		_backdrop.AddEventListener( "onmouseup", e =>
		{
			if ( e.Target == _backdrop )
				ThornsPlayerGameplay.Local?.RequestCloseResearchStation();
		} );

		_onRevision = OnRevision;
		UiRevisionBus.MenuRevisionChanged += _onRevision;
		Refresh();
	}

	public void Dispose() => UiRevisionBus.MenuRevisionChanged -= _onRevision;

	void OnRevision( UiRevisionChannel channel, int _ )
	{
		if ( channel is UiRevisionChannel.Research or UiRevisionChannel.Inventory )
			Refresh();
	}

	public void Refresh()
	{
		if ( !_backdrop.IsValid() )
			return;

		var research = ThornsUiClientState.Snapshot.Research;
		var open = research?.IsOpen == true;
		_backdrop.Style.Display = open ? DisplayMode.Flex : DisplayMode.None;
		if ( !open )
		{
			_levelsScroll.DeleteChildren( true );
			return;
		}

		var maxLevel = ThornsResearchCatalog.MaxLevel;
		var completed = Math.Clamp( research.CompletedLevel, 0, maxLevel );
		_progress.Text = $"Overall progress — {completed} / {maxLevel} levels ({research.PercentComplete:F0}%)";
		_progressFill.Style.Width = Length.Percent( Math.Clamp( research.PercentComplete, 0f, 100f ) );

		if ( research.ActiveLevel > 0 )
		{
			var def = ThornsResearchCatalog.TryGet( research.ActiveLevel, out var levelDef ) ? levelDef.Title : $"Level {research.ActiveLevel}";
			_active.Text = $"Researching L{research.ActiveLevel} {def} — {FormatTime( research.ActiveSecondsRemaining )} remaining";
			_active.SetClass( "hidden", false );
		}
		else
		{
			_active.Text = completed >= maxLevel
				? "Ascension protocol complete."
				: "Select the next available level to begin research.";
			_active.SetClass( "hidden", false );
		}

		if ( Time.Now < _nextRefreshRealtime && _levelsScroll.Children.Count() > 0 )
			return;

		_nextRefreshRealtime = Time.Now + 0.35;
		RebuildLevels( research );
	}

	void RebuildLevels( ThornsResearchSnapshotDto research )
	{
		_levelsScroll.DeleteChildren( true );
		foreach ( var level in research.Levels )
			AddLevelRow( level );
	}

	void AddLevelRow( ThornsResearchLevelDto level )
	{
		var row = ThornsUiFactory.AddPanel( _levelsScroll, "research-level-row thorns-interact" );
		row.SetClass( "completed", level.Completed );
		row.SetClass( "active", level.Active );
		row.SetClass( "available", level.Available );
		row.SetClass( "locked", !level.Completed && !level.Active && !level.Available );

		var index = ThornsUiFactory.AddPanel( row, "research-level-index" );
		var levelLabel = ThornsUiFactory.AddPassiveLabel( index, $"L{level.Level}", "research-level-number" );
		levelLabel.SetClass( "completed", level.Completed );

		var content = ThornsUiFactory.AddPanel( row, "research-level-content" );
		ThornsUiFactory.AddPassiveLabel( content, level.Title, "research-level-title" );
		ThornsUiFactory.AddPassiveLabel( content, level.Description, "research-level-desc thorns-muted" );

		var meta = level.Completed
			? "Completed"
			: level.Active
				? $"In progress — {FormatTime( level.SecondsRemaining )} remaining"
				: $"Research time — {FormatTime( level.ResearchSeconds )}";
		ThornsUiFactory.AddPassiveLabel( content, meta, "research-level-meta thorns-muted" );

		if ( level.Costs is { Count: > 0 } )
		{
			var costsRow = ThornsUiFactory.AddPanel( content, "research-level-costs" );
			costsRow.Style.FlexDirection = FlexDirection.Row;
			costsRow.Style.FlexWrap = Wrap.Wrap;
			foreach ( var cost in level.Costs )
				AddCostChip( costsRow, cost );
		}

		if ( level.RewardCount > 0 && !string.IsNullOrWhiteSpace( level.RewardItemId ) )
		{
			var rewardsRow = ThornsUiFactory.AddPanel( content, "research-level-rewards" );
			rewardsRow.Style.FlexDirection = FlexDirection.Row;
			rewardsRow.Style.FlexWrap = Wrap.Wrap;
			AddRewardChip( rewardsRow, level.RewardItemId, level.RewardCount );
		}

		var action = ThornsUiFactory.AddPanel( row, "research-level-action" );
		if ( level.Available )
		{
			var canAfford = CanAfford( level );
			var button = ThornsUiFactory.AddClickable(
				action,
				canAfford ? "research-level-btn thorns-btn-primary" : "research-level-btn thorns-btn-primary disabled",
				canAfford ? "Start" : "Missing",
				() =>
				{
					if ( CanAfford( level ) )
						ThornsPlayerGameplay.Local?.RequestStartResearchLevel( level.Level );
				} );
			button.Style.Opacity = canAfford ? 1f : 0.45f;
		}
		else
		{
			var label = level.Completed ? "Done" : level.Active ? "Active" : "Locked";
			ThornsUiFactory.AddPassiveLabel( action, label, "research-level-state" );
		}
	}

	static void AddRewardChip( Panel parent, string itemId, int count )
	{
		var name = ThornsItemRegistry.TryGet( itemId, out var def ) ? def.DisplayName : itemId;
		ThornsUiFactory.AddPassiveLabel( parent, $"Reward: {name} ×{count}", "research-reward-chip" );
	}

	static void AddCostChip( Panel parent, ThornsResearchIngredientDto cost )
	{
		var have = CountInInventory( cost.ItemId );
		var name = ThornsItemRegistry.TryGet( cost.ItemId, out var def ) ? def.DisplayName : cost.ItemId;
		var chip = ThornsUiFactory.AddPassiveLabel( parent, $"{name} {have}/{cost.Count}", "research-cost-chip" );
		chip.SetClass( "affordable", have >= cost.Count );
		chip.SetClass( "missing", have < cost.Count );
	}

	static bool CanAfford( ThornsResearchLevelDto level )
	{
		foreach ( var cost in level.Costs ?? new List<ThornsResearchIngredientDto>() )
		{
			if ( CountInInventory( cost.ItemId ) < cost.Count )
				return false;
		}

		return true;
	}

	static int CountInInventory( string itemId )
	{
		var total = 0;
		foreach ( var slot in ThornsUiClientState.Snapshot.Inventory?.Slots ?? [] )
		{
			if ( string.Equals( slot.ItemId, itemId, StringComparison.OrdinalIgnoreCase ) )
				total += slot.Count;
		}

		return total;
	}

	static string FormatTime( float seconds )
	{
		var total = Math.Max( 0, (int)MathF.Ceiling( seconds ) );
		var mm = total / 60;
		var ss = total % 60;
		return $"{mm:00}:{ss:00}";
	}
}
