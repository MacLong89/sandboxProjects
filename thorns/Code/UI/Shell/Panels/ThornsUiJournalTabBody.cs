using System;
using Sandbox.UI;

namespace Sandbox;

public sealed class ThornsUiJournalTabBody : Panel
{
	readonly Panel _listHost;
	readonly Panel _detailHost;
	readonly Label _detailTitle;
	readonly Label _detailHint;
	readonly Label _detailBody;
	readonly Label _detailProgressCount;
	readonly ThornsUiProgressBar _detailProgress;
	readonly Label _detailReward;
	readonly ThornsUiCapsuleButton _pinBtn;
	readonly ThornsUiCapsuleButton _toggleGoalsBtn;
	readonly ThornsUiCapsuleButton _toggleMilestonesBtn;

	readonly ThornsUiJournalGoalRow[] _rows;

	GameObject _pawnRoot;
	readonly Action<string, bool> _applyJournalHudPin;
	readonly Func<string> _getPinnedGoalId;
	readonly Func<bool> _getPinExplicit;
	readonly Action _ensureDefaultPinnedGoal;

	bool _goalsMode = true;
	int _selectedIndex;
	bool _didInitialSelect;

	public ThornsUiJournalTabBody(
		Action<string, bool> applyJournalHudPin,
		Func<string> getPinnedGoalId,
		Func<bool> getPinExplicit,
		Action ensureDefaultPinnedGoal )
	{
		_applyJournalHudPin = applyJournalHudPin;
		_getPinnedGoalId = getPinnedGoalId;
		_getPinExplicit = getPinExplicit;
		_ensureDefaultPinnedGoal = ensureDefaultPinnedGoal;

		AddClass( "thorns-tab-journal" );
		AddClass( "thorns-tab-journal-layout" );
		Style.FlexGrow = 1;
		Style.FlexShrink = 1;
		Style.MinHeight = 0;
		Style.Width = Length.Fraction( 1f );

		var hdr = ThornsUiPanelAdd.AddChildPanel( this, "thorns-journal-toolbar" );
		_toggleGoalsBtn = hdr.AddChild( new ThornsUiCapsuleButton( "Goals", "secondary", () => ApplyJournalMode( true ) ) );
		_toggleMilestonesBtn =
			hdr.AddChild( new ThornsUiCapsuleButton( "Completed", "secondary", () => ApplyJournalMode( false ) ) );

		var body = ThornsUiPanelAdd.AddChildPanel( this, "thorns-journal-body" );
		body.Style.FlexDirection = FlexDirection.Row;
		body.Style.FlexGrow = 1;
		body.Style.FlexShrink = 1;
		body.Style.MinHeight = 0;
		body.Style.Width = Length.Fraction( 1f );

		_listHost = ThornsUiPanelAdd.AddChildPanel( body, "thorns-journal-list" );
		_listHost.Style.FlexDirection = FlexDirection.Column;
		_listHost.Style.PointerEvents = PointerEvents.All;
		_listHost.Style.Overflow = OverflowMode.Scroll;
		_listHost.CanDragScroll = false;

		var n = ThornsMilestoneDefinitions.Count;
		_rows = new ThornsUiJournalGoalRow[n];
		for ( var i = 0; i < n; i++ )
		{
			var idx = i;
			var row = _listHost.AddChild( new ThornsUiJournalGoalRow( idx, OnRowSelected ) );
			_rows[i] = row;
		}

		_detailHost = ThornsUiPanelAdd.AddChildPanel( body, "thorns-journal-detail" );
		_detailHost.Style.FlexDirection = FlexDirection.Column;
		_detailHost.Style.PointerEvents = PointerEvents.All;
		_detailHost.Style.Overflow = OverflowMode.Scroll;
		_detailHost.CanDragScroll = false;

		_detailTitle = _detailHost.AddChild( new Label( "Select a goal", "thorns-journal-detail-title" ) );
		_detailTitle.Style.PointerEvents = PointerEvents.None;

		_detailHint = _detailHost.AddChild( new Label( "", "thorns-journal-detail-hint" ) );
		_detailHint.Style.PointerEvents = PointerEvents.None;

		_detailBody = _detailHost.AddChild( new Label( "", "thorns-tab-context-placeholder" ) );
		_detailBody.Style.PointerEvents = PointerEvents.None;
		_detailBody.Style.WhiteSpace = WhiteSpace.Normal;

		_detailHost.AddChild( new Label( "PROGRESS", "thorns-tab-section-title" ) ).Style.PointerEvents =
			PointerEvents.None;
		_detailProgressCount = _detailHost.AddChild( new Label( "", "thorns-journal-detail-progress-count" ) );
		_detailProgressCount.Style.PointerEvents = PointerEvents.None;
		_detailProgress = _detailHost.AddChild( new ThornsUiProgressBar() );

		_detailReward = _detailHost.AddChild( new Label( "", "thorns-journal-detail-reward" ) );
		_detailReward.Style.PointerEvents = PointerEvents.None;

		var pinRow = ThornsUiPanelAdd.AddChildPanel( _detailHost, "thorns-journal-pin-row" );
		pinRow.Style.FlexDirection = FlexDirection.Row;
		pinRow.Style.FlexShrink = 0;
		pinRow.Style.JustifyContent = Justify.FlexStart;
		pinRow.Style.PointerEvents = PointerEvents.All;

		_pinBtn = pinRow.AddChild( new ThornsUiCapsuleButton( "Pin to HUD", "secondary", OnPinClicked ) );

		ApplyJournalMode( true );
	}

