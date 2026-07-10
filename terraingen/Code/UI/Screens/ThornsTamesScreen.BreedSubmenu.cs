namespace Terraingen.UI.Screens;

using Sandbox.UI;
using Terraingen.GameData;
using Terraingen.Player;
using Terraingen.UI;
using Terraingen.UI.Components;
using Terraingen.UI.Core;
using Terraingen.UI.Menu;

public sealed partial class ThornsTamesScreen
{
	Panel _breedOverlay;
	Panel _breedPanel;
	Panel _breedPanelBody;
	Panel _breedFooter;

	void BuildBreedSubmenuShell()
	{
		( _breedOverlay, _breedPanel ) = ThornsMenuChrome.CreateOverlayShell( this, "tames-breed-submenu", maxWidthPx: 720 );
		_breedOverlay.AddClass( "tames-breed-overlay" );
		_breedOverlay.Style.Display = DisplayMode.None;
		ThornsUiLayer.ApplyModalSurface( _breedOverlay, ThornsUiPriority.CriticalPopup );

		ApplyBreedPopupBackdrop( _breedPanel );
		_breedPanel.Style.MinHeight = Length.Pixels( 360 );
		_breedPanel.Style.MaxHeight = Length.Percent( 86 );
		_breedPanel.Style.MinWidth = Length.Pixels( 0 );

		var header = ThornsUiFactory.AddPanel( _breedPanel, "tames-breed-header" );
		header.Style.FlexDirection = FlexDirection.Row;
		header.Style.AlignItems = Align.Center;
		header.Style.JustifyContent = Justify.SpaceBetween;
		header.Style.FlexShrink = 0;
		header.Style.MarginBottom = Length.Pixels( 8 );
		header.Style.Width = Length.Percent( 100 );
		header.Style.MinWidth = Length.Pixels( 0 );
		ThornsTheme.CreateSectionHeader( header, "BREEDING", "tames-breed-section-header" );
		var closeBtn = ThornsUiFactory.AddClickable( header, "tame-breed-close-btn", "CLOSE", CloseBreedSubmenu );
		closeBtn.Style.FlexShrink = 0;

		_breedPanelBody = ThornsUiFactory.AddPanel( _breedPanel, "tames-breed-body" );
		_breedPanelBody.Style.FlexDirection = FlexDirection.Column;
		_breedPanelBody.Style.FlexGrow = 1;
		_breedPanelBody.Style.FlexShrink = 1;
		_breedPanelBody.Style.MinHeight = Length.Pixels( 0 );
		_breedPanelBody.Style.Overflow = OverflowMode.Scroll;
		_breedPanelBody.Style.Width = Length.Percent( 100 );

		_breedFooter = ThornsUiFactory.AddPanel( _breedPanel, "tames-breed-footer" );
		_breedFooter.Style.FlexDirection = FlexDirection.Column;
		_breedFooter.Style.FlexShrink = 0;
		_breedFooter.Style.Width = Length.Percent( 100 );
		_breedFooter.Style.MarginTop = Length.Pixels( 10 );
		_breedFooter.Style.PaddingTop = Length.Pixels( 8 );
	}

	void ToggleBreedSubmenu()
	{
		var snap = ThornsUiClientState.Snapshot.Tames;
		snap.BreedPanelOpen = !snap.BreedPanelOpen;
		if ( !snap.BreedPanelOpen )
			ClearBreedSelection( snap );

		UiRevisionBus.Publish( UiRevisionChannel.Tames );
	}

	void CloseBreedSubmenu()
	{
		var snap = ThornsUiClientState.Snapshot.Tames;
		if ( !snap.BreedPanelOpen )
			return;

		snap.BreedPanelOpen = false;
		ClearBreedSelection( snap );
		UiRevisionBus.Publish( UiRevisionChannel.Tames );
	}

	static void ClearBreedSelection( ThornsTamesSnapshotDto snap )
	{
		snap.BreedParentAId = Guid.Empty;
		snap.BreedParentBId = Guid.Empty;
	}

