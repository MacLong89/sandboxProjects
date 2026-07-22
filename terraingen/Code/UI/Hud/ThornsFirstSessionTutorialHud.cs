namespace Terraingen.UI.Hud;

using Sandbox.UI;
using Terraingen.Buildings;
using Terraingen.Combat;
using Terraingen.Player;
using Terraingen.UI;
using Terraingen.UI.Core;

/// <summary>First-session control primer — goal-gated center modal (Got it soft-dismisses; next step after goal completes).</summary>
public sealed class ThornsFirstSessionTutorialHud
{
	/// <summary>When true, the first-session primer is built into the gameplay HUD.</summary>
	public static bool Enabled { get; set; } = true;

	const float StepCooldownSeconds = 0.55f;
	const float MoveGoalSeconds = 0.85f;
	const float SurviveHoldSeconds = 0.45f;

	readonly Panel _backdrop;
	readonly Panel _modal;
	readonly Label _title;
	readonly Label _body;
	readonly Label _step;

	readonly bool[] _stepGoalsComplete = new bool[6];
	int _stepIndex;
	bool _sessionComplete;
	bool _combatHidden;
	bool _softDismissed;
	TimeSince _stepCooldown;
	float _moveAccum;
	float _surviveHoldAccum;
	Vector3? _lastMoveSamplePos;

	static readonly (string Title, string Body)[] Steps =
	{
		( "Move & Look", "WASD to move. Mouse to look. Hold Shift to sprint." ),
		( "Interact", "Press E to interact — doors, containers, and stations. LMB attacks or gathers." ),
		( "Survive", "Hold RMB on food or water in your hotbar. Hold E at rivers to drink." ),
		( "Menus", "Tab opens the game menu. Or use I, J, K, and M for Inventory, Journal, Skills, and Map." ),
		( "Build", "Press B to open build mode. Q/R rotate pieces." ),
		( "Goals", "Follow the objective card on the right — click it to open your journal." )
	};

	public bool IsOpen => _backdrop.IsValid() && _backdrop.Style.Display == DisplayMode.Flex;

	public Panel Backdrop => _backdrop;

	public ThornsFirstSessionTutorialHud( Panel parent )
	{
		_backdrop = ThornsUiFactory.AddPanel( parent, "tutorial-tip-overlay" );
		_backdrop.Style.Position = PositionMode.Absolute;
		_backdrop.Style.Left = Length.Pixels( 0 );
		_backdrop.Style.Top = Length.Pixels( 0 );
		_backdrop.Style.Width = Length.Percent( 100 );
		_backdrop.Style.Height = Length.Percent( 100 );
		_backdrop.Style.Display = DisplayMode.None;
		_backdrop.Style.FlexDirection = FlexDirection.Column;
		_backdrop.Style.JustifyContent = Justify.Center;
		_backdrop.Style.AlignItems = Align.Center;
		_backdrop.Style.PointerEvents = PointerEvents.All;
		ThornsUiLayer.ApplyModalSurface( _backdrop, ThornsUiPriority.CriticalPopup );
		ThornsMenuChrome.ApplyMenuOverlay( _backdrop );

		_modal = ThornsUiFactory.AddPanel( _backdrop, "tutorial-tip-modal thorns-glass" );
		_modal.Style.FlexDirection = FlexDirection.Column;
		_modal.Style.AlignItems = Align.Stretch;
		_modal.Style.Width = Length.Pixels( 520 );
		_modal.Style.MaxWidth = Length.Percent( 94 );
		_modal.Style.PaddingTop = Length.Pixels( 32 );
		_modal.Style.PaddingRight = Length.Pixels( 36 );
		_modal.Style.PaddingBottom = Length.Pixels( 28 );
		_modal.Style.PaddingLeft = Length.Pixels( 36 );
		_modal.Style.PointerEvents = PointerEvents.All;

		_title = ThornsUiFactory.AddLabel( _modal, "SURVIVOR BASICS", "tutorial-tip-title thorns-header" );
		_title.Style.TextAlign = TextAlign.Center;
		_title.Style.WhiteSpace = WhiteSpace.Normal;

		_body = ThornsUiFactory.AddPassiveLabel( _modal, "", "tutorial-tip-body" );
		_body.Style.WhiteSpace = WhiteSpace.Normal;
		_body.Style.MarginTop = Length.Pixels( 12 );
		_body.Style.LineHeight = Length.Pixels( 22 );
		_body.Style.TextAlign = TextAlign.Left;

		_step = ThornsUiFactory.AddPassiveLabel( _modal, "1 / 6", "tutorial-tip-step thorns-muted" );
		_step.Style.MarginTop = Length.Pixels( 10 );
		_step.Style.TextAlign = TextAlign.Center;

		var btnRow = ThornsUiFactory.AddPanel( _modal, "tutorial-tip-btn-row" );
		btnRow.Style.FlexDirection = FlexDirection.Row;
		btnRow.Style.JustifyContent = Justify.Center;
		btnRow.Style.AlignItems = Align.Center;
		btnRow.Style.MarginTop = Length.Pixels( 22 );
		btnRow.Style.Gap = Length.Pixels( 12 );

		var hideBtn = ThornsUiFactory.AddClickable( btnRow, "thorns-btn-secondary tutorial-tip-hide-btn", HideAllTips );
		ThornsUiFactory.AddPassiveLabel( hideBtn, "Hide tips" );

		var gotItBtn = ThornsUiFactory.AddClickable( btnRow, "thorns-btn-primary tutorial-tip-got-it-btn", DismissStep );
		ThornsUiFactory.AddPassiveLabel( gotItBtn, "Got it" );

		_stepIndex = 0;
		ApplyStep( 0 );
		RefreshVisibility();
	}

