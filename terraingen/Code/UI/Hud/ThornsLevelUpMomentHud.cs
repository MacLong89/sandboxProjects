namespace Terraingen.UI.Hud;

using Sandbox.UI;
using Terraingen.UI;
using Terraingen.UI.Core;

/// <summary>Compact level-up toast under the vitals cluster — no screen dim or input capture.</summary>
public sealed class ThornsLevelUpMomentHud
{
	const float VisibleSeconds = 2.75f;
	const float FadeSeconds = ThornsUiAnimations.FadeMs / 1000f;

	enum FadePhase
	{
		Hidden,
		FadingIn,
		Visible,
		FadingOut
	}

	readonly Panel _root;
	readonly Label _title;
	double _visibleUntil;
	double _fadeCompleteRealtime;
	FadePhase _phase = FadePhase.Hidden;
	bool _pendingFadeIn;

	public ThornsLevelUpMomentHud( Panel parent )
	{
		_root = ThornsUiFactory.AddPanel( parent, "level-up-toast-host" );
		_root.Style.Position = PositionMode.Absolute;
		_root.Style.Left = Length.Pixels( ThornsHudSafeZones.LevelUpToastLeftPx );
		_root.Style.Top = Length.Pixels( ThornsHudSafeZones.LevelUpToastTopPx );
		_root.Style.Width = Length.Pixels( ThornsHudSafeZones.LevelUpToastWidthPx );
		_root.Style.MaxWidth = Length.Pixels( ThornsHudSafeZones.LevelUpToastWidthPx );
		_root.Style.FlexDirection = FlexDirection.Column;
		_root.Style.PointerEvents = PointerEvents.None;
		_root.Style.Opacity = 0f;
		ThornsUiLayer.ApplyPassive( _root, ThornsUiPriority.Toast );
		_root.Style.Display = DisplayMode.None;

		var card = ThornsUiFactory.AddPanel( _root, "level-up-toast" );
		ThornsHudTheme.ApplyLevelUpToastPanel( card );

		var tag = ThornsUiFactory.AddPassiveLabel( card, "LEVEL UP", "level-up-toast-tag thorns-header" );
		tag.Style.LetterSpacing = Length.Pixels( 2 );
		tag.Style.FontSize = Length.Pixels( 10 );
		tag.Style.MarginBottom = Length.Pixels( 2 );

		_title = ThornsUiFactory.AddPassiveLabel( card, "Level 1", "level-up-toast-title thorns-accent" );
		_title.Style.FontSize = Length.Pixels( 16 );
		_title.Style.MarginBottom = Length.Pixels( 2 );

		var hint = ThornsUiFactory.AddPassiveLabel( card, "Skill point available — Tab → Skills", "level-up-toast-hint thorns-muted" );
		hint.Style.WhiteSpace = WhiteSpace.Normal;
		hint.Style.FontSize = Length.Pixels( 10 );
		hint.Style.LineHeight = Length.Pixels( 14 );
	}

	public void Show( int level )
	{
		if ( _title.IsValid )
			_title.Text = $"Level {Math.Max( 1, level )}";

		_phase = FadePhase.FadingIn;
		_pendingFadeIn = true;
		_visibleUntil = Time.Now + VisibleSeconds;
		if ( !_root.IsValid )
			return;

		_root.Style.Display = DisplayMode.Flex;
		_root.Style.Opacity = 0f;
	}

	public void Tick()
	{
		if ( _phase == FadePhase.Hidden )
			return;

		if ( _pendingFadeIn )
		{
			_pendingFadeIn = false;
			_root.Style.Opacity = 1f;
			_phase = FadePhase.Visible;
		}

		if ( _phase == FadePhase.FadingOut )
		{
			if ( Time.Now >= _fadeCompleteRealtime )
				FinishHide();
			return;
		}

		if ( Input.Pressed( "Attack1" ) || Input.Pressed( "Use" ) || Input.Pressed( "Tab" ) )
		{
			BeginFadeOut();
			return;
		}

		if ( Time.Now >= _visibleUntil )
			BeginFadeOut();
	}

	void BeginFadeOut()
	{
		if ( _phase == FadePhase.FadingOut || _phase == FadePhase.Hidden )
			return;

		_phase = FadePhase.FadingOut;
		_fadeCompleteRealtime = Time.Now + FadeSeconds;
		if ( _root.IsValid )
			_root.Style.Opacity = 0f;
	}

	void FinishHide()
	{
		_phase = FadePhase.Hidden;
		_pendingFadeIn = false;
		if ( _root.IsValid )
		{
			_root.Style.Display = DisplayMode.None;
			_root.Style.Opacity = 0f;
		}
	}
}
