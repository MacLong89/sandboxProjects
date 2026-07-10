namespace Terraingen.UI.Menu;



using System.Threading.Tasks;

using Sandbox.UI;

using Terraingen.Multiplayer;

using Terraingen.UI;

using Terraingen.UI.Menu.Panels;



/// <summary>Two-column browser: public servers (left), my worlds + host panel (right).</summary>

public sealed class ServerBrowserScreen : Panel

{

	public event Action MenuBackPressed;



	readonly MyHostedServersPanel _myServers;

	readonly ServerListPanel _list;

	readonly ServerDetailsPanel _publicDetails;

	readonly MainMenuWorldNamePrompt _worldNamePrompt;
	readonly MainMenuConfirmPrompt _confirmPrompt;

	Label _status;

	TextEntry _password;

	Panel _passwordRow;



	IReadOnlyList<Sandbox.Network.LobbyInformation> _allLobbies = Array.Empty<Sandbox.Network.LobbyInformation>();

	bool _showingHostedDetails;



	public ServerBrowserScreen( Panel parent, MainMenuWorldNamePrompt worldNamePrompt, MainMenuConfirmPrompt confirmPrompt )

	{
		_worldNamePrompt = worldNamePrompt;
		_confirmPrompt = confirmPrompt;

		AddClass( "mainmenu-browser-screen mainmenu-browser-parchment thorns-menu-overlay-fantasy-textured" );
		ThornsMainMenuBackdrop.ApplyTabMenuBackdrop( this );

		Style.FlexDirection = FlexDirection.Column;

		Style.FlexGrow = 1;

		Style.MinHeight = 0;

		Style.Padding = Length.Pixels( 24 );



		var head = ThornsUiFactory.AddPanel( this, "mainmenu-browser-head" );

		head.Style.FlexDirection = FlexDirection.Row;

		head.Style.AlignItems = Align.Center;

		head.Style.FlexShrink = 0;

		ThornsUiFactory.AddLabel( head, "SERVER BROWSER", "mainmenu-browser-title" );

		ThornsUiFactory.AddClickable( head, "mainmenu-back", "← Back", OnBackClicked );



		_status = ThornsUiFactory.AddLabel( this, "", "mainmenu-browser-status" );



		var body = ThornsUiFactory.AddPanel( this, "mainmenu-browser-body" );

		body.Style.FlexDirection = FlexDirection.Row;

		body.Style.FlexGrow = 1;

		body.Style.MinHeight = 0;

		body.Style.MinWidth = 0;

		body.Style.Overflow = OverflowMode.Hidden;



		// Right (host) column first in DOM so it stays visible when horizontal space is tight.

		var rightCol = ThornsUiFactory.AddPanel( body, "mainmenu-browser-right-col" );

		rightCol.Style.FlexDirection = FlexDirection.Column;

		rightCol.Style.FlexGrow = 0;

		rightCol.Style.FlexShrink = 0;

		rightCol.Style.Width = Length.Pixels( 360 );

		rightCol.Style.MinWidth = Length.Pixels( 320 );

		rightCol.Style.MaxWidth = Length.Pixels( 420 );



		ThornsUiFactory.AddPassiveLabel( rightCol, "MY WORLDS", "mainmenu-browser-col-title" );

		_myServers = new MyHostedServersPanel( rightCol );

		_myServers.SelectionChanged += OnMyServerSelectionChanged;
		_myServers.HostPressed += OnHostPressed;
		_myServers.RemovePressed += OnRemoveHostedPressed;



		// Left: other players' servers + join details

		var leftCol = ThornsUiFactory.AddPanel( body, "mainmenu-browser-left-col" );

		leftCol.Style.FlexDirection = FlexDirection.Column;

		leftCol.Style.FlexGrow = 1;

		leftCol.Style.FlexShrink = 1;

		leftCol.Style.MinWidth = Length.Pixels( 280 );



		ThornsUiFactory.AddPassiveLabel( leftCol, "PUBLIC SERVERS", "mainmenu-browser-col-title" );

		var publicCard = ThornsUiFactory.AddPanel( leftCol, "mainmenu-public-card" );
		ThornsTheme.ApplyParchmentCard( publicCard );
		publicCard.Style.FlexDirection = FlexDirection.Row;
		publicCard.Style.FlexGrow = 1;
		publicCard.Style.MinHeight = 0;
		publicCard.Style.MinWidth = 0;
		publicCard.Style.Overflow = OverflowMode.Hidden;
		publicCard.Style.Padding = Length.Pixels( 12 );

		var listSection = ThornsUiFactory.AddPanel( publicCard, "mainmenu-public-list-section" );
		listSection.Style.FlexDirection = FlexDirection.Column;
		listSection.Style.FlexGrow = 1;
		listSection.Style.MinWidth = 0;
		listSection.Style.MinHeight = 0;

		_list = new ServerListPanel( listSection );
		_list.ServerRefreshRequested += OnServerRefreshRequested;
		_list.ServerSelectionChanged += OnPublicServerSelectionChanged;
		_list.HostFromEmptyRequested += OnHostFromEmptyRequested;

		var divider = ThornsUiFactory.AddPanel( publicCard, "mainmenu-public-divider" );

		var detailSection = ThornsUiFactory.AddPanel( publicCard, "mainmenu-public-detail-section" );
		detailSection.Style.FlexDirection = FlexDirection.Column;
		detailSection.Style.FlexShrink = 0;
		detailSection.Style.Width = Length.Pixels( 300 );
		detailSection.Style.MinHeight = 0;

		_passwordRow = ThornsUiFactory.AddPanel( detailSection, "mainmenu-password-row" );
		_passwordRow.Style.FlexDirection = FlexDirection.Row;
		_passwordRow.Style.FlexShrink = 0;
		_passwordRow.SetClass( "mainmenu-hidden", true );
		_password = _passwordRow.AddChild( new TextEntry() );
		_password.AddClass( "mainmenu-search" );
		_password.Placeholder = "Join password";

		_publicDetails = new ServerDetailsPanel( detailSection, embedded: true );
		_publicDetails.JoinPressed += OnJoinPressed;
		_publicDetails.FavoritePressed += OnFavoriteToggle;



		ThornsMenuServerPrefs.Load();

		ThornsHostedServerCatalog.Load();

		_myServers.RefreshList();

		_showingHostedDetails = _myServers.SelectedServer is not null;

		UpdateDetails();

		parent.AddChild( this );
	}