	void OnRowSelected( int index )
	{
		_selectedIndex = Math.Clamp( index, 0, Math.Max( 0, _rows.Length - 1 ) );
		for ( var i = 0; i < _rows.Length; i++ )
			_rows[i].SetSelected( i == _selectedIndex );

		RefreshDetail();
	}

	void OnPinClicked()
	{
		if ( !ThornsMilestoneDefinitions.TryGet( _selectedIndex, out var def ) )
			return;

		var cur = _getPinnedGoalId?.Invoke() ?? "";
		var explicitPin = _getPinExplicit?.Invoke() ?? false;
		var pinnedThis = explicitPin && string.Equals( cur, def.Id, StringComparison.OrdinalIgnoreCase );
		if ( pinnedThis )
			_applyJournalHudPin?.Invoke( "", false );
		else
			_applyJournalHudPin?.Invoke( def.Id, true );

		RefreshPinButtonLabel();
	}

	void ApplyJournalMode( bool goals )
	{
		_goalsMode = goals;
		_toggleGoalsBtn.SetClass( "active", goals );
		_toggleMilestonesBtn.SetClass( "active", !goals );
		RefreshListVisibility();
		RefreshFromPawn( _pawnRoot, force: true );
	}

	void RefreshListVisibility()
	{
		for ( var i = 0; i < _rows.Length; i++ )
		{
			var ms = _pawnRoot.IsValid() ? _pawnRoot.Components.Get<ThornsPlayerMilestones>() : null;
			var done = ms.IsValid() && ms.ClientIsGoalComplete( i );
			_rows[i].Style.Display = _goalsMode ? DisplayMode.Flex : (done ? DisplayMode.Flex : DisplayMode.None);
		}
	}

	public void RefreshFromPawn( GameObject pawnRoot, bool force )
	{
		_pawnRoot = pawnRoot;
		_ensureDefaultPinnedGoal?.Invoke();

		var ms = pawnRoot.IsValid() ? pawnRoot.Components.Get<ThornsPlayerMilestones>() : null;

		if ( !_didInitialSelect )
		{
			_didInitialSelect = true;
			var pick = 0;
			if ( ms.IsValid() )
			{
				var fi = ms.ClientFirstIncompleteGoalIndex();
				if ( fi >= 0 )
					pick = fi;
			}

			OnRowSelected( pick );
		}

		RefreshListVisibility();
		RefreshRowTitles( ms );
		RefreshDetail();
		RefreshPinButtonLabel();
	}

	void RefreshRowTitles( ThornsPlayerMilestones ms )
	{
		var pr = ms.IsValid() ? ms.GetGoalProgressSnapshot() : Array.Empty<int>();
		for ( var i = 0; i < _rows.Length; i++ )
		{
			if ( !ThornsMilestoneDefinitions.TryGet( i, out var def ) )
				continue;

			var p = i < pr.Length ? pr[i] : 0;
			var done = p >= def.TargetValue;
			var cur = Math.Min( p, def.TargetValue );
			var frac = def.TargetValue > 0 ? Math.Clamp( p / (float)def.TargetValue, 0f, 1f ) : 1f;
			var countStr = done ? $"{def.TargetValue} / {def.TargetValue}" : $"{cur} / {def.TargetValue}";

			_rows[i].SetContent( def.Title, def.ShortHint, done, frac, countStr );
		}
	}

