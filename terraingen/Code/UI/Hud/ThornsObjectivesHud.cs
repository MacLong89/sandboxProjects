namespace Terraingen.UI.Hud;

using Sandbox.UI;
using Terraingen.GameData;
using Terraingen.UI;
using Terraingen.UI.Core;

public sealed class ThornsObjectivesHud
{
	readonly Panel _root;
	readonly Panel _list;
	readonly Label _categoryLabel;
	readonly Panel _alertIcon;
	readonly Panel _alertGraphic;
	readonly Panel _classicCard;
	readonly bool _classicLayout;

	public ThornsObjectivesHud( Panel parent )
	{
		_classicLayout = ThornsHudClassicChrome.IsActive;

		var root = ThornsUiFactory.AddClickable( parent, "objectives-hud", OpenJournalTab );
		root.Style.Width = Length.Percent( 100 );
		root.Style.FlexDirection = FlexDirection.Column;
		root.Style.FlexShrink = 0;
		root.Style.PointerEvents = PointerEvents.All;
		_root = root;

		if ( _classicLayout )
		{
			root.AddClass( "objectives-hud-classic" );
			ThornsHudTheme.ApplyHudWoodPanel( root );
			_classicCard = ThornsUiFactory.AddPanel( root, "objectives-classic-card" );
			_classicCard.Style.Width = Length.Percent( 100 );
			_classicCard.Style.FlexShrink = 0;
		}
		else
		{
			ThornsHudTheme.ApplyObjectivesHudPanel( root );
			_classicCard = null;
		}

		BuildChrome( root, out _list, out _categoryLabel, out _alertIcon, out _alertGraphic );
	}

	public void UpdatePinAlert()
	{
		if ( _alertIcon is null || !_alertIcon.IsValid || _alertGraphic is null || !_alertGraphic.IsValid )
			return;

		var flashing = ThornsJournalPinAlert.IsFlashing;
		_alertIcon.SetClass( "journal-pin-alert-flash", flashing );

		if ( !flashing )
		{
			_alertGraphic.Style.Opacity = 1f;
			_alertIcon.Style.Opacity = 1f;
			return;
		}

		var pulse = ThornsJournalPinAlert.Pulse01;
		_alertGraphic.Style.Opacity = 0.45f + 0.55f * pulse;
		_alertIcon.Style.Opacity = 0.75f + 0.25f * pulse;
	}

	static void BuildChrome(
		Panel root,
		out Panel list,
		out Label categoryLabel,
		out Panel alertIcon,
		out Panel alertGraphic )
	{
		var head = ThornsUiFactory.AddPanel( root, "objectives-head" );
		head.Style.FlexDirection = FlexDirection.Row;
		head.Style.AlignItems = Align.Center;
		head.Style.JustifyContent = Justify.SpaceBetween;

		var headLeft = ThornsUiFactory.AddPanel( head, "objectives-head-left" );
		headLeft.Style.FlexDirection = FlexDirection.Row;
		headLeft.Style.AlignItems = Align.Center;
		headLeft.Style.FlexGrow = 1;
		headLeft.Style.MinWidth = Length.Pixels( 0 );

		if ( ThornsHudClassicChrome.IsActive )
		{
			head.Style.Display = DisplayMode.None;
			alertGraphic = null;
			alertIcon = null;
		}
		else
		{
			alertGraphic = ThornsHudPinAlertIcon.CreateNotificationGraphic( headLeft, out alertIcon );
			ThornsUiFactory.AddPassiveLabel( head, "TAB", "objectives-key objectives-head-key" );
		}

		categoryLabel = ThornsUiFactory.AddLabel( headLeft, "CURRENT GOAL", "objectives-category" );

		list = ThornsUiFactory.AddPanel( root, "objectives-list" );
		list.Style.FlexDirection = FlexDirection.Column;
		list.Style.FlexShrink = 0;
		if ( ThornsHudClassicChrome.IsActive )
			list.Style.Display = DisplayMode.None;
	}

	static void StyleMultilineHudLabel( Label label, int fontPx, int lineHeightPx, TextAlign align = TextAlign.Center )
	{
		if ( label is null || !label.IsValid )
			return;

		label.Style.FontSize = Length.Pixels( fontPx );
		label.Style.LineHeight = Length.Pixels( lineHeightPx );
		label.Style.WhiteSpace = WhiteSpace.Normal;
		label.Style.Width = Length.Percent( 100 );
		label.Style.FlexShrink = 0;
		label.Style.TextAlign = align;
	}