	public override void OnDeleted()
	{
		if ( _list is not null && _list.IsValid )
		{
			_list.ServerRefreshRequested -= OnServerRefreshRequested;
			_list.ServerSelectionChanged -= OnPublicServerSelectionChanged;
			_list.HostFromEmptyRequested -= OnHostFromEmptyRequested;
		}

		if ( _publicDetails is not null && _publicDetails.IsValid )
		{
			_publicDetails.JoinPressed -= OnJoinPressed;
			_publicDetails.FavoritePressed -= OnFavoriteToggle;
		}

		if ( _myServers is not null && _myServers.IsValid )
		{
			_myServers.SelectionChanged -= OnMyServerSelectionChanged;
			_myServers.HostPressed -= OnHostPressed;
			_myServers.RemovePressed -= OnRemoveHostedPressed;
		}

		base.OnDeleted();
	}



	void OnBackClicked() => MenuBackPressed?.Invoke();

	void OnHostFromEmptyRequested() => _ = HostSelectedAsync();

	void OnCreateWorldRequested() =>
		ShowWorldNamePrompt(
			"CREATE WORLD",
			"Choose a name for your new world.",
			"Create",
			_myServers.GetNewNameDraft(),
			CreateWorldFromName );

	void ShowWorldNamePrompt(
		string title,
		string hint,
		string confirmLabel,
		string initialName,
		Action<string> onNamed )
	{
		if ( _worldNamePrompt is null )
		{
			_status.Text = "Enter a name for your new world.";
			return;
		}

		_worldNamePrompt.Show( title, hint, confirmLabel, "World name...", initialName, onNamed );
	}

	void CreateWorldFromName( string name )
	{
		var created = ThornsHostedServerCatalog.CreateNew( name );
		_myServers.ClearNewNameDraft();
		_myServers.RefreshList();
		_myServers.Select( created.Id );
		_showingHostedDetails = true;
		_status.Text = $"Created \"{created.DisplayName}\".";
		UpdateDetails();
	}

	void OnServerRefreshRequested() => _ = RefreshAsyncSafe();

	void OnJoinPressed() => _ = JoinSelectedAsync();

	void OnHostPressed() => _ = HostSelectedAsync();



	public Task RefreshAsyncSafe() => RefreshAsync();



	public async Task RefreshAsync()

	{

		try

		{

			_status.Text = "Searching...";

			_allLobbies = await ThornsServerBrowserService.QueryLobbiesAsync();

			_list.SetLobbies( _allLobbies, autoSelectFirst: !_showingHostedDetails );

			if ( _allLobbies.Count == 0 )
				_status.Text = "No public servers online — host your own world on the right.";
			else
				_status.Text = _allLobbies.Count == 1 ? "1 server found." : $"{_allLobbies.Count} servers found.";

		}

		catch ( Exception e )

		{

			Log.Warning( e, "[Thorns Menu] Server browser refresh failed." );

			_status.Text = "Server search failed.";

		}

	}



	void OnMyServerSelectionChanged()

	{

		_showingHostedDetails = _myServers.SelectedServer is not null;

		if ( _showingHostedDetails )

			_list.ClearSelection();



		UpdateDetails();

	}



	void OnPublicServerSelectionChanged()

