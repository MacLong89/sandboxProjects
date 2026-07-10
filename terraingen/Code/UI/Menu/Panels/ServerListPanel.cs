namespace Terraingen.UI.Menu.Panels;

using Sandbox.Network;
using Sandbox.UI;
using Terraingen.UI;

public sealed class ServerListPanel : Panel
{
	public event Action ServerSelectionChanged;
	public event Action ServerRefreshRequested;
	public event Action HostFromEmptyRequested;

	readonly Panel _tabs;
	readonly Panel _list;
	readonly TextEntry _search;
	readonly List<Panel> _rows = new();

	IReadOnlyList<LobbyInformation> _lobbies = Array.Empty<LobbyInformation>();
	IReadOnlyList<LobbyInformation> _filtered = Array.Empty<LobbyInformation>();
	int _selectedIndex = -1;

	public LobbyInformation SelectedLobby =>
		_selectedIndex >= 0 && _selectedIndex < _filtered.Count ? _filtered[_selectedIndex] : default;

	ThornsServerBrowserTab _tab = ThornsServerBrowserTab.Official;
	bool _autoSelectFirst = true;

	public ServerListPanel( Panel parent )
	{
		parent.AddChild( this );
		AddClass( "mainmenu-server-list-panel" );
		Style.FlexDirection = FlexDirection.Column;
		Style.FlexGrow = 1;
		Style.MinHeight = 0;

		_tabs = ThornsUiFactory.AddPanel( this, "mainmenu-browser-tabs" );
		_tabs.Style.FlexDirection = FlexDirection.Row;
		AddTab( "Official", ThornsServerBrowserTab.Official );
		AddTab( "Community", ThornsServerBrowserTab.Community );
		AddTab( "Favorites", ThornsServerBrowserTab.Favorites );
		AddTab( "Recent", ThornsServerBrowserTab.Recent );

		var searchRow = ThornsUiFactory.AddPanel( this, "mainmenu-search-row" );
		searchRow.Style.FlexDirection = FlexDirection.Row;
		_search = searchRow.AddChild( new TextEntry() );
		_search.AddClass( "mainmenu-search" );
		_search.Placeholder = "Search servers...";
		_search.AddEventListener( "onvaluechanged", OnSearchChanged );
		ThornsUiFactory.AddClickable( searchRow, "mainmenu-icon-btn", "↻", OnRefreshClicked );

		var header = ThornsUiFactory.AddPanel( this, "mainmenu-server-header" );
		header.Style.FlexDirection = FlexDirection.Row;
		ThornsUiFactory.AddPassiveLabel( header, "NAME", "col-name" );
		ThornsUiFactory.AddPassiveLabel( header, "PLAYERS", "col-players" );
		ThornsUiFactory.AddPassiveLabel( header, "PING", "col-ping" );
		ThornsUiFactory.AddPassiveLabel( header, "REGION", "col-region" );

		_list = ThornsUiFactory.AddPanel( this, "mainmenu-server-rows" );
		_list.Style.FlexDirection = FlexDirection.Column;
		_list.Style.FlexGrow = 1;
		_list.Style.Overflow = OverflowMode.Scroll;

		SetTab( ThornsServerBrowserTab.Community );
	}

	void OnSearchChanged() => FilterAndRebuildRows();
	void OnRefreshClicked() => ServerRefreshRequested?.Invoke();
	void OnHostFromEmptyClicked() => HostFromEmptyRequested?.Invoke();

	void AddTab( string label, ThornsServerBrowserTab tab )
	{
		_ = new ServerTabButton( _tabs, this, tab, label );
	}

	internal void SetTab( ThornsServerBrowserTab tab )
	{
		_tab = tab;
		UpdateTabStyles();
		FilterAndRebuildRows();
	}

	void UpdateTabStyles()
	{
		var tabs = new[] { ThornsServerBrowserTab.Official, ThornsServerBrowserTab.Community, ThornsServerBrowserTab.Favorites, ThornsServerBrowserTab.Recent };
		var i = 0;
		foreach ( var child in _tabs.Children )
		{
			if ( i >= tabs.Length )
				break;

			child.SetClass( "active", tabs[i] == _tab );
			i++;
		}
	}

	public void SetLobbies( IReadOnlyList<LobbyInformation> lobbies, bool autoSelectFirst = true )
	{
		_lobbies = lobbies ?? Array.Empty<LobbyInformation>();
		_autoSelectFirst = autoSelectFirst;
		FilterAndRebuildRows();
	}