	static void StyleClassicHudLabel( Label label, Color color, int fontPx = 12, bool singleLine = false )
	{
		if ( label is null || !label.IsValid )
			return;

		label.Style.FontColor = color;
		label.Style.FontSize = Length.Pixels( fontPx );
		label.Style.LineHeight = Length.Pixels( fontPx + 2 );
		label.Style.WhiteSpace = singleLine ? WhiteSpace.NoWrap : WhiteSpace.Normal;
		label.Style.TextAlign = TextAlign.Left;
		label.Style.Width = Length.Percent( 100 );
		label.Style.MinWidth = Length.Pixels( 0 );
		label.Style.FlexShrink = 1;
		if ( singleLine )
		{
			label.Style.Overflow = OverflowMode.Hidden;
			label.Style.TextOverflow = TextOverflow.Ellipsis;
		}
	}

	static void AddOrnamentDivider( Panel parent )
	{
		var row = ThornsUiFactory.AddPanel( parent, "objective-ornament-divider" );
		ThornsUiFactory.AddPanel( row, "objective-ornament-line" );
		ThornsUiFactory.AddPanel( row, "objective-ornament-diamond" );
		ThornsUiFactory.AddPanel( row, "objective-ornament-line" );
	}

	static void AddSimpleDivider( Panel parent ) =>
		ThornsUiFactory.AddPanel( parent, "objective-divider" );

	static void OpenJournalTab()
	{
		if ( !ThornsMenuTabUnlock.IsTabUnlocked( "Journal" ) )
		{
			ThornsNotificationBus.Push( "Journal unlocks after you place a workbench.", "info", 3f );
			return;
		}

		ThornsMenuHost.Instance?.SetOpen( true, "Journal" );
	}

	static void AddTaskCheck( Panel parent, bool complete )
	{
		var check = ThornsUiFactory.AddPanel( parent, "objective-task-check" );
		check.SetClass( "complete", complete );
	}

	public void Refresh()
	{
		if ( !_list.IsValid )
			return;

		if ( !ThornsUiClientState.HasSnapshot )
		{
			PopulateEmpty( _list, _categoryLabel, _classicCard );
			return;
		}

		PopulateContent( _list, _categoryLabel, _classicCard );

		var journal = ThornsUiClientState.Snapshot.Journal;
		var goal = ResolveHudGoal( journal );
		ThornsJournalPinAlert.NotifyHudGoal( goal?.GoalId );
	}

	static void PopulateEmpty( Panel list, Label categoryLabel, Panel classicCard )
	{
		if ( classicCard is not null && classicCard.IsValid )
		{
			classicCard.DeleteChildren( true );
			ThornsUiFactory.AddPassiveLabel( classicCard, "Press Tab to open your Survivor Journal.", "objectives-classic-empty" );
			return;
		}

		list.DeleteChildren( true );
		if ( categoryLabel.IsValid )
			categoryLabel.Text = "SURVIVAL LOG";
		ThornsUiFactory.AddPassiveLabel( list, "Press Tab to open your Survivor Journal.", "objectives-empty" );
	}

