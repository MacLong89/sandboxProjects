namespace Terraingen.UI.Screens;

using Sandbox.UI;
using Terraingen.GameData;
using Terraingen.Player;
using Terraingen.UI;
using Terraingen.UI.Components;
using Terraingen.UI.Core;

public sealed partial class ThornsTamesScreen : ThornsScreenBase
{
	static readonly Color HealthFill = new( 0.82f, 0.22f, 0.18f );
	static readonly Color ExpFill = new( 0.18f, 0.16f, 0.14f );

	Panel _list;
	Panel _right;
	Label _capacityLabel;
	Panel _attributesPanel;
	Panel _traitsPanel;
	Panel _commands;
	Label _heroName;
	Label _heroTier;
	Label _heroLevel;
	TextEntry _renameEntry;
	Guid _renameEntityId;
	Panel _feedNoticeAnchor;
	Label _feedNotice;

	public ThornsTamesScreen( ThornsMenuHost host, Panel parent ) : base( host, parent ) { }

	protected override void Build()
	{
		BuildTamesConceptLayout();
	}

	protected override void OnRevision( UiRevisionChannel channel, int revision )
	{
		_ = revision;
		if ( channel == UiRevisionChannel.TameFeedNotice )
		{
			RefreshFeedNotice();
			return;
		}

		if ( channel == UiRevisionChannel.Tames )
			Rebuild();
	}

	public override void OnShown()
	{
		base.OnShown();
		RefreshFeedNotice();
		_ = DeferredRelayoutAfterShowAsync();
	}

	async System.Threading.Tasks.Task DeferredRelayoutAfterShowAsync()
	{
		await System.Threading.Tasks.Task.Yield();
		await System.Threading.Tasks.Task.Yield();
		if ( !IsValid )
			return;

		RebuildCommands();
	}

	public override void Rebuild()
	{
		if ( !ThornsUiClientState.HasSnapshot )
			return;

		var snap = ThornsUiClientState.Snapshot.Tames;
		if ( snap.Tames.Count > 0 && snap.SelectedEntityId == Guid.Empty )
			snap.SelectedEntityId = snap.Tames[0].EntityId;

		RebuildListFooter();
		RebuildList();
		RebuildDetail();
		RebuildCommands();
		RebuildBreedSubmenu();
	}

	void RebuildListFooter()
	{
		if ( _listFooter is null || !_listFooter.IsValid )
			return;

		_listFooter.DeleteChildren( true );
		var snap = ThornsUiClientState.Snapshot.Tames;

		var breedBtn = ThornsUiFactory.AddClickable(
			_listFooter,
			"thorns-btn-primary tame-breed-btn",
			snap.BreedPanelOpen ? "CLOSE" : "BREED",
			ToggleBreedSubmenu );
		breedBtn.Style.Width = Length.Percent( 100 );
		breedBtn.Style.MinHeight = Length.Pixels( 40 );
		breedBtn.Style.JustifyContent = Justify.Center;
		breedBtn.Style.AlignItems = Align.Center;
	}

