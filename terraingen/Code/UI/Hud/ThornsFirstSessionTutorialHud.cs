namespace Terraingen.UI.Hud;

using Sandbox.UI;
using Terraingen.UI;
using Terraingen.UI.Core;

/// <summary>First-session control primer — dismissible overlay for ~60 seconds.</summary>
public sealed class ThornsFirstSessionTutorialHud
{
	readonly Panel _root;
	readonly Label _title;
	readonly Label _body;
	readonly Label _step;
	int _stepIndex;
	TimeSince _stepTimer;
	TimeSince _sessionTimer;
	bool _dismissed;
	bool _combatHidden;

	static readonly (string Title, string Body)[] Steps =
	{
		( "Move & Look", "WASD to move. Mouse to look. Hold Shift to sprint." ),
		( "Interact", "Press E to interact — doors, containers, and stations. LMB attacks or gathers." ),
		( "Survive", "Hold RMB on food or water in your hotbar. Hold E at rivers to drink." ),
		( "Menus", "Tab opens the game menu. Or use I, J, K, and M for Inventory, Journal, Skills, and Map." ),
		( "Build", "Press B to open build mode. Q/R rotate pieces." ),
		( "Goals", "Follow the objective card on the right — click it to open your journal." )
	};

	public ThornsFirstSessionTutorialHud( Panel parent )
	{
		_root = ThornsUiFactory.AddPanel( parent, "first-session-tutorial thorns-hud-glass" );
		_root.Style.Position = PositionMode.Absolute;
		_root.Style.Top = Length.Pixels( 96 );
		_root.Style.Left = Length.Percent( 50 );
		_root.Style.MarginLeft = Length.Pixels( -220 );
		_root.Style.Width = Length.Pixels( 440 );
		_root.Style.FlexDirection = FlexDirection.Column;
		_root.Style.Padding = Length.Pixels( 14 );
		_root.Style.PointerEvents = PointerEvents.All;
		ThornsUiLayer.Apply( _root, ThornsUiPriority.Journal );

		_title = ThornsUiFactory.AddLabel( _root, "SURVIVOR BASICS", "tutorial-title thorns-header" );
		_body = ThornsUiFactory.AddPassiveLabel( _root, "", "tutorial-body" );
		_body.Style.WhiteSpace = WhiteSpace.Normal;
		_body.Style.MarginTop = Length.Pixels( 8 );
		_step = ThornsUiFactory.AddPassiveLabel( _root, "1 / 6", "tutorial-step thorns-muted" );
		_step.Style.MarginTop = Length.Pixels( 8 );

		var dismiss = ThornsUiFactory.AddClickable( _root, "tutorial-dismiss thorns-accent", "Got it — hide tips  (H)", DismissPermanently );
		dismiss.Style.MarginTop = Length.Pixels( 10 );
		dismiss.Style.JustifyContent = Justify.Center;

		_dismissed = ThornsLocalSettings.Current.FirstSessionTutorialDismissed;
		_sessionTimer = 0;
		ApplyStep( 0 );
		RefreshVisibility();
	}

	public void Tick( float delta )
	{
		if ( _dismissed || ThornsLocalSettings.Current.FirstSessionTutorialDismissed )
		{
			RefreshVisibility();
			return;
		}

		TryHandleDismissInput();

		if ( _dismissed )
		{
			RefreshVisibility();
			return;
		}

		if ( _sessionTimer > 60f )
		{
			DismissPermanently();
			return;
		}

		if ( _stepTimer < 10f )
			return;

		_stepTimer = 0;
		_stepIndex = Math.Min( Steps.Length - 1, _stepIndex + 1 );
		ApplyStep( _stepIndex );
		RefreshVisibility();
	}

	public void Hide()
	{
		_combatHidden = true;
		RefreshVisibility();
	}

	public void TryHandleDismissInput()
	{
		if ( _dismissed || ThornsLocalSettings.Current.FirstSessionTutorialDismissed )
			return;

		if ( Input.Keyboard.Pressed( "h" ) || Input.Keyboard.Pressed( "H" ) )
			DismissPermanently();
	}

	void DismissPermanently()
	{
		if ( _dismissed || ThornsLocalSettings.Current.FirstSessionTutorialDismissed )
			return;

		_dismissed = true;
		ThornsLocalSettings.Current.FirstSessionTutorialDismissed = true;
		ThornsLocalSettings.Save();
		RefreshVisibility();
	}

	bool IsShowing =>
		!_dismissed
		&& !_combatHidden
		&& !ThornsMenuHost.IsOpen
		&& !ThornsUiGameplayState.ShouldHideTutorial;

	void ApplyStep( int index )
	{
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
		if ( !_root.IsValid )
			return;

		var show = IsShowing;
		_root.Style.Display = show ? DisplayMode.Flex : DisplayMode.None;
	}
}