	void PopulateContent( Panel list, Label categoryLabel, Panel classicCard )
	{
		var journal = ThornsUiClientState.Snapshot.Journal;
		var goal = ResolveHudGoal( journal );

		if ( goal is null )
		{
			PopulateEmpty( list, categoryLabel, classicCard );
			return;
		}

		var def = ThornsDefinitionRegistry.GetGoal( goal.GoalId );
		if ( def is null )
		{
			if ( _classicLayout && classicCard is not null && classicCard.IsValid )
			{
				classicCard.DeleteChildren( true );
				ThornsUiFactory.AddPassiveLabel( classicCard, "No journal entries in progress.", "objectives-classic-empty" );
			}
			else
			{
				list.DeleteChildren( true );
				if ( categoryLabel.IsValid )
					categoryLabel.Text = "SURVIVAL LOG";
				ThornsUiFactory.AddPassiveLabel( list, "No journal entries in progress.", "objectives-empty" );
			}

			return;
		}

		if ( _classicLayout && classicCard is not null && classicCard.IsValid )
		{
			PopulateClassicConceptLayout( classicCard, def, goal );
			return;
		}

		list.DeleteChildren( true );

		if ( categoryLabel.IsValid )
			categoryLabel.Text = def.JourneyCategory.ToString().ToUpper();

		var block = ThornsUiFactory.AddPanel( list, "objective-block" );
		block.Style.FlexDirection = FlexDirection.Column;
		block.Style.FlexShrink = 0;
		block.Style.Width = Length.Percent( 100 );

		var title = ThornsUiFactory.AddPassiveLabel( block, def.Title, "objective-goal-title" );
		StyleMultilineHudLabel( title, fontPx: 20, lineHeightPx: 26 );

		AddOrnamentDivider( block );

		var journalLine = ThornsJourneyProgression.DisplayJournalText( def );
		if ( !string.IsNullOrWhiteSpace( journalLine ) )
		{
			var excerptLabel = ThornsUiFactory.AddPassiveLabel( block, journalLine, "objective-journal-excerpt" );
			StyleMultilineHudLabel( excerptLabel, fontPx: 12, lineHeightPx: 20 );
		}

		AddSimpleDivider( block );

		foreach ( var task in def.Tasks ?? Enumerable.Empty<ThornsJournalTaskDefinition>() )
		{
			ThornsJournalTaskProgressDto prog = null;
			if ( goal.Tasks is not null )
			{
				for ( var ti = 0; ti < goal.Tasks.Count; ti++ )
				{
					if ( goal.Tasks[ti].TaskId == task.Id )
					{
						prog = goal.Tasks[ti];
						break;
					}
				}
			}

			var cur = prog?.Current ?? 0;
			var tgt = prog?.Target > 0 ? prog.Target : task.TargetCount;
			var complete = tgt > 0 && cur >= tgt;

			var taskRow = ThornsUiFactory.AddPanel( block, "objective-task-row" );
			taskRow.Style.FlexDirection = FlexDirection.Row;
			taskRow.Style.AlignItems = Align.Center;
			taskRow.Style.JustifyContent = Justify.SpaceBetween;

			var taskLeft = ThornsUiFactory.AddPanel( taskRow, "objective-task-left" );
			taskLeft.Style.FlexDirection = FlexDirection.Row;
			taskLeft.Style.AlignItems = Align.Center;
			taskLeft.Style.FlexGrow = 1;
			taskLeft.Style.MinWidth = Length.Pixels( 0 );

			AddTaskCheck( taskLeft, complete );
			ThornsUiFactory.AddPassiveLabel( taskLeft, task.Label, "objective-task-label" );

			ThornsUiFactory.AddPassiveLabel( taskRow, $"{cur}/{tgt}", "objective-task-progress" );
		}

		var hint = ThornsJourneyMaterialHints.GetHint( def.Id );
		if ( !string.IsNullOrWhiteSpace( hint ) )
		{
			var hintLabel = ThornsUiFactory.AddPassiveLabel( block, hint, "objective-material-hint thorns-accent" );
			StyleMultilineHudLabel( hintLabel, fontPx: 11, lineHeightPx: 16, TextAlign.Left );
		}

		AppendContractReminder( list );
	}