	void RebuildList()
	{
		if ( _list is null || !_list.IsValid )
			return;

		_list.DeleteChildren( true );

		var snap = ThornsUiClientState.Snapshot.Tames;
		if ( _capacityLabel is not null && _capacityLabel.IsValid )
			_capacityLabel.Text = $"{snap.Tames.Count} / {ThornsTameCatalog.MaxTameSlots}";

		if ( snap.Tames.Count == 0 )
		{
			ThornsTheme.CreateMuted( _list, "You don't have any pets yet. Tame wild animals in the world to add companions." );
			return;
		}

		var selectedId = snap.SelectedEntityId == Guid.Empty ? snap.Tames[0].EntityId : snap.SelectedEntityId;

		for ( var i = 0; i < ThornsTameCatalog.MaxTameSlots; i++ )
		{
			if ( i >= snap.Tames.Count )
			{
				var locked = ThornsUiFactory.AddPanel( _list, "tame-slot-card tame-slot-locked tames-list-card" );
				locked.Style.FlexDirection = FlexDirection.Row;
				locked.Style.AlignItems = Align.Center;
				ThornsUiFactory.AddPassiveLabel( locked, "🔒", "tame-lock-icon" );
				var lockBody = ThornsUiFactory.AddPanel( locked, "tame-card-body" );
				lockBody.Style.FlexDirection = FlexDirection.Column;
				lockBody.Style.FlexGrow = 1;
				lockBody.Style.MinWidth = Length.Pixels( 0 );
				ThornsUiFactory.AddPassiveLabel( lockBody, "LOCKED SLOT", "tame-card-title" );
				ThornsUiFactory.AddPassiveLabel( lockBody, "Tame more animals to unlock", "tame-card-sub" );
				continue;
			}

			var tame = snap.Tames[i];
			var captured = tame;
			var card = ThornsUiFactory.AddClickable( _list, "tame-slot-card tames-list-card",
				() => Host.SetSelectedTame( captured.EntityId ) );
			card.SetClass( "selected", tame.EntityId == selectedId );
			card.Style.FlexDirection = FlexDirection.Row;
			card.Style.AlignItems = Align.FlexStart;
			card.Style.Overflow = OverflowMode.Hidden;

			var portrait = ThornsUiFactory.AddPanel( card, "tame-portrait tame-list-portrait slot-icon" );
			portrait.Style.Width = Length.Pixels( 72 );
			portrait.Style.Height = Length.Pixels( 72 );
			portrait.Style.FlexShrink = 0;
			portrait.Style.MarginRight = Length.Pixels( 10 );
			portrait.Style.PointerEvents = PointerEvents.None;
			ThornsIconCache.ApplyToPanel( portrait, tame.PortraitPath );

			var body = ThornsUiFactory.AddPanel( card, "tame-card-body tame-list-card-body" );
			body.Style.FlexDirection = FlexDirection.Column;
			body.Style.FlexGrow = 1;
			body.Style.MinWidth = Length.Pixels( 0 );
			body.Style.Overflow = OverflowMode.Hidden;

			var titleRow = ThornsUiFactory.AddPanel( body, "tame-card-title-row" );
			titleRow.Style.FlexDirection = FlexDirection.Row;
			titleRow.Style.AlignItems = Align.Center;
			titleRow.Style.JustifyContent = Justify.SpaceBetween;
			titleRow.Style.Width = Length.Percent( 100 );
			titleRow.Style.MinWidth = Length.Pixels( 0 );
			titleRow.Style.Overflow = OverflowMode.Hidden;
			titleRow.Style.MarginBottom = Length.Pixels( 2 );

			var nameLabel = ThornsUiFactory.AddPassiveLabel( titleRow, tame.DisplayName.ToUpperInvariant(), "tame-card-title tame-slot-name" );
			nameLabel.Style.FlexGrow = 1;
			nameLabel.Style.FlexShrink = 1;
			nameLabel.Style.MinWidth = Length.Pixels( 0 );

			AddInlineTierLabel( titleRow, tame.Tier );

			ThornsUiFactory.AddPassiveLabel( body, $"Level {Math.Max( 1, tame.Level )}", "tame-card-level" );

			var xpCurrent = Math.Max( 0, tame.CurrentExperience );
			var xpMax = Math.Max( 1, tame.ExperienceToNextLevel );
			ThornsUiFactory.AddPassiveLabel( body, $"XP: {xpCurrent} / {xpMax}", "tame-card-xp-label" );
			var xpTrack = ThornsUiFactory.AddPanel( body, "tame-card-xp-track" );
			var xpFill = ThornsUiFactory.AddPanel( xpTrack, "tame-card-xp-fill" );
			var xpFrac = xpMax > 0 ? Math.Clamp( xpCurrent / (float)xpMax, 0f, 1f ) : 0f;
			xpFill.Style.Width = Length.Percent( xpFrac * 100f );

			var hpRow = ThornsUiFactory.AddPanel( body, "tame-card-hp-row" );
			hpRow.Style.FlexDirection = FlexDirection.Row;
			hpRow.Style.AlignItems = Align.Center;
			hpRow.Style.Width = Length.Percent( 100 );
			ThornsUiFactory.AddPassiveLabel( hpRow, "♥", "tame-card-hp-icon" );
			ThornsUiFactory.AddPassiveLabel( hpRow, $"{tame.CurrentHealth:0} / {tame.MaxHealth:0}", "tame-card-hp-text" );

			var hpTrack = ThornsUiFactory.AddPanel( body, "tame-card-hp-track" );
			var hpFill = ThornsUiFactory.AddPanel( hpTrack, "tame-card-hp-fill" );
			var maxHp = Math.Max( 1f, tame.MaxHealth );
			hpFill.Style.Width = Length.Percent( Math.Clamp( tame.CurrentHealth / maxHp, 0f, 1f ) * 100f );
		}
	}