	public void Tick( float delta )
	{
		if ( ThornsLocalSettings.Current.FirstSessionTutorialDismissed || _sessionComplete )
		{
			RefreshVisibility();
			return;
		}

		TickGoals( delta );
		TryHandleDismissInput();
		RefreshVisibility();
	}

	public void Hide()
	{
		_combatHidden = true;
		RefreshVisibility();
	}

	public void TryHandleDismissInput()
	{
		if ( Input.Keyboard.Pressed( "h" ) || Input.Keyboard.Pressed( "H" ) )
			ToggleTipsHidden();
	}

	public void DismissStep()
	{
		if ( ThornsLocalSettings.Current.FirstSessionTutorialDismissed || _sessionComplete )
			return;

		_softDismissed = true;
		RefreshVisibility();
	}

	void HideAllTips()
	{
		if ( ThornsLocalSettings.Current.FirstSessionTutorialDismissed )
			return;

		ThornsLocalSettings.Current.FirstSessionTutorialDismissed = true;
		ThornsLocalSettings.Save();
		ThornsNotificationBus.Push( "Tips hidden — press H to show again", "info", 3f );
		RefreshVisibility();
	}

	void ToggleTipsHidden()
	{
		var hidden = ThornsLocalSettings.Current.FirstSessionTutorialDismissed;
		ThornsLocalSettings.Current.FirstSessionTutorialDismissed = !hidden;
		ThornsLocalSettings.Save();

		if ( !hidden )
		{
			ThornsNotificationBus.Push( "Tips hidden — press H to show again", "info", 3f );
			RefreshVisibility();
			return;
		}

		ThornsNotificationBus.Push( "Tips enabled", "info", 3f );
		_softDismissed = false;
		_sessionComplete = false;
		RefreshVisibility();
	}

	void TickGoals( float delta )
	{
		if ( _stepIndex < 0 || _stepIndex >= Steps.Length )
			return;

		if ( !_stepGoalsComplete[_stepIndex] )
		{
			if ( IsStepGoalMet( _stepIndex, delta ) )
				CompleteStepGoal( _stepIndex );
		}
	}