	{

		if ( ThornsLobbyUtil.IsValid( _list.SelectedLobby ) )

		{

			_showingHostedDetails = false;

			_myServers.ClearSelection();

		}



		UpdateDetails();

	}



	void UpdateDetails()

	{

		if ( _showingHostedDetails )

		{

			var server = _myServers.SelectedServer;

			_myServers.BindSelection( server, server is not null );

			_passwordRow.SetClass( "mainmenu-hidden", true );



			var selected = _list.SelectedLobby;

			var hasPublic = ThornsLobbyUtil.IsValid( selected );

			_publicDetails.Bind( selected, hasPublic );

			return;

		}



		var lobby = _list.SelectedLobby;

		var hasLobby = ThornsLobbyUtil.IsValid( lobby );

		_publicDetails.Bind( lobby, hasLobby );

		_myServers.BindSelection( null, false );



		var needsPassword = hasLobby && ThornsLobbyPasswordGate.LobbyRequiresPassword( lobby );

		_passwordRow.SetClass( "mainmenu-hidden", !needsPassword );

	}



	async Task HostSelectedAsync()
	{
		var draftName = _myServers.GetNewNameDraft();
		var server = _myServers.SelectedServer;

		if ( !string.IsNullOrWhiteSpace( draftName ) )
		{
			server = ThornsHostedServerCatalog.CreateNew( draftName );
			_myServers.ClearNewNameDraft();
			_myServers.RefreshList();
			_myServers.Select( server.Id );
			_showingHostedDetails = true;
			UpdateDetails();
			await HostServerAsync( server );
			return;
		}

		if ( server is null )
		{
			ShowWorldNamePrompt(
				"HOST WORLD",
				"Name your world, then hosting will start.",
				"Host",
				"",
				name =>
				{
					var created = ThornsHostedServerCatalog.CreateNew( name );
					_myServers.ClearNewNameDraft();
					_myServers.RefreshList();
					_myServers.Select( created.Id );
					_showingHostedDetails = true;
					UpdateDetails();
					_ = HostServerAsync( created );
				} );
			return;
		}

		await HostServerAsync( server );
	}

	async Task HostServerAsync( ThornsHostedServerDto server )
	{
		if ( server is null )
			return;

		try
		{
			ThornsMenuJoinFlow.SetProgressMessage( "Starting Lobby..." );
			await ThornsServerBrowserService.HostLocalServerAsync( server );
		}
		catch ( Exception e )
		{
			Log.Warning( e, "[Thorns Menu] Host failed." );
			ThornsMenuJoinFlow.ResetForMainMenu();
			_status.Text = "Host failed.";
		}
	}



	async Task JoinSelectedAsync()

	{

		var selected = _list.SelectedLobby;

		if ( !ThornsLobbyUtil.IsValid( selected ) )

			return;



		try

		{

			var (ok, error) = await ThornsServerBrowserService.TryJoinAsync( selected, _password?.Text ?? "" );

			if ( !ok )
			{
				ThornsMenuJoinFlow.ResetForMainMenu();
				_status.Text = string.IsNullOrWhiteSpace( error ) ? "Join failed." : error;
			}

		}

		catch ( Exception e )

		{

			Log.Warning( e, "[Thorns Menu] Join failed." );

			ThornsMenuJoinFlow.ResetForMainMenu();
			_status.Text = "Join failed.";

		}

	}



	void OnFavoriteToggle()

	{

		var lobby = _publicDetails.CurrentLobby;

		if ( !ThornsLobbyUtil.IsValid( lobby ) )

			return;



		ThornsMenuServerPrefs.ToggleFavorite( lobby.LobbyId.ToString() );

		_publicDetails.Bind( lobby, true );

		_list.SetLobbies( _allLobbies );

	}



	void OnRemoveHostedPressed()
	{
		var server = _myServers.SelectedServer;
		if ( server is null )
			return;

		if ( _confirmPrompt is null )
		{
			ConfirmRemoveHostedWorld( server );
			return;
		}

		var displayName = string.IsNullOrWhiteSpace( server.DisplayName ) ? "this world" : $"\"{server.DisplayName}\"";
		_confirmPrompt.Show(
			"ARE YOU SURE?",
			$"This will permanently delete {displayName} and its save data. This cannot be undone.",
			"Remove World",
			() => ConfirmRemoveHostedWorld( server ) );
	}

	void ConfirmRemoveHostedWorld( ThornsHostedServerDto server )
	{
		if ( server is null )
			return;

		if ( !ThornsHostedServerCatalog.TryRemove( server.Id, out var err ) )
		{
			_status.Text = string.IsNullOrWhiteSpace( err ) ? "Could not delete world." : err;
			return;
		}

		_myServers.RefreshList();
		_showingHostedDetails = _myServers.SelectedServer is not null;
		_status.Text = "World deleted.";
		UpdateDetails();
	}

}