	void RebuildBreedSubmenu()
	{
		if ( _breedOverlay is null || !_breedOverlay.IsValid || _breedPanelBody is null || !_breedPanelBody.IsValid )
			return;

		var snap = ThornsUiClientState.Snapshot.Tames;
		_breedOverlay.Style.Display = snap.BreedPanelOpen ? DisplayMode.Flex : DisplayMode.None;
		_breedPanelBody.DeleteChildren( true );
		_breedFooter?.DeleteChildren( true );

		if ( !snap.BreedPanelOpen )
			return;

		ThornsUiFactory.AddPassiveLabel(
			_breedPanelBody,
			"Choose two different tames to breed a new companion.",
			"tames-breed-intro thorns-muted" );

		if ( snap.Tames.Count < 2 )
		{
			ThornsTheme.CreateMuted( _breedPanelBody, "You need at least two living tames." );
			RebuildBreedSubmitFooter( snap, canBreed: false );
			return;
		}

		var parentsRow = ThornsUiFactory.AddPanel( _breedPanelBody, "tames-breed-parents-row" );
		parentsRow.Style.FlexDirection = FlexDirection.Row;
		parentsRow.Style.FlexShrink = 0;
		parentsRow.Style.Width = Length.Percent( 100 );
		parentsRow.Style.MarginTop = Length.Pixels( 10 );
		parentsRow.Style.MarginBottom = Length.Pixels( 10 );

		AddBreedParentSlot( parentsRow, "PARENT A", snap.BreedParentAId, snap, marginRight: true );
		AddBreedParentSlot( parentsRow, "PARENT B", snap.BreedParentBId, snap );

		ThornsTheme.CreateHeader( _breedPanelBody, "SELECT PARENTS" );

		var list = ThornsUiFactory.AddPanel( _breedPanelBody, "tames-breed-candidate-list" );
		list.Style.FlexDirection = FlexDirection.Column;
		list.Style.Width = Length.Percent( 100 );

		foreach ( var tame in snap.Tames )
			AddBreedCandidateRow( list, snap, tame );

		var parentA = snap.Tames.FirstOrDefault( t => t.EntityId == snap.BreedParentAId );
		var parentB = snap.Tames.FirstOrDefault( t => t.EntityId == snap.BreedParentBId );
		var canBreed = snap.Tames.Count < ThornsTameCatalog.MaxTameSlots
		               && snap.BreedParentAId != Guid.Empty
		               && snap.BreedParentBId != Guid.Empty
		               && snap.BreedParentAId != snap.BreedParentBId
		               && parentA is not null && !ThornsTameCatalog.IsOnBreedCooldown( parentA.BreedCooldownUntilUtcTicks )
		               && parentB is not null && !ThornsTameCatalog.IsOnBreedCooldown( parentB.BreedCooldownUntilUtcTicks );

		MaybeClearBreedParentsAfterCooldown( snap );

		RebuildBreedSubmitFooter( snap, canBreed );
	}

	static void MaybeClearBreedParentsAfterCooldown( ThornsTamesSnapshotDto snap )
	{
		if ( snap.BreedParentAId == Guid.Empty && snap.BreedParentBId == Guid.Empty )
			return;

		var parentA = snap.BreedParentAId != Guid.Empty
			? snap.Tames.FirstOrDefault( t => t.EntityId == snap.BreedParentAId )
			: null;
		var parentB = snap.BreedParentBId != Guid.Empty
			? snap.Tames.FirstOrDefault( t => t.EntityId == snap.BreedParentBId )
			: null;

		if ( parentA is null && snap.BreedParentAId != Guid.Empty )
			snap.BreedParentAId = Guid.Empty;
		if ( parentB is null && snap.BreedParentBId != Guid.Empty )
			snap.BreedParentBId = Guid.Empty;

		if ( snap.BreedParentAId == Guid.Empty || snap.BreedParentBId == Guid.Empty )
			return;

		if ( !ThornsTameCatalog.IsOnBreedCooldown( parentA!.BreedCooldownUntilUtcTicks )
		     || !ThornsTameCatalog.IsOnBreedCooldown( parentB!.BreedCooldownUntilUtcTicks ) )
			return;

		ClearBreedSelection( snap );
		snap.BreedPanelOpen = false;
	}

