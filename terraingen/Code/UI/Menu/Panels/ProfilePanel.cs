namespace Terraingen.UI.Menu.Panels;

using Sandbox.UI;
using Terraingen.UI;

public sealed class ProfilePanel : Panel
{
	Label _name;
	Label _level;
	Label _guild;

	public ProfilePanel( Panel parent )
	{
		parent.AddChild( this );
		AddClass( "mainmenu-profile" );
		ThornsTheme.ApplyGlassPanel( this );
		Style.FlexDirection = FlexDirection.Column;
		Style.Padding = Length.Pixels( 12 );
		Style.MinWidth = Length.Pixels( 200 );

		_name = ThornsUiFactory.AddLabel( this, "", "mainmenu-profile-name" );
		_level = ThornsUiFactory.AddLabel( this, "", "mainmenu-profile-level" );
		_guild = ThornsUiFactory.AddLabel( this, "", "mainmenu-profile-guild" );

		UiRevisionBus.MenuRevisionChanged += OnRevision;
		Refresh();
	}

	public override void OnDeleted()
	{
		UiRevisionBus.MenuRevisionChanged -= OnRevision;
		base.OnDeleted();
	}

	void OnRevision( UiRevisionChannel channel, int _ )
	{
		if ( channel == UiRevisionChannel.Menu || channel == UiRevisionChannel.Settings )
			Refresh();
	}

	public void Refresh()
	{
		ThornsMenuProfile.LoadCache();
		_name.Text = ThornsMenuProfile.DisplayName;
		_level.Text = $"LEVEL {Math.Max( 1, ThornsMenuProfile.Cache.PlayerLevel )}";

		if ( ThornsMenuProfile.Cache.InGuild && !string.IsNullOrWhiteSpace( ThornsMenuProfile.Cache.GuildName ) )
		{
			_guild.Text = ThornsMenuProfile.Cache.GuildName;
			_guild.SetClass( "mainmenu-hidden", false );
		}
		else
		{
			_guild.Text = "";
			_guild.SetClass( "mainmenu-hidden", true );
		}
	}
}
