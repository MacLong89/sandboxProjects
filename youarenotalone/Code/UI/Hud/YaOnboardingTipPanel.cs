using Sandbox.UI;

namespace Sandbox;

/// <summary>Center-screen first-run tip modal (Hide tips + Got it).</summary>
public sealed class YaOnboardingTipPanel : Panel
{
	public YaPlayerHud Hud { get; set; }

	Label _title;
	Label _body;
	Panel _hideBtn;
	Panel _gotItBtn;

	public YaOnboardingTipPanel()
	{
		AddClass( "ya-onboarding-tip" );
		Style.Display = DisplayMode.None;
		Style.Position = PositionMode.Absolute;
		Style.Left = 0;
		Style.Top = 0;
		Style.Width = Length.Fraction( 1f );
		Style.Height = Length.Fraction( 1f );
		Style.AlignItems = Align.Center;
		Style.JustifyContent = Justify.Center;
		Style.PointerEvents = PointerEvents.All;

		var card = AddChild<Panel>( "ya-onboarding-tip__card" );
		_title = card.AddChild( new Label( "", "ya-onboarding-tip__title" ) );
		_body = card.AddChild( new Label( "", "ya-onboarding-tip__body" ) );

		var row = card.AddChild<Panel>( "ya-onboarding-tip__row" );
		_hideBtn = row.AddChild( new OnboardingTipButtonPanel { Hud = this, HideAll = true } );
		_hideBtn.AddChild( new Label( "Hide tips", "ya-onboarding-tip__btn-label" ) );
		_gotItBtn = row.AddChild( new OnboardingTipButtonPanel { Hud = this, HideAll = false } );
		_gotItBtn.AddChild( new Label( "Got it", "ya-onboarding-tip__btn-label ya-onboarding-tip__btn-label--primary" ) );
	}

	public void Apply( YaOnboardingTipDef tip )
	{
		if ( tip is null )
		{
			Style.Display = DisplayMode.None;
			return;
		}

		Style.Display = DisplayMode.Flex;
		_title.Text = tip.Title;
		_body.Text = tip.Body;
	}

	sealed class OnboardingTipButtonPanel : Panel
	{
		public YaOnboardingTipPanel Hud { get; set; }
		public bool HideAll { get; set; }

		public OnboardingTipButtonPanel()
		{
			AddClass( "ya-onboarding-tip__btn" );
		}

		protected override void OnMouseDown( MousePanelEvent e )
		{
			base.OnMouseDown( e );
			if ( e.MouseButton != MouseButtons.Left )
				return;

			Hud?.Hud?.DismissOnboardingTip( HideAll );
		}
	}
}