	void FilterAndRebuildRows()
	{
		_filtered = ThornsServerBrowserService.Filter( _lobbies, _tab, _search?.Text ?? "" );
		RebuildRows();
	}

	void RebuildRows()
	{
		_rows.Clear();
		if ( _list is null || !_list.IsValid )
			return;

		_list.DeleteChildren();
		_selectedIndex = -1;

		if ( _filtered.Count == 0 )
		{
			if ( _lobbies.Count == 0 && string.IsNullOrWhiteSpace( _search?.Text ) )
			{
				var callout = ThornsUiFactory.AddPanel( _list, "mainmenu-empty-callout" );
				callout.Style.FlexDirection = FlexDirection.Column;
				callout.Style.AlignItems = Align.Center;
				ThornsUiFactory.AddPassiveLabel( callout, "No public servers online", "mainmenu-empty-callout-title" );
				ThornsUiFactory.AddPassiveLabel(
					callout,
					"Nobody is hosting a Thorns world right now. Host your own — it shows up in the browser so friends can join.",
					"mainmenu-empty-callout-body" );
				ThornsUiFactory.AddClickable( callout, "mainmenu-btn-primary mainmenu-empty-host-btn", "Host a World", OnHostFromEmptyClicked );
			}
			else
			{
				ThornsUiFactory.AddPassiveLabel( _list, "No servers match your search.", "mainmenu-empty" );
			}

			ServerSelectionChanged?.Invoke();
			return;
		}

		for ( var i = 0; i < _filtered.Count; i++ )
		{
			var lobby = _filtered[i];
			var row = new ServerRowPanel( _list, this, i );
			row.Style.FlexDirection = FlexDirection.Row;

			var name = string.IsNullOrWhiteSpace( lobby.Name ) ? "Unnamed Server" : lobby.Name;
			ThornsUiFactory.AddPassiveLabel( row, name, "col-name" );
			ThornsUiFactory.AddPassiveLabel( row, $"{lobby.Members}/{lobby.MaxMembers}", "col-players" );
			ThornsUiFactory.AddPassiveLabel( row, $"{lobby.Ping}ms", "col-ping" );

			var region = ThornsLobbyMetadata.GetRegion( lobby );
			if ( !string.IsNullOrEmpty( region ) )
				ThornsUiFactory.AddPassiveLabel( row, region, "col-region" );

			_rows.Add( row );
		}

		if ( _autoSelectFirst )
			SelectIndex( 0 );
		else
			ClearSelection();
	}

	internal void SelectIndex( int index )
	{
		if ( index < 0 || index >= _filtered.Count )
			return;

		_selectedIndex = index;
		for ( var i = 0; i < _rows.Count; i++ )
		{
			if ( _rows[i] is not null && _rows[i].IsValid )
				_rows[i].SetClass( "selected", i == index );
		}

		ServerSelectionChanged?.Invoke();
	}

	public void ClearSelection()
	{
		_selectedIndex = -1;
		foreach ( var row in _rows )
		{
			if ( row is not null && row.IsValid )
				row.SetClass( "selected", false );
		}
	}

	public IReadOnlyList<LobbyInformation> GetFilteredLobbies() => _filtered;

	sealed class ServerTabButton : ThornsClickPanel
	{
		readonly ServerListPanel _owner;
		readonly ThornsServerBrowserTab _tab;

		public ServerTabButton( Panel parent, ServerListPanel owner, ThornsServerBrowserTab tab, string label )
		{
			_owner = owner;
			_tab = tab;
			parent.AddChild( this );
			AddClass( "mainmenu-tab" );
			SetClick( OnClicked );
			ThornsUiFactory.AddPassiveLabel( this, label );
		}

		void OnClicked() => _owner.SetTab( _tab );
	}

	sealed class ServerRowPanel : ThornsClickPanel
	{
		readonly ServerListPanel _owner;
		readonly int _index;

		public ServerRowPanel( Panel parent, ServerListPanel owner, int index )
		{
			_owner = owner;
			_index = index;
			parent.AddChild( this );
			AddClass( "mainmenu-server-row" );
			SetClick( OnClicked );
		}

		void OnClicked() => _owner.SelectIndex( _index );
	}
}