	void RebuildDetail()
	{
		_attributesPanel?.DeleteChildren( true );
		_traitsPanel?.DeleteChildren( true );

		var snap = ThornsUiClientState.Snapshot.Tames;
		var tame = snap.Tames.FirstOrDefault( t => t.EntityId == snap.SelectedEntityId )
		           ?? snap.Tames.FirstOrDefault();

		if ( tame is null )
		{
			ClearDetailPanel();
			return;
		}

		if ( _detailPortrait is not null && _detailPortrait.IsValid )
			ThornsIconCache.ApplyToPanel( _detailPortrait, tame.PortraitPath );
		if ( _detailSpeciesIcon is not null && _detailSpeciesIcon.IsValid )
			ThornsIconCache.ApplyToPanel( _detailSpeciesIcon, tame.PortraitPath );

		if ( _heroName is not null && _heroName.IsValid )
			_heroName.Text = tame.DisplayName.ToUpperInvariant();
		ApplyTierLabel( _heroTier, tame.Tier );
		if ( _heroLevel is not null && _heroLevel.IsValid )
			_heroLevel.Text = $"Level {Math.Max( 1, tame.Level )}";

		if ( _renameEntry is not null && _renameEntry.IsValid && _renameEntityId != tame.EntityId )
		{
			_renameEntityId = tame.EntityId;
			_renameEntry.Text = tame.DisplayName;
		}
		else if ( _renameEntry is not null && _renameEntry.IsValid && _renameEntityId == Guid.Empty )
		{
			_renameEntityId = tame.EntityId;
			_renameEntry.Text = tame.DisplayName;
		}

		var availableXp = tame.UnspentStatPoints;
		if ( _detailAvailableXpValue is not null && _detailAvailableXpValue.IsValid )
			_detailAvailableXpValue.Text = availableXp.ToString();

		var maxHp = Math.Max( 1f, tame.MaxHealth );
		if ( _detailHealthLabel is not null && _detailHealthLabel.IsValid )
			_detailHealthLabel.Text = $"{tame.CurrentHealth:0} / {tame.MaxHealth:0}";
		if ( _detailHealthFill is not null && _detailHealthFill.IsValid )
		{
			_detailHealthFill.Style.Width = Length.Percent( Math.Clamp( tame.CurrentHealth / maxHp, 0f, 1f ) * 100f );
			_detailHealthFill.Style.BackgroundColor = HealthFill;
		}

		var xpCurrent = Math.Max( 0, tame.CurrentExperience );
		var xpMax = Math.Max( 1, tame.ExperienceToNextLevel );
		if ( _detailExpLevelLabel is not null && _detailExpLevelLabel.IsValid )
			_detailExpLevelLabel.Text = $"Level {Math.Max( 1, tame.Level )}";
		if ( _detailExpLabel is not null && _detailExpLabel.IsValid )
			_detailExpLabel.Text = $"{xpCurrent:N0} / {xpMax:N0} XP";
		if ( _detailExpFill is not null && _detailExpFill.IsValid )
		{
			var xpFrac = xpMax > 0 ? Math.Clamp( xpCurrent / (float)xpMax, 0f, 1f ) : 0f;
			_detailExpFill.Style.Width = Length.Percent( xpFrac * 100f );
			_detailExpFill.Style.BackgroundColor = ExpFill;
		}

		RebuildAttributeRows( tame, availableXp );
		RebuildTraits( tame );
	}

	void ClearDetailPanel()
	{
		if ( _heroName is not null && _heroName.IsValid )
			_heroName.Text = "";
		if ( _heroTier is not null && _heroTier.IsValid )
		{
			_heroTier.Text = "";
			_heroTier.Style.Display = DisplayMode.None;
		}
		if ( _heroLevel is not null && _heroLevel.IsValid )
			_heroLevel.Text = "";
		if ( _detailAvailableXpValue is not null && _detailAvailableXpValue.IsValid )
			_detailAvailableXpValue.Text = "0";
		if ( _detailHealthLabel is not null && _detailHealthLabel.IsValid )
			_detailHealthLabel.Text = "0 / 0";
		if ( _detailHealthFill is not null && _detailHealthFill.IsValid )
			_detailHealthFill.Style.Width = Length.Percent( 0 );
		if ( _detailExpLevelLabel is not null && _detailExpLevelLabel.IsValid )
			_detailExpLevelLabel.Text = "";
		if ( _detailExpLabel is not null && _detailExpLabel.IsValid )
			_detailExpLabel.Text = "";
		if ( _detailExpFill is not null && _detailExpFill.IsValid )
			_detailExpFill.Style.Width = Length.Percent( 0 );
		if ( _detailPortrait is not null && _detailPortrait.IsValid )
			ThornsIconCache.ApplyToPanel( _detailPortrait, "" );
	}

