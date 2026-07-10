using Sandbox.UI;



namespace Sandbox;



/// <summary>Top-center role banner: Alone vs NotAlone (tactical teal theme).</summary>

public sealed class YaRolePanel : Panel

{

	readonly Label _label;



	public YaRolePanel()

	{

		AddClass( "ya-hud-role-panel" );

		Style.Width = Length.Fraction( 1f );

		Style.FlexDirection = FlexDirection.Column;

		Style.AlignItems = Align.Center;

		_label = AddChild( new Label( "", "ya-hud-role-panel__text" ) );

		_label.Style.FontSize = YaUiDesignTokens.TopStackRoleFontPx;

		_label.Style.FontWeight = 900;
		_label.Style.TextAlign = TextAlign.Center;
		_label.Style.Width = Length.Fraction( 1f );
		_label.Style.MaxWidth = Length.Pixels( YaUiDesignTokens.TopStackTextMaxWidthPx );

	}



	public void ApplyFromRole( YaPlayerRole role, bool spectatingInRound = false )

	{

		if ( spectatingInRound )

		{

			_label.Text = "You are Spectating";

			_label.Style.FontColor = YaHudTheme.TextSecondary;

			return;

		}



		if ( role == YaPlayerRole.Alone )

		{

			_label.Text = "You are Alone";

			_label.Style.FontColor = YaHudRoleTheme.Alone.Accent;

		}

		else if ( role == YaPlayerRole.NotAlone )

		{

			_label.Text = "You are Not Alone";

			_label.Style.FontColor = YaHudRoleTheme.Hunter.Accent;

		}

		else

		{

			_label.Text = "—";

			_label.Style.FontColor = YaHudTheme.TextMuted;

		}

	}

}