	bool IsStepGoalMet( int step, float delta )
	{
		return step switch
		{
			0 => TickMoveGoal( delta ),
			1 => Input.Pressed( "Use" ) || Input.Pressed( "use" ),
			2 => TickSurviveGoal( delta ),
			3 => ThornsMenuHost.IsOpen || ThornsKeybindService.Pressed( "Tab" ) || ThornsKeybindService.Pressed( "InventoryMenu" )
			     || ThornsKeybindService.Pressed( "JournalMenu" ) || ThornsKeybindService.Pressed( "SkillsMenu" )
			     || ThornsKeybindService.Pressed( "MapMenu" ),
			4 => ThornsPlayerBuildingController.Local?.BuildMenuOpen == true || ThornsKeybindService.Pressed( "Build" ),
			5 => ThornsMenuHost.IsJournalTabOpen || ThornsKeybindService.Pressed( "JournalMenu" ),
			_ => false
		};
	}

	bool TickMoveGoal( float delta )
	{
		var moved = Input.AnalogMove.Length > 0.08f;
		var player = ThornsPlayerGameplay.Local;
		if ( player.IsValid() )
		{
			var pos = player.WorldPosition;
			if ( _lastMoveSamplePos is { } last )
			{
				if ( ( pos - last ).WithZ( 0f ).Length > 8f )
					moved = true;
			}

			_lastMoveSamplePos = pos;
		}

		if ( moved )
			_moveAccum += delta;

		return _moveAccum >= MoveGoalSeconds;
	}

	bool TickSurviveGoal( float delta )
	{
		var player = ThornsPlayerGameplay.Local;
		if ( !player.IsValid() )
			return false;

		var consume = player.Components.Get<ThornsPlayerHotbarConsumeUse>();
		if ( consume.IsValid() && consume.IsConsuming )
			return true;

		var holdingRmb = Input.Down( "Attack2" ) || Input.Down( "attack2" );
		if ( holdingRmb && consume.IsValid() && consume.TryGetActiveConsumablePrompt( out _ ) )
		{
			_surviveHoldAccum += delta;
			if ( _surviveHoldAccum >= SurviveHoldSeconds )
				return true;
		}
		else
		{
			_surviveHoldAccum = 0f;
		}

		return false;
	}

	void CompleteStepGoal( int step )
	{
		if ( step < 0 || step >= _stepGoalsComplete.Length || _stepGoalsComplete[step] )
			return;

		_stepGoalsComplete[step] = true;

		if ( step != _stepIndex )
			return;

		AdvanceAfterGoal();
	}

	void AdvanceAfterGoal()
	{
		_softDismissed = false;

		if ( _stepIndex >= Steps.Length - 1 )
		{
			_sessionComplete = true;
			RefreshVisibility();
			return;
		}

		_stepIndex++;
		ApplyStep( _stepIndex );
		_stepCooldown = 0f;
	}

	bool IsShowing =>
		!ThornsLocalSettings.Current.FirstSessionTutorialDismissed
		&& !_sessionComplete
		&& !_combatHidden
		&& !_softDismissed
		&& !ThornsMenuHost.IsOpen
		&& !ThornsMenuHost.IsVictoryIntroOpen
		&& !ThornsUiGameplayState.ShouldHideTutorial
		&& !IsStepCooldownActive;

	bool IsStepCooldownActive => _stepCooldown > 0f && _stepCooldown < StepCooldownSeconds;

	void ApplyStep( int index )
	{
		index = Math.Clamp( index, 0, Steps.Length - 1 );
		var step = Steps[index];
		if ( _title.IsValid )
			_title.Text = step.Title.ToUpper();
		if ( _body.IsValid )
			_body.Text = step.Body;
		if ( _step.IsValid )
			_step.Text = $"{index + 1} / {Steps.Length}";
	}

	void RefreshVisibility()
	{
		if ( !_backdrop.IsValid )
			return;

		var show = IsShowing;
		_backdrop.Style.Display = show ? DisplayMode.Flex : DisplayMode.None;
	}
}