	void RebuildBreedSubmitFooter( ThornsTamesSnapshotDto snap, bool canBreed )
	{
		if ( _breedFooter is null || !_breedFooter.IsValid )
			return;

		var breedBtn = ThornsUiFactory.AddClickable(
			_breedFooter,
			canBreed ? "thorns-btn-primary tame-breed-submit" : "thorns-btn-primary tame-breed-submit disabled",
			"BREED",
			() =>
			{
				if ( snap.BreedParentAId == Guid.Empty || snap.BreedParentBId == Guid.Empty || snap.BreedParentAId == snap.BreedParentBId )
					return;

				ThornsPlayerGameplay.Local?.RequestTameBreed( new ThornsTameBreedRequest
				{
					ParentAEntityId = snap.BreedParentAId,
					ParentBEntityId = snap.BreedParentBId
				} );
			} );
		breedBtn.SetClass( "disabled", !canBreed );
		breedBtn.Style.Width = Length.Percent( 100 );
		breedBtn.Style.MinHeight = Length.Pixels( 40 );
		breedBtn.Style.JustifyContent = Justify.Center;
		breedBtn.Style.AlignItems = Align.Center;
	}

	void AddBreedParentSlot( Panel parent, string label, Guid entityId, ThornsTamesSnapshotDto snap, bool marginRight = false )
	{
		var slot = ThornsUiFactory.AddPanel( parent, "tames-breed-parent-slot concept-section" );
		ThornsTheme.ApplyConceptSection( slot );
		slot.Style.FlexDirection = FlexDirection.Column;
		slot.Style.FlexGrow = 1;
		slot.Style.FlexBasis = Length.Percent( 50 );
		if ( marginRight )
			slot.Style.MarginRight = Length.Pixels( 6 );

		ThornsUiFactory.AddPassiveLabel( slot, label, "tames-breed-parent-label" );

		var tame = snap.Tames.FirstOrDefault( t => t.EntityId == entityId );
		if ( tame is null )
		{
			ThornsUiFactory.AddPassiveLabel( slot, "None selected", "tames-breed-parent-empty thorns-muted" );
			return;
		}

		var row = ThornsUiFactory.AddPanel( slot, "tames-breed-parent-selected" );
		row.Style.FlexDirection = FlexDirection.Row;
		row.Style.AlignItems = Align.Center;
		row.Style.MarginTop = Length.Pixels( 6 );

		var portrait = ThornsUiFactory.AddPanel( row, "tame-portrait tame-list-portrait slot-icon" );
		portrait.Style.Width = Length.Pixels( 48 );
		portrait.Style.Height = Length.Pixels( 48 );
		portrait.Style.FlexShrink = 0;
		portrait.Style.MarginRight = Length.Pixels( 8 );
		ThornsIconCache.ApplyToPanel( portrait, tame.PortraitPath );

		var body = ThornsUiFactory.AddPanel( row, "tame-card-body" );
		body.Style.FlexDirection = FlexDirection.Column;
		body.Style.MinWidth = Length.Pixels( 0 );
		body.Style.FlexGrow = 1;
		ThornsUiFactory.AddPassiveLabel( body, tame.DisplayName.ToUpperInvariant(), "tame-card-title" );
		ThornsUiFactory.AddPassiveLabel( body, ThornsTameCatalog.FormatTierLevelLine( tame.Tier, tame.SpeciesName, tame.Level ), "tame-card-sub" );
	}