	static void PopulateClassicConceptLayout(
		Panel card,
		ThornsJournalGoalDefinition def,
		ThornsJournalGoalProgressDto goal )
	{
		card.DeleteChildren( true );

		var taskStates = ThornsObjectivesHudClassicTasks.Build( def, goal );
		if ( taskStates.Count == 0 )
		{
			ThornsUiFactory.AddPassiveLabel( card, def.Title, "objectives-classic-empty" );
			return;
		}

		var primaryIndex = 0;
		for ( var i = 0; i < taskStates.Count; i++ )
		{
			if ( !taskStates[i].Complete )
			{
				primaryIndex = i;
				break;
			}
		}

		var primary = taskStates[primaryIndex];

		var topRow = ThornsUiFactory.AddPanel( card, "objectives-classic-top" );
		topRow.Style.FlexDirection = FlexDirection.Row;
		topRow.Style.Width = Length.Percent( 100 );
		topRow.Style.AlignItems = Align.Center;
		topRow.Style.JustifyContent = Justify.FlexStart;

		var iconWrap = ThornsUiFactory.AddPanel( topRow, "objectives-classic-icon-wrap" );
		iconWrap.Style.Width = Length.Pixels( 32 );
		iconWrap.Style.MinWidth = Length.Pixels( 32 );
		iconWrap.Style.Height = Length.Pixels( 32 );
		iconWrap.Style.FlexShrink = 0;
		iconWrap.Style.JustifyContent = Justify.Center;
		iconWrap.Style.AlignItems = Align.Center;
		iconWrap.Style.MarginRight = Length.Pixels( 10 );

		var journalMark = ThornsUiFactory.AddPassiveLabel( iconWrap, "J", "objectives-classic-journal-mark" );
		journalMark.Style.FontSize = Length.Pixels( 24 );
		journalMark.Style.FontWeight = 700;
		journalMark.Style.LineHeight = Length.Pixels( 24 );
		journalMark.Style.TextAlign = TextAlign.Center;
		journalMark.Style.FontColor = new Color( 1f, 248f / 255f, 234f / 255f );
		journalMark.Style.PointerEvents = PointerEvents.None;

		var bodyCol = ThornsUiFactory.AddPanel( topRow, "objectives-classic-body" );
		bodyCol.Style.FlexDirection = FlexDirection.Column;
		bodyCol.Style.FlexGrow = 1;
		bodyCol.Style.MinWidth = Length.Pixels( 0 );
		bodyCol.Style.AlignItems = Align.FlexStart;

		var typeLabel = ThornsUiFactory.AddPassiveLabel(
			bodyCol,
			ThornsJournalUiCatalog.CategoryTitle( def.JourneyCategory ),
			"objectives-classic-type" );
		StyleClassicHudLabel( typeLabel, new Color( 1f, 248f / 255f, 234f / 255f ), fontPx: 11, singleLine: true );

		var taskLine = ThornsUiFactory.AddPassiveLabel(
			bodyCol,
			$"{primary.Label} {primary.Current}/{primary.Target}",
			"objectives-classic-task-line" );
		StyleClassicHudLabel( taskLine, ThornsHudTheme.TextWarm, fontPx: 12, singleLine: true );
	}

	static void AppendContractReminder( Panel list )
	{
	}

	static ThornsJournalGoalProgressDto ResolveHudGoal( ThornsJournalSnapshotDto journal )
	{
		if ( journal?.Goals is null or { Count: 0 } )
			return null;

		if ( !string.IsNullOrWhiteSpace( journal.HudPinnedGoalId ) )
		{
			for ( var i = 0; i < journal.Goals.Count; i++ )
			{
				var goal = journal.Goals[i];
				if ( goal.State == ThornsGoalState.Active
				     && string.Equals( goal.GoalId, journal.HudPinnedGoalId, StringComparison.OrdinalIgnoreCase ) )
					return goal;
			}
		}

		for ( var i = 0; i < journal.Goals.Count; i++ )
		{
			var goal = journal.Goals[i];
			if ( goal.State == ThornsGoalState.Active )
				return goal;
		}

		return null;
	}
}

static class ThornsObjectivesHudClassicTasks
{
	internal readonly struct State
	{
		public string Label { get; }
		public int Current { get; }
		public int Target { get; }
		public bool Complete { get; }

		public State( string label, int current, int target, bool complete )
		{
			Label = label;
			Current = current;
			Target = target;
			Complete = complete;
		}
	}

	internal static List<State> Build(
		ThornsJournalGoalDefinition def,
		ThornsJournalGoalProgressDto goal )
	{
		var states = new List<State>();
		foreach ( var task in def.Tasks ?? Enumerable.Empty<ThornsJournalTaskDefinition>() )
		{
			ThornsJournalTaskProgressDto prog = null;
			if ( goal.Tasks is not null )
			{
				for ( var ti = 0; ti < goal.Tasks.Count; ti++ )
				{
					if ( goal.Tasks[ti].TaskId == task.Id )
					{
						prog = goal.Tasks[ti];
						break;
					}
				}
			}

			var cur = prog?.Current ?? 0;
			var tgt = prog?.Target > 0 ? prog.Target : task.TargetCount;
			var complete = tgt > 0 && cur >= tgt;
			states.Add( new State( task.Label, cur, tgt, complete ) );
		}

		return states;
	}
}
