namespace Terraingen.UI.Menu.Panels;

using Sandbox.Network;
using Sandbox.UI;
using Terraingen.Multiplayer;
using Terraingen.UI;

public sealed class ServerDetailsPanel : Panel
{
	public event Action JoinPressed;
	public event Action FavoritePressed;

	Label _title;
	Label _description;
	Label _players;
	Label _ping;
	Label _region;
	Label _time;
	Label _weather;
	Panel _preview;
	ThornsClickPanel _joinBtn;
	ThornsClickPanel _favoriteBtn;

	LobbyInformation _lobby;

	public ServerDetailsPanel( Panel parent, bool embedded = false )
	{
		parent.AddChild( this );
		AddClass( "mainmenu-server-details" );
		if ( embedded )
			AddClass( "mainmenu-server-details-embedded" );
		else
			ThornsTheme.ApplyGlassPanel( this );

		Style.FlexDirection = FlexDirection.Column;
		Style.Padding = Length.Pixels( embedded ? 0 : 18 );
		Style.FlexGrow = embedded ? 0 : 1;
		Style.FlexShrink = 0;
		Style.Height = embedded ? Length.Percent( 100 ) : Length.Auto;

		_title = ThornsUiFactory.AddLabel( this, "Select a server", "mainmenu-detail-title" );
		_preview = ThornsUiFactory.AddPanel( this, "mainmenu-detail-preview" );
		_preview.Style.MinHeight = Length.Pixels( embedded ? 0 : 160 );
		_preview.Style.FlexShrink = 0;
		if ( embedded )
			_preview.Style.Display = DisplayMode.None;
		_description = ThornsUiFactory.AddLabel( this, "", "mainmenu-detail-desc" );
		if ( embedded )
			_description.Style.Display = DisplayMode.None;

		var stats = ThornsUiFactory.AddPanel( this, "mainmenu-detail-stats" );
		stats.Style.FlexDirection = FlexDirection.Row;
		stats.Style.FlexWrap = Wrap.Wrap;
		_players = ThornsUiFactory.AddLabel( stats, "", "mainmenu-stat" );
		_ping = ThornsUiFactory.AddLabel( stats, "", "mainmenu-stat" );
		_region = ThornsUiFactory.AddLabel( stats, "", "mainmenu-stat" );
		_time = ThornsUiFactory.AddLabel( stats, "", "mainmenu-stat" );
		_weather = ThornsUiFactory.AddLabel( stats, "", "mainmenu-stat" );

		if ( !embedded )
		{
			var spacer = ThornsUiFactory.AddPanel( this, "mainmenu-spacer" );
			spacer.Style.FlexGrow = 1;
		}

		var actions = ThornsUiFactory.AddPanel( this, "mainmenu-detail-actions" );
		actions.Style.MarginTop = embedded ? Length.Pixels( 8 ) : Length.Auto;
		actions.Style.FlexDirection = FlexDirection.Row;
		actions.Style.FlexWrap = Wrap.Wrap;
		_favoriteBtn = ThornsUiFactory.AddClickable( actions, "mainmenu-btn-secondary", "☆ Favorite", OnFavoriteClick );
		_joinBtn = ThornsUiFactory.AddClickable( actions, "mainmenu-btn-primary", "Join Server", OnJoinClick );
	}

	public void Bind( LobbyInformation lobby, bool hasSelection )
	{
		if ( !IsValid )
			return;

		_lobby = lobby;
		if ( !hasSelection )
		{
			_title.Text = "Select a server";
			_description.Text = "";
			_players.Text = _ping.Text = _region.Text = _time.Text = _weather.Text = "";
			_joinBtn.SetClass( "disabled", true );
			_preview.Style.BackgroundImage = null;
			return;
		}

		_title.Text = string.IsNullOrWhiteSpace( lobby.Name ) ? "Unnamed Server" : lobby.Name;
		var biome = ThornsLobbyMetadata.GetBiome( lobby );
		_description.Text = ThornsLobbyMetadata.IsOfficial( lobby )
			? "Official Thorns survival world."
			: "Community-hosted Thorns world.";

		_players.Text = $"Players {lobby.Members}/{lobby.MaxMembers}";
		_ping.Text = $"Ping {lobby.Ping}ms";
		var region = ThornsLobbyMetadata.GetRegion( lobby );
		_region.Text = string.IsNullOrEmpty( region ) ? "" : $"Region {region}";
		_region.SetClass( "mainmenu-hidden", string.IsNullOrEmpty( region ) );

		_time.Text = $"Time {DateTime.Now:HH:mm}";
		_weather.Text = "Weather Clear";
		_joinBtn.SetClass( "disabled", lobby.IsFull );

		ThornsServerBiomePreview.ApplyToPanel( _preview, biome );
		UpdateFavoriteLabel();
	}

	void UpdateFavoriteLabel()
	{
		if ( !ThornsLobbyUtil.IsValid( _lobby ) || _favoriteBtn is null || !_favoriteBtn.IsValid )
			return;

		var fav = ThornsMenuServerPrefs.IsFavorite( _lobby.LobbyId.ToString() );
		foreach ( var child in _favoriteBtn.Children.ToArray() )
			child.Delete();
		ThornsUiFactory.AddPassiveLabel( _favoriteBtn, fav ? "★ Favorited" : "☆ Favorite", "mainmenu-btn-label" );
	}

	void OnJoinClick() => JoinPressed?.Invoke();
	void OnFavoriteClick() => FavoritePressed?.Invoke();

	public LobbyInformation CurrentLobby => _lobby;
}