	void AddBreedCandidateRow( Panel parent, ThornsTamesSnapshotDto snap, ThornsTameListEntryDto tame )
	{
		var row = ThornsUiFactory.AddPanel( parent, "tame-slot-card tames-list-card tames-breed-candidate-row concept-section" );
		ThornsTheme.ApplyConceptSection( row );
		row.Style.FlexDirection = FlexDirection.Row;
		row.Style.AlignItems = Align.Center;
		row.Style.MarginBottom = Length.Pixels( 8 );

		var portrait = ThornsUiFactory.AddPanel( row, "tame-portrait tame-list-portrait slot-icon" );
		portrait.Style.Width = Length.Pixels( 56 );
		portrait.Style.Height = Length.Pixels( 56 );
		portrait.Style.FlexShrink = 0;
		portrait.Style.MarginRight = Length.Pixels( 10 );
		ThornsIconCache.ApplyToPanel( portrait, tame.PortraitPath );

		var body = ThornsUiFactory.AddPanel( row, "tame-card-body tame-list-card-body" );
		body.Style.FlexDirection = FlexDirection.Column;
		body.Style.FlexGrow = 1;
		body.Style.MinWidth = Length.Pixels( 0 );
		ThornsUiFactory.AddPassiveLabel( body, tame.DisplayName.ToUpperInvariant(), "tame-card-title tame-slot-name" );
		var sub = $"{ThornsTameCatalog.FormatTierLevelLine( tame.Tier, tame.SpeciesName, tame.Level )} — {FormatLineage( tame )}";
		if ( ThornsTameCatalog.IsOnBreedCooldown( tame.BreedCooldownUntilUtcTicks ) )
			sub += $" — ready in {ThornsTameCatalog.FormatBreedCooldownRemaining( tame.BreedCooldownUntilUtcTicks )}";
		ThornsUiFactory.AddPassiveLabel( body, sub, "tame-card-sub" );

		var picks = ThornsUiFactory.AddPanel( row, "tames-breed-pick-row" );
		picks.Style.FlexDirection = FlexDirection.Row;
		picks.Style.AlignItems = Align.Center;
		picks.Style.FlexShrink = 0;
		picks.Style.MarginLeft = Length.Pixels( 8 );

		AddBreedPickButton( picks, "A", snap, tame );
		AddBreedPickButton( picks, "B", snap, tame );
	}

	void AddBreedPickButton( Panel parent, string slot, ThornsTamesSnapshotDto snap, ThornsTameListEntryDto tame )
	{
		var tameId = tame.EntityId;
		var onCooldown = ThornsTameCatalog.IsOnBreedCooldown( tame.BreedCooldownUntilUtcTicks );
		var selected = string.Equals( slot, "A", StringComparison.Ordinal )
			? snap.BreedParentAId == tameId
			: snap.BreedParentBId == tameId;
		var btn = ThornsUiFactory.AddClickable(
			parent,
			selected ? "tame-breed-pick selected" : onCooldown ? "tame-breed-pick disabled" : "tame-breed-pick",
			slot,
			() =>
			{
				if ( onCooldown )
					return;

				if ( string.Equals( slot, "A", StringComparison.Ordinal ) )
				{
					snap.BreedParentAId = selected ? Guid.Empty : tameId;
					if ( snap.BreedParentBId == snap.BreedParentAId )
						snap.BreedParentBId = Guid.Empty;
				}
				else
				{
					snap.BreedParentBId = selected ? Guid.Empty : tameId;
					if ( snap.BreedParentAId == snap.BreedParentBId )
						snap.BreedParentAId = Guid.Empty;
				}

				UiRevisionBus.Publish( UiRevisionChannel.Tames );
			} );
		btn.SetClass( "selected", selected );
		btn.SetClass( "disabled", onCooldown );
		if ( !string.Equals( slot, "A", StringComparison.Ordinal ) )
			btn.Style.MarginLeft = Length.Pixels( 6 );
	}

	static string FormatLineage( ThornsTameListEntryDto tame )
	{
		if ( tame.GeneticSpeciesNames is { Count: > 0 } )
			return string.Join( " / ", tame.GeneticSpeciesNames );

		return tame.SpeciesName;
	}

	static void ApplyBreedPopupBackdrop( Panel panel )
	{
		if ( panel is null || !panel.IsValid )
			return;

		panel.RemoveClass( "thorns-station-parchment-fill" );
		panel.AddClass( "tames-breed-submenu-backdrop" );
		panel.Style.Overflow = OverflowMode.Hidden;
		ThornsMainMenuBackdrop.ApplyTabMenuBackdrop( panel );
	}
}
