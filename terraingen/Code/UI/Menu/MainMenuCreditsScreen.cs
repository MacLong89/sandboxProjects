namespace Terraingen.UI.Menu;

using Sandbox.UI;
using Terraingen.UI;

public sealed class MainMenuCreditsScreen : Panel
{
	public event Action MenuBackPressed;

	public MainMenuCreditsScreen( Panel parent )
	{
		parent.AddChild( this );
		AddClass( "mainmenu-overlay-screen" );
		ThornsTheme.ApplyGlassPanel( this );
		Style.FlexDirection = FlexDirection.Column;
		Style.Padding = Length.Pixels( 32 );
		Style.Margin = Length.Pixels( 80 );
		Style.MaxWidth = Length.Pixels( 640 );

		var head = ThornsUiFactory.AddPanel( this, "mainmenu-overlay-head" );
		head.Style.FlexDirection = FlexDirection.Row;
		ThornsUiFactory.AddLabel( head, "CREDITS", "mainmenu-overlay-title" );
		ThornsUiFactory.AddClickable( head, "mainmenu-back", "← Back", () => MenuBackPressed?.Invoke() );

		AddSection( "Team", "Thorns — Survive together." );
		AddSection( "Libraries", "s&box · Sandbox engine · Poppins UI font" );
		AddSection( "Special Thanks", "Playtesters, concept artists, and the s&box community." );
	}

	void AddSection( string title, string body )
	{
		ThornsUiFactory.AddLabel( this, title, "mainmenu-credits-heading" );
		ThornsUiFactory.AddLabel( this, body, "mainmenu-credits-body" );
	}
}