	void RefreshDetail()
	{
		if ( !ThornsMilestoneDefinitions.TryGet( _selectedIndex, out var def ) )
			return;

		var ms = _pawnRoot.IsValid() ? _pawnRoot.Components.Get<ThornsPlayerMilestones>() : null;
		var pr = ms.IsValid() ? ms.GetGoalProgressSnapshot() : Array.Empty<int>();
		var p = _selectedIndex < pr.Length ? pr[_selectedIndex] : 0;
		var done = p >= def.TargetValue;
		var cur = Math.Min( p, def.TargetValue );
		var frac = done
			? 1f
			: def.TargetValue > 0
				? Math.Clamp( p / (float)def.TargetValue, 0f, 1f )
				: 1f;

		_detailTitle.Text = def.Title;
		_detailHint.Text = def.ShortHint;
		_detailBody.Text = def.Description;
		_detailProgressCount.Text = done
			? $"{def.TargetValue} / {def.TargetValue}"
			: $"{cur} / {def.TargetValue}";
		_detailProgress.SetFraction01( frac );
		_detailReward.Text = done
			? $"Completed · +{def.RewardXp} XP awarded."
			: $"Reward on completion: +{def.RewardXp} XP";

		_pinBtn.Style.Display = _goalsMode ? DisplayMode.Flex : DisplayMode.None;
		RefreshPinButtonLabel();
	}

	void RefreshPinButtonLabel()
	{
		if ( !ThornsMilestoneDefinitions.TryGet( _selectedIndex, out var def ) )
			return;

		var pinned = _getPinnedGoalId?.Invoke() ?? "";
		var explicitPin = _getPinExplicit?.Invoke() ?? false;
		var isPinned = explicitPin && string.Equals( pinned, def.Id, StringComparison.OrdinalIgnoreCase );
		_pinBtn.SetClass( "active", isPinned );
		_pinBtn.SetLabel( isPinned ? "Unpin from HUD" : "Pin to HUD" );
	}
}

sealed class ThornsUiJournalGoalRow : ThornsUiCardPanel
{
	public int GoalIndex { get; }
	readonly Label _title;
	readonly Label _hint;
	readonly Label _count;
	readonly ThornsUiProgressBar _bar;
	readonly Action<int> _onPick;

	public ThornsUiJournalGoalRow( int goalIndex, Action<int> onPick )
		: base( "thorns-item-card thorns-journal-goal-row" )
	{
		GoalIndex = goalIndex;
		_onPick = onPick;
		Style.PointerEvents = PointerEvents.All;
		Style.FlexShrink = 0;
		AddEventListener( "onmousedown", _ => _onPick?.Invoke( GoalIndex ) );

		_title = AddChild( new Label( "", "thorns-item-card-title" ) );
		_title.Style.PointerEvents = PointerEvents.None;
		_title.Style.WhiteSpace = WhiteSpace.Normal;
		_title.Style.FlexShrink = 0;
		_hint = AddChild( new Label( "", "thorns-item-card-sub" ) );
		_hint.Style.PointerEvents = PointerEvents.None;
		_hint.Style.WhiteSpace = WhiteSpace.Normal;
		_hint.Style.FlexShrink = 0;

		var meta = ThornsUiPanelAdd.AddChildPanel( this, "thorns-journal-goal-row-meta" );
		meta.Style.PointerEvents = PointerEvents.None;
		meta.Style.FlexShrink = 0;
		_count = meta.AddChild( new Label( "", "thorns-journal-goal-row-count" ) );
		_count.Style.PointerEvents = PointerEvents.None;
		_count.Style.FlexShrink = 0;
		_bar = meta.AddChild( new ThornsUiProgressBar() );
	}

	public void SetContent( string title, string shortHint, bool completed, float progressFrac01, string countText )
	{
		_title.Text = title;
		_hint.Text = shortHint;
		_count.Text = countText;
		_bar.SetFraction01( completed ? 1f : progressFrac01 );
		SetClass( "thorns-journal-goal-row--complete", completed );
	}

	public void SetSelected( bool sel ) => SetClass( "thorns-journal-goal-row--selected", sel );
}