	void RebuildAttributeRows( ThornsTameListEntryDto tame, int availableXp )
	{
		if ( _attributesPanel is null || !_attributesPanel.IsValid )
			return;

		AddAttributeRow( tame, ThornsTameStat.Strength, tame.StatStrength, availableXp );
		AddAttributeRow( tame, ThornsTameStat.Defense, tame.StatDefense, availableXp );
		AddAttributeRow( tame, ThornsTameStat.Stamina, tame.StatStamina, availableXp );
		AddAttributeRow( tame, ThornsTameStat.Agility, tame.StatAgility, availableXp );
		AddAttributeRow( tame, ThornsTameStat.Intelligence, tame.StatIntelligence, availableXp );
	}

	void AddAttributeRow( ThornsTameListEntryDto tame, ThornsTameStat stat, int value, int availableXp )
	{
		var canUpgrade = availableXp > 0;
		var row = ThornsUiFactory.AddPanel( _attributesPanel, "tames-attribute-row" );
		row.Style.FlexDirection = FlexDirection.Row;
		row.Style.AlignItems = Align.Center;
		row.Style.JustifyContent = Justify.SpaceBetween;
		row.Style.MarginBottom = Length.Pixels( 6 );
		row.Style.Width = Length.Percent( 100 );

		ThornsUiFactory.AddPassiveLabel( row, ThornsTameProgression.StatLabel( stat ), "tames-attribute-name" );

		var valueRow = ThornsUiFactory.AddPanel( row, "tames-attribute-value-row" );
		valueRow.Style.FlexDirection = FlexDirection.Row;
		valueRow.Style.AlignItems = Align.Center;
		ThornsUiFactory.AddPassiveLabel( valueRow, value.ToString(), "tames-attribute-value" );

		var statKey = ThornsTameProgression.StatKey( stat );
		var entityId = tame.EntityId;
		var plusBtn = ThornsUiFactory.AddClickable(
			valueRow,
			canUpgrade ? "tames-attribute-plus" : "tames-attribute-plus disabled",
			"+",
			() => RequestStatUpgrade( entityId, statKey ) );
		plusBtn.SetClass( "disabled", !canUpgrade );
	}

	void RequestStatUpgrade( Guid entityId, string statKey )
	{
		if ( entityId == Guid.Empty || string.IsNullOrWhiteSpace( statKey ) )
			return;

		ThornsPlayerGameplay.Local?.RequestTameStatUpgrade( new ThornsTameStatUpgradeRequest
		{
			TameEntityId = entityId,
			StatKey = statKey
		} );
	}

	void RebuildTraits( ThornsTameListEntryDto tame )
	{
		if ( _traitsPanel is null || !_traitsPanel.IsValid )
			return;

		if ( tame.Traits.Count == 0 && !tame.IsCrossbreed && !tame.IsMutated && !tame.IsSuperCrossbreed )
			return;

		ThornsTheme.CreateHeader( _traitsPanel, "TRAITS" );

		if ( tame.Traits.Count == 0 )
		{
			ThornsTheme.CreateMuted( _traitsPanel, "No traits recorded." );
			return;
		}

		foreach ( var trait in tame.Traits )
		{
			var row = ThornsUiFactory.AddPanel( _traitsPanel, "tame-trait-card" );
			row.Style.FlexDirection = FlexDirection.Row;
			row.Style.AlignItems = Align.Center;
			row.Style.MarginBottom = Length.Pixels( 6 );

			var traitIcon = ThornsUiFactory.AddPanel( row, "tame-trait-icon slot-icon" );
			traitIcon.Style.Width = Length.Pixels( ThornsUiMetrics.MenuTameTraitIcon );
			traitIcon.Style.Height = Length.Pixels( ThornsUiMetrics.MenuTameTraitIcon );
			traitIcon.Style.FlexShrink = 0;
			traitIcon.Style.MarginRight = Length.Pixels( 8 );
			ThornsIconCache.ApplyToPanel( traitIcon, trait.IconPath );

			var textCol = ThornsUiFactory.AddPanel( row, "tame-trait-text" );
			textCol.Style.FlexDirection = FlexDirection.Column;
			textCol.Style.FlexGrow = 1;
			ThornsUiFactory.AddPassiveLabel( textCol, trait.Title, "tame-trait-title" );
			if ( !string.IsNullOrWhiteSpace( trait.Description ) )
				ThornsUiFactory.AddPassiveLabel( textCol, trait.Description, "tame-trait-desc" );
		}
	}

