namespace Terraingen.UI.Menu.Panels;

using Sandbox.UI;
using Terraingen.Multiplayer;
using Terraingen.UI;

public sealed class MyHostedServersPanel : Panel
{
	public event Action SelectionChanged;
	public event Action HostPressed;
	public event Action RemovePressed;

	readonly Panel _rows;
	readonly TextEntry _newName;
	readonly ThornsClickPanel _hostBtn;
	readonly ThornsClickPanel _removeBtn;
	readonly List<Panel> _rowPanels = new();

	string _selectedId;
	IReadOnlyList<ThornsHostedServerDto> _servers = Array.Empty<ThornsHostedServerDto>();

	public ThornsHostedServerDto SelectedServer =>
		string.IsNullOrEmpty( _selectedId ) ? null : ThornsHostedServerCatalog.GetById( _selectedId );

	public MyHostedServersPanel( Panel parent )
	{
		parent.AddChild( this );
		AddClass( "mainmenu-my-servers-panel" );
		ThornsTheme.ApplyParchmentCard( this );
		Style.FlexDirection = FlexDirection.Column;
		Style.FlexGrow = 1;
		Style.MinHeight = 0;
		Style.Padding = Length.Pixels( 14 );

		_rows = ThornsUiFactory.AddPanel( this, "mainmenu-my-servers-rows" );
		_rows.Style.FlexDirection = FlexDirection.Column;
		_rows.Style.FlexGrow = 1;
		_rows.Style.MinHeight = 0;
		_rows.Style.Overflow = OverflowMode.Scroll;

		var nameRow = ThornsUiFactory.AddPanel( this, "mainmenu-my-servers-create" );
		nameRow.Style.FlexDirection = FlexDirection.Row;
		nameRow.Style.FlexShrink = 0;
		nameRow.Style.MarginTop = Length.Pixels( 10 );

		_newName = nameRow.AddChild( new TextEntry() );
		_newName.AddClass( "mainmenu-search" );
		_newName.Placeholder = "New world name (optional)...";
		_newName.Style.FlexGrow = 1;

		var actions = ThornsUiFactory.AddPanel( this, "mainmenu-my-world-actions" );
		actions.Style.FlexDirection = FlexDirection.Column;
		actions.Style.FlexShrink = 0;
		actions.Style.MarginTop = Length.Pixels( 12 );

		_removeBtn = ThornsUiFactory.AddClickable( actions, "mainmenu-btn-secondary", "Remove World", OnRemoveClicked );
		_hostBtn = ThornsUiFactory.AddClickable( actions, "mainmenu-btn-primary", "Host World", OnHostClicked );
	}

	void OnHostClicked() => HostPressed?.Invoke();
	void OnRemoveClicked() => RemovePressed?.Invoke();

	public void BindSelection( ThornsHostedServerDto server, bool hasSelection )
	{
		if ( !IsValid )
			return;

		var canRemove = hasSelection && server is not null;
		_removeBtn.SetClass( "disabled", !canRemove );
		_hostBtn.SetClass( "disabled", false );
	}

	public string GetNewNameDraft() => _newName?.Text?.Trim() ?? "";

	public void ClearNewNameDraft()
	{
		if ( _newName is not null && _newName.IsValid )
			_newName.Text = "";
	}

	public void RefreshList()
	{
		ThornsHostedServerCatalog.Load();
		_servers = ThornsHostedServerCatalog.ListOrdered();
		RebuildRows();
		BindSelection( SelectedServer, SelectedServer is not null );
	}

	void RebuildRows()
	{
		_rowPanels.Clear();
		_rows.DeleteChildren();

		if ( _servers.Count == 0 )
		{
			_selectedId = null;
			ThornsUiFactory.AddPassiveLabel( _rows, "Enter a world name below and press Host World.", "mainmenu-empty" );
			SelectionChanged?.Invoke();
			return;
		}

		foreach ( var server in _servers )
		{
			var row = new HostedServerRowPanel( _rows, this, server.Id );
			row.Style.FlexDirection = FlexDirection.Column;

			ThornsUiFactory.AddPassiveLabel( row, server.DisplayName, "mainmenu-my-server-name" );

			var meta = ThornsHostedServerCatalog.SaveExists( server ) ? "Save on disk" : "New world";
			ThornsUiFactory.AddPassiveLabel( row, meta, "mainmenu-my-server-meta" );

			_rowPanels.Add( row );
		}

		if ( string.IsNullOrEmpty( _selectedId ) || ThornsHostedServerCatalog.GetById( _selectedId ) is null )
			Select( _servers[0].Id );
		else
			HighlightSelection();
	}

	internal void Select( string id )
	{
		if ( string.IsNullOrEmpty( id ) )
			return;

		_selectedId = id;
		HighlightSelection();
		BindSelection( SelectedServer, true );
		SelectionChanged?.Invoke();
	}

	void HighlightSelection()
	{
		for ( var i = 0; i < _rowPanels.Count && i < _servers.Count; i++ )
		{
			if ( _rowPanels[i] is not null && _rowPanels[i].IsValid )
				_rowPanels[i].SetClass( "selected", _servers[i].Id == _selectedId );
		}
	}

	public void ClearSelection()
	{
		_selectedId = null;
		HighlightSelection();
		BindSelection( null, false );
	}

	sealed class HostedServerRowPanel : ThornsClickPanel
	{
		readonly MyHostedServersPanel _owner;
		readonly string _serverId;

		public HostedServerRowPanel( Panel parent, MyHostedServersPanel owner, string serverId )
		{
			_owner = owner;
			_serverId = serverId;
			parent.AddChild( this );
			AddClass( "mainmenu-my-server-row" );
			SetClick( OnClicked );
		}

		void OnClicked() => _owner.Select( _serverId );
	}
}