	static void ApplyTierLabel( Label label, int tier )
	{
		if ( label is null || !label.IsValid )
			return;

		label.Text = ThornsTameCatalog.FormatTierRarityLine( tier );
		label.Style.FontColor = ThornsTameCatalog.TierColor( tier );
		label.Style.Display = DisplayMode.Flex;
	}

	static void AddInlineTierLabel( Panel parent, int tier )
	{
		var label = ThornsUiFactory.AddPassiveLabel( parent, ThornsTameCatalog.FormatTierRarityLine( tier ), "tame-card-tier tame-slot-species tame-card-tier-inline" );
		label.Style.FontColor = ThornsTameCatalog.TierColor( tier );
		label.Style.FlexShrink = 0;
		label.Style.MarginLeft = Length.Pixels( 6 );
		label.Style.TextAlign = TextAlign.Right;
	}

	void RebuildCommands()
	{
		if ( _commands is null || !_commands.IsValid )
			return;

		_commands.DeleteChildren( true );

		var snap = ThornsUiClientState.Snapshot.Tames;
		var tame = snap.Tames.FirstOrDefault( t => t.EntityId == snap.SelectedEntityId )
		           ?? snap.Tames.FirstOrDefault();
		if ( tame is null )
		{
			ThornsTheme.CreateMuted( _commands, "Select a tame to issue orders." );
			return;
		}

		foreach ( var cmd in ConceptActionCommands )
		{
			var captured = cmd;
			AddConceptActionButton(
				_commands,
				ConceptCommandLabel( cmd ),
				ThornsTameCatalog.CommandIconPath( cmd ),
				tame.ActiveCommand == cmd,
				() => SendCommand( captured ) );
		}

		AddConceptActionButton(
			_commands,
			"SUMMON",
			ThornsTameCatalog.CommandIconPath( ThornsTameCommand.Summon ),
			false,
			() => SendCommand( ThornsTameCommand.Summon ) );

		AddConceptActionButton(
			_commands,
			"FEED",
			ThornsTameCatalog.FeedCommandIconPath(),
			false,
			SendFeed );
	}

	void RefreshFeedNotice()
	{
		if ( _feedNoticeAnchor is null || !_feedNoticeAnchor.IsValid || _feedNotice is null || !_feedNotice.IsValid )
			return;

		var entry = ThornsTameFeedNoticeBus.Active;
		if ( entry is null || string.IsNullOrWhiteSpace( entry.Message ) )
		{
			_feedNotice.Text = "";
			_feedNoticeAnchor.Style.Display = DisplayMode.None;
			return;
		}

		_feedNotice.Text = entry.Message;
		_feedNoticeAnchor.Style.Display = DisplayMode.Flex;
		_feedNotice.Style.FontColor = entry.Kind == "success"
			? new Color( 0.47f, 0.75f, 0.33f )
			: new Color( 0.92f, 0.72f, 0.38f );
	}

	void OnRenameCommitted()
	{
		if ( _renameEntry is null || !_renameEntry.IsValid || _renameEntityId == Guid.Empty )
			return;

		var name = _renameEntry.Text?.Trim() ?? "";
		if ( string.IsNullOrEmpty( name ) )
			return;

		var entityId = _renameEntityId;
		var snap = ThornsUiClientState.Snapshot.Tames;
		var current = snap.Tames.FirstOrDefault( t => t.EntityId == entityId );
		if ( current is not null && string.Equals( current.DisplayName, name, StringComparison.Ordinal ) )
			return;

		ThornsPlayerGameplay.Local?.RequestTameRename( new ThornsTameRenameRequest
		{
			TameEntityId = entityId,
			DisplayName = name
		} );
	}

	void SendCommand( ThornsTameCommand cmd )
	{
		var id = ResolveSelectedTameId();
		if ( id == Guid.Empty )
			return;

		ThornsPlayerGameplay.Local?.RequestTameCommand( new ThornsTameCommandRequest
		{
			TameEntityId = id,
			Command = cmd
		} );
	}

	void SendFeed()
	{
		var id = ResolveSelectedTameId();
		if ( id == Guid.Empty )
			return;

		ThornsPlayerGameplay.Local?.RequestTameFeed( new ThornsTameFeedRequest
		{
			TameEntityId = id
		} );
	}

	Guid ResolveSelectedTameId()
	{
		var snap = ThornsUiClientState.Snapshot.Tames;
		if ( snap.SelectedEntityId != Guid.Empty
		     && snap.Tames.Any( t => t.EntityId == snap.SelectedEntityId ) )
			return snap.SelectedEntityId;

		return snap.Tames.FirstOrDefault()?.EntityId ?? Guid.Empty;
	}

}
