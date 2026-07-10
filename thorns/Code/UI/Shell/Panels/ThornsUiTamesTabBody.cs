#nullable disable

using System;
using System.Collections.Generic;
using System.Text;
using Sandbox.UI;

namespace Sandbox;

/// <summary>Inventory-tab-style tames UI — three-column layout (list · preview · inspect) matching shell mockup.</summary>
public sealed class ThornsUiTamesTabBody : Panel
{
	const string PanelHiddenClass = "thorns-tames-panel-hidden";

	public const int MaxActiveTamesUiCap = 5;

	readonly Action _onCloseMenu;

	readonly Label _subtitleActive;
	readonly Panel _listScroll;
	readonly Panel _previewStage;
	readonly Panel _previewImageHost;
	readonly Label _previewGlyphFallback;
	readonly Label _detailHeading;
	readonly Label _levelLine;
	readonly Panel _xpTrack;
	readonly Panel _xpFill;
	readonly Label _xpNums;
	readonly Panel _tabRow;
	readonly Panel[] _tabChips = new Panel[3];
	readonly Panel _statsBody;
	readonly Panel _abilitiesBody;
	readonly Label _behaviorBody;
	readonly TextEntry _nameEntry;
	readonly Panel _upgradeBannerHost;
	readonly Label _upgradePromptLabel;
	readonly Panel _statRowsHost;
	readonly Panel _orderRowHost;
	readonly ThornsUiCapsuleButton _followBtn;
	readonly ThornsUiCapsuleButton _stayBtn;
	readonly ThornsUiCapsuleButton _applyNameBtn;
	readonly Panel _abilityStrip;

	Guid _selectedWildlifeId;
	GameObject _pawnRoot;
	int _detailTab; // 0 stats, 1 abilities, 2 behavior
	Guid _lastDetailWildlifeId = Guid.Empty;
	bool _nameEntryFocused;

	public ThornsUiTamesTabBody( Action onCloseMenu )
	{
		_onCloseMenu = onCloseMenu;

		AddClass( "thorns-tab-tames" );
		AddClass( "thorns-tames-mock-root" );
		Style.FlexGrow = 1;
		Style.FlexShrink = 1;
		Style.MinHeight = 0;
		Style.Width = Length.Fraction( 1f );
		Style.FlexDirection = FlexDirection.Column;
		Style.Overflow = OverflowMode.Hidden;

		var head = ThornsUiPanelAdd.AddChildPanel(this,  "thorns-tames-mock-head" );
		head.Style.FlexDirection = FlexDirection.Row;
		head.Style.AlignItems = Align.Center;
		head.Style.JustifyContent = Justify.SpaceBetween;
		head.Style.MarginBottom = Length.Pixels( 12 );
		head.Style.FlexShrink = 0;

		var headLeft = ThornsUiPanelAdd.AddChildPanel(head,  "thorns-tames-mock-head-left" );
		headLeft.Style.FlexDirection = FlexDirection.Column;
		headLeft.Style.PointerEvents = PointerEvents.None;

		headLeft.AddChild( new Label( "TAMES", "thorns-tames-mock-title" ) );
		_subtitleActive = headLeft.AddChild(
			new Label( $"0/{MaxActiveTamesUiCap} Active", "thorns-tames-mock-subtitle" ) );

		var closeBtn = ThornsUiPanelAdd.AddChildPanel(head,  "thorns-tames-mock-close" );
		closeBtn.Style.PointerEvents = PointerEvents.All;
		closeBtn.AddEventListener( "onmousedown", _ => _onCloseMenu?.Invoke() );
		closeBtn.AddChild( new Label( "×", "thorns-tames-mock-close-glyph" ) );

		var body = ThornsUiPanelAdd.AddChildPanel(this,  "thorns-tames-mock-columns" );
		body.AddClass( "thorns-tab-tames-layout" );
		body.Style.FlexGrow = 1;
		body.Style.FlexShrink = 1;
		body.Style.MinHeight = 0;

		_listScroll = ThornsUiPanelAdd.AddChildPanel(body,  "thorns-tames-list" );
		_listScroll.Style.FlexBasis = Length.Percent( 24f );
		_listScroll.Style.MinWidth = Length.Pixels( 200 );

		var center = ThornsUiPanelAdd.AddChildPanel(body,  "thorns-tames-mock-preview-wrap" );
		center.Style.FlexGrow = 1;
		center.Style.FlexShrink = 1;
		center.Style.MinWidth = Length.Pixels( 200 );
		center.Style.MinHeight = 0;

		_previewStage = ThornsUiPanelAdd.AddChildPanel(center,  "thorns-tames-mock-preview-stage" );
		_previewImageHost = ThornsUiPanelAdd.AddChildPanel(_previewStage,  "thorns-tames-mock-preview-image-host" );
		_previewImageHost.Style.PointerEvents = PointerEvents.None;
		_previewGlyphFallback = _previewStage.AddChild( new Label( "◇", "thorns-tames-mock-preview-glyph" ) );
		_previewGlyphFallback.Style.PointerEvents = PointerEvents.None;

		var right = ThornsUiPanelAdd.AddChildPanel(body,  "thorns-tames-mock-detail" );
		right.Style.FlexBasis = Length.Percent( 42f );
		right.Style.MinWidth = Length.Pixels( 300 );
		right.Style.MaxWidth = Length.Pixels( 560 );
		right.Style.FlexDirection = FlexDirection.Column;
		right.Style.MinHeight = 0;
		right.Style.Overflow = OverflowMode.Visible;

		var nameRow = ThornsUiPanelAdd.AddChildPanel(right,  "thorns-tames-mock-name-row" );
		nameRow.Style.FlexDirection = FlexDirection.Row;
		nameRow.Style.AlignItems = Align.Center;
		nameRow.Style.JustifyContent = Justify.SpaceBetween;
		nameRow.Style.MarginBottom = Length.Pixels( 4 );

		_detailHeading = nameRow.AddChild( new Label( "—", "thorns-tames-mock-detail-heading" ) );
		_detailHeading.Style.PointerEvents = PointerEvents.None;
		_detailHeading.Style.FlexGrow = 1;
		_detailHeading.Style.WhiteSpace = WhiteSpace.Normal;

		_levelLine = right.AddChild( new Label( "Level —", "thorns-tames-mock-level" ) );
		_levelLine.Style.PointerEvents = PointerEvents.None;

		var xpRow = ThornsUiPanelAdd.AddChildPanel(right,  "thorns-tames-mock-xp-row" );
		xpRow.Style.FlexDirection = FlexDirection.Row;
		xpRow.Style.AlignItems = Align.Center;
		xpRow.Style.MarginBottom = Length.Pixels( 10 );
		xpRow.Style.PointerEvents = PointerEvents.None;

		_xpTrack = ThornsUiPanelAdd.AddChildPanel(xpRow,  "thorns-tames-mock-xp-track" );
		_xpTrack.Style.FlexGrow = 1;
		_xpTrack.Style.Height = Length.Pixels( 8 );
		_xpTrack.Style.MarginRight = Length.Pixels( 10 );
		_xpFill = ThornsUiPanelAdd.AddChildPanel(_xpTrack,  "thorns-tames-mock-xp-fill" );

		_xpNums = xpRow.AddChild( new Label( "— / — XP", "thorns-tames-mock-xp-nums" ) );

		_tabRow = ThornsUiPanelAdd.AddChildPanel(right,  "thorns-tames-mock-tabs" );
		_tabRow.Style.FlexDirection = FlexDirection.Row;
		_tabRow.Style.MarginBottom = Length.Pixels( 10 );
		_tabRow.Style.FlexShrink = 0;

		AddTabChip( 0, "STATS" );
		AddTabChip( 1, "ABILITIES" );
		AddTabChip( 2, "BEHAVIOR" );

		// One scroll region for stats + abilities + behavior so long text is not clipped by the detail column overflow.
		var tabScrollHost = ThornsUiPanelAdd.AddChildPanel(right,  "thorns-tames-mock-tab-scroll-host" );
		tabScrollHost.Style.FlexDirection = FlexDirection.Column;
		tabScrollHost.Style.FlexGrow = 1;
		tabScrollHost.Style.MinHeight = 0;
		tabScrollHost.Style.Width = Length.Fraction( 1f );
		tabScrollHost.Style.Overflow = OverflowMode.Scroll;

		_statsBody = ThornsUiPanelAdd.AddChildPanel(tabScrollHost,  "thorns-tames-mock-stats-body" );
		_statsBody.Style.FlexDirection = FlexDirection.Column;
		_statsBody.Style.FlexShrink = 0;

		_upgradeBannerHost = ThornsUiPanelAdd.AddChildPanel(_statsBody,  "thorns-tames-mock-upgrade-banner" );
		_upgradeBannerHost.Style.FlexDirection = FlexDirection.Column;
		_upgradeBannerHost.Style.MarginBottom = Length.Pixels( 10 );
		_upgradeBannerHost.Style.FlexShrink = 0;
		_upgradePromptLabel = _upgradeBannerHost.AddChild(
			new Label( "", "thorns-tames-mock-upgrade-prompt" ) );
		_upgradePromptLabel.Style.MarginBottom = Length.Pixels( 6 );
		var upgradeBtnRow = ThornsUiPanelAdd.AddChildPanel(_upgradeBannerHost,  "thorns-tames-mock-upgrade-buttons" );
		upgradeBtnRow.Style.FlexDirection = FlexDirection.Row;
		upgradeBtnRow.AddChild( new ThornsUiCapsuleButton(
			$"Health +{ThornsWildlifeIdentity.UpgradeHpBonusPerStep * 100f:F0}%",
			"accent",
			() => PushTameStatUpgrade( 0 ) ) );
		upgradeBtnRow.AddChild( new ThornsUiCapsuleButton(
			$"Damage +{ThornsWildlifeIdentity.UpgradeDmgBonusPerStep * 100f:F0}%",
			"secondary",
			() => PushTameStatUpgrade( 1 ) ) );
		upgradeBtnRow.AddChild( new ThornsUiCapsuleButton(
			$"Speed +{ThornsWildlifeIdentity.UpgradeSpdBonusPerStep * 100f:F0}%",
			"secondary",
			() => PushTameStatUpgrade( 2 ) ) );
		_upgradeBannerHost.SetClass( PanelHiddenClass, true );

		_statRowsHost = ThornsUiPanelAdd.AddChildPanel(_statsBody,  "thorns-tames-mock-stat-rows" );
		_statRowsHost.Style.FlexDirection = FlexDirection.Column;

		var statsFeedRow = ThornsUiPanelAdd.AddChildPanel(_statsBody,  "thorns-tames-mock-stats-feed-row" );
		statsFeedRow.Style.FlexDirection = FlexDirection.Row;
		statsFeedRow.Style.AlignItems = Align.Center;
		statsFeedRow.Style.MarginTop = Length.Pixels( 6 );
		statsFeedRow.Style.FlexShrink = 0;
		statsFeedRow.AddChild( new ThornsUiCapsuleButton( "Feed", "secondary", () => PushFeed() ) );

		_abilitiesBody = ThornsUiPanelAdd.AddChildPanel( tabScrollHost, "thorns-tames-abilities-root" );
		_abilitiesBody.Style.FlexDirection = FlexDirection.Column;
		_abilitiesBody.Style.AlignItems = Align.Stretch;
		_abilitiesBody.Style.FlexShrink = 0;
		_abilitiesBody.Style.Width = Length.Fraction( 1f );

		_behaviorBody = tabScrollHost.AddChild( new Label(
			"",
			"thorns-tames-mock-tab-body" ) );
		_behaviorBody.Style.WhiteSpace = WhiteSpace.PreLine;

		_nameEntry = right.AddChild( new TextEntry() );
		_nameEntry.Placeholder = "Rename tame…";
		_nameEntry.Style.MarginBottom = Length.Pixels( 8 );
		_nameEntry.AddEventListener( "onfocus", _ => _nameEntryFocused = true );
		_nameEntry.AddEventListener( "onblur", _ => _nameEntryFocused = false );

		_orderRowHost = ThornsUiPanelAdd.AddChildPanel(right,  "thorns-tames-mock-order-row" );
		_orderRowHost.Style.FlexDirection = FlexDirection.Row;
		_orderRowHost.Style.MarginBottom = Length.Pixels( 8 );
		_followBtn = _orderRowHost.AddChild( new ThornsUiCapsuleButton( "Follow", "secondary", () => PushFollow( true ) ) );
		_stayBtn = _orderRowHost.AddChild( new ThornsUiCapsuleButton( "Stay", "secondary", () => PushFollow( false ) ) );
		_applyNameBtn = _orderRowHost.AddChild( new ThornsUiCapsuleButton( "Save name", "secondary", () => PushApplyName() ) );

		_abilityStrip = ThornsUiPanelAdd.AddChildPanel(right,  "thorns-tames-mock-ability-strip" );
		_abilityStrip.Style.FlexDirection = FlexDirection.Row;
		_abilityStrip.Style.MarginBottom = Length.Pixels( 10 );
		_abilityStrip.Style.JustifyContent = Justify.FlexStart;
		_abilityStrip.Style.FlexShrink = 0;

		var summonBtn = ThornsUiPanelAdd.AddChildPanel(right,  "thorns-tames-mock-summon" );
		summonBtn.Style.PointerEvents = PointerEvents.All;
		summonBtn.AddEventListener( "onmousedown", _ => PushSummon() );
		summonBtn.AddChild( new Label( "SUMMON", "thorns-tames-mock-summon-label" ) );

		SetDetailTab( 0 );
	}

	void AddTabChip( int tabIndex, string label )
	{
		var chip = ThornsUiPanelAdd.AddChildPanel(_tabRow,  $"thorns-tames-tab-chip-{tabIndex}" );
		chip.AddClass( "thorns-tames-tab-chip" );
		chip.Style.PointerEvents = PointerEvents.All;
		chip.AddChild( new Label( label, "thorns-tames-tab-chip-label" ) );
		var t = tabIndex;
		chip.AddEventListener( "onmousedown", _ => SetDetailTab( t ) );
		_tabChips[tabIndex] = chip;
	}

	void SetDetailTab( int tab )
	{
		_detailTab = Math.Clamp( tab, 0, 2 );
		for ( var i = 0; i < _tabChips.Length; i++ )
		{
			if ( _tabChips[i] is not null && _tabChips[i].IsValid )
				_tabChips[i].SetClass( "thorns-tames-tab-chip--active", i == _detailTab );
		}

		SetPanelTabShown( _statsBody, _detailTab == 0 );
		SetPanelTabShown( _abilitiesBody, _detailTab == 1 );
		SetPanelTabShown( _behaviorBody, _detailTab == 2 );
		SetPanelTabShown( _nameEntry, _detailTab == 2 );
		orderRowVisibility();
	}

	static void SetPanelTabShown( Panel panel, bool shown ) =>
		panel.SetClass( PanelHiddenClass, !shown );

	void orderRowVisibility() =>
		SetPanelTabShown( _orderRowHost, _detailTab == 2 );

	public void RefreshFromPawn( GameObject pawnRoot, bool force = false )
	{
		if ( !IsValid )
			return;

		_pawnRoot = pawnRoot;

		var lc = Connection.Local;
		if ( lc is null || pawnRoot is null || !pawnRoot.IsValid() )
		{
			_listScroll.DeleteChildren();
			_subtitleActive.Text = $"0/{MaxActiveTamesUiCap} Active";
			ClearDetailChrome();
			return;
		}

		var scene = pawnRoot.Scene;
		if ( scene is null || !scene.IsValid() )
			return;

		var myAccountKey = ThornsPersistenceIdentity.GetStableAccountKey( lc );
		var tames = new List<ThornsWildlifeIdentity>();
		ThornsWildlifeTameRegistry.CopyOwnedTames( lc.Id, myAccountKey, tames );

		tames.Sort( static ( a, b ) => string.Compare(
			a.WildlifeIdSync, b.WildlifeIdSync, StringComparison.Ordinal ) );

		_listScroll.DeleteChildren();

		var active = Math.Min( tames.Count, MaxActiveTamesUiCap );
		_subtitleActive.Text = $"{active}/{MaxActiveTamesUiCap} Active";

		if ( tames.Count == 0 )
		{
			_selectedWildlifeId = Guid.Empty;
			_listScroll.AddChild( new Label(
				"No tames yet.\n\nWeaken a creature below your taming threshold, then hold Use (E) on it.",
				"thorns-tab-context-placeholder" ) );
			UpdateDetailPanel( null );
			return;
		}

		foreach ( var wid in tames )
		{
			var wGuid = wid.WildlifeId;
			if ( wGuid == Guid.Empty )
				continue;

			var card = ThornsUiPanelAdd.AddChildPanel(_listScroll,  "thorns-tames-list-card" );
			card.Style.FlexDirection = FlexDirection.Row;
			card.Style.AlignItems = Align.Center;
			card.SetClass( "thorns-tames-list-card--selected", wGuid == _selectedWildlifeId );

			var portrait = ThornsUiPanelAdd.AddChildPanel(card,  "thorns-tames-list-portrait" );
			portrait.Style.PointerEvents = PointerEvents.None;
			var isPredator = wid.Definition.IsPredator;
			portrait.AddClass( isPredator
				? "thorns-tames-list-portrait--wolf"
				: "thorns-tames-list-portrait--deer" );
			portrait.Style.BackgroundImage = null;
			portrait.DeleteChildren();
			if ( !ThornsTameHudIcons.TryBindPortraitBackground( portrait, wid.Species ) )
			{
				var display = wid.Definition.DisplayName;
				var letter = string.IsNullOrWhiteSpace( display ) ? "?" : display.Trim().Substring( 0, 1 ).ToUpperInvariant();
				portrait.AddChild( new Label( letter, "thorns-tames-list-portrait-letter" ) );
			}

			var mid = ThornsUiPanelAdd.AddChildPanel(card,  "thorns-tames-list-card-mid" );
			mid.Style.FlexGrow = 1;
			mid.Style.FlexDirection = FlexDirection.Column;
			mid.Style.PointerEvents = PointerEvents.None;

			var titleText = string.IsNullOrWhiteSpace( wid.TameDisplayNameSync )
				? wid.Definition.DisplayName
				: wid.TameDisplayNameSync.Trim();
			var titleLbl = mid.AddChild( new Label( titleText, "thorns-tames-list-card-title" ) );

			var lvl = wid.ComputeTameLevel();
			var tq = wid.TameQualityTier;
			var tierTint = tq.TintApprox();
			titleLbl.Style.FontColor = tierTint;
			var levelLbl = mid.AddChild( new Label( $"{wid.Definition.DisplayName}  \u25cf  Level {lvl}", "thorns-tames-list-card-level" ) );
			levelLbl.Style.FontColor = tierTint;

			GetTameXpProgressUi( wid, out _, out _, out var xpFill01 );
			var xpMini = ThornsUiPanelAdd.AddChildPanel(mid,  "thorns-tames-list-mini-xp" );
			var xpMiniFill = ThornsUiPanelAdd.AddChildPanel(xpMini,  "thorns-tames-list-mini-xp-fill" );
			xpMiniFill.Style.Width = Length.Fraction( xpFill01 );

			var hp = wid.Components.Get<ThornsHealth>();
			var hp01 = 0f;
			if ( hp.IsValid() && hp.MaxHealth > 0.01f )
				hp01 = Math.Clamp( hp.CurrentHealth / hp.MaxHealth, 0f, 1f );
			var hpMini = ThornsUiPanelAdd.AddChildPanel(mid,  "thorns-tames-list-mini-hp" );
			var hpMiniFill = ThornsUiPanelAdd.AddChildPanel(hpMini,  "thorns-tames-list-mini-hp-fill" );
			hpMiniFill.Style.Width = Length.Fraction( hp01 );

			var status = card.AddChild( new Label(
				wid.TameFollowOwnerSync ? "➜" : "⌖",
				"thorns-tames-list-status" ) );
			status.Style.PointerEvents = PointerEvents.None;

			card.AddEventListener( "onmousedown", _ =>
			{
				_nameEntryFocused = false;
				_selectedWildlifeId = wGuid;
				RefreshFromPawn( _pawnRoot, force: true );
			} );
		}

		if ( _selectedWildlifeId == Guid.Empty
		     || tames.TrueForAll( t => t.WildlifeId != _selectedWildlifeId ) )
			_selectedWildlifeId = tames[0].WildlifeId;

		ThornsWildlifeIdentity selected = null;
		foreach ( var t in tames )
		{
			if ( t.WildlifeId == _selectedWildlifeId )
			{
				selected = t;
				break;
			}
		}

		UpdateDetailPanel( selected );
	}

	static void GetTameXpProgressUi( ThornsWildlifeIdentity wid, out int progInLevel, out int spanInLevel, out float fill01 )
	{
		var total = wid.TameTotalXp;
		var lvl = ThornsVitals.ComputeLevelFromTotalXp( total );
		var curBase = ThornsVitals.CumulativeXpToEnterLevel( lvl );
		var nextBase = ThornsVitals.CumulativeXpToEnterLevel( lvl + 1 );
		spanInLevel = Math.Max( 1, nextBase - curBase );
		progInLevel = Math.Clamp( total - curBase, 0, spanInLevel );
		fill01 = Math.Clamp( progInLevel / (float)spanInLevel, 0f, 1f );
	}

	void ClearDetailChrome()
	{
		_lastDetailWildlifeId = Guid.Empty;
		_detailHeading.Text = "—";
		_levelLine.Text = "—";

		var neutral = new Color( 0.78f, 0.82f, 0.88f, 0.92f );
		_detailHeading.Style.FontColor = neutral;
		_levelLine.Style.FontColor = neutral;

		_xpNums.Text = "— / — XP";
		_xpFill.Style.Width = Length.Fraction( 0f );
		_previewImageHost.Style.BackgroundImage = null;
		_previewGlyphFallback.Text = "◇";
		_previewGlyphFallback.SetClass( "thorns-tames-mock-preview-glyph--hidden", false );
		_statRowsHost.DeleteChildren();
		_abilitiesBody.DeleteChildren();
		_behaviorBody.Text = "";
		_nameEntry.Text = "";
		RebuildAbilityStrip( null );
		ResetFollowStayVisuals();
		if ( _upgradeBannerHost.IsValid )
			_upgradeBannerHost.SetClass( PanelHiddenClass, true );
	}

	void ResetFollowStayVisuals()
	{
		if ( _followBtn.IsValid )
		{
			_followBtn.SetClass( "thorns-capsule-btn--accent", false );
			_followBtn.SetClass( "thorns-capsule-btn--secondary", true );
			_followBtn.SetClass( "active", false );
		}

		if ( _stayBtn.IsValid )
		{
			_stayBtn.SetClass( "thorns-capsule-btn--accent", false );
			_stayBtn.SetClass( "thorns-capsule-btn--secondary", true );
			_stayBtn.SetClass( "active", false );
		}
	}

	void UpdateDetailPanel( ThornsWildlifeIdentity sel )
	{
		if ( sel is null || !sel.IsValid() )
		{
			ClearDetailChrome();
			_detailHeading.Text = "No tame selected";
			_behaviorBody.Text = "Select a creature from the list.";
			return;
		}

		var wid = sel.WildlifeId;
		var selectionChanged = wid != _lastDetailWildlifeId;
		_lastDetailWildlifeId = wid;

		var def = sel.Definition;
		var hp = sel.Components.Get<ThornsHealth>();
		var title = string.IsNullOrWhiteSpace( sel.TameDisplayNameSync )
			? def.DisplayName
			: sel.TameDisplayNameSync.Trim();

		var tq = sel.TameQualityTier;
		var tierTint = tq.TintApprox();
		_detailHeading.Text = $"{title}  \u25cf  {tq.DisplayName()}";
		_detailHeading.Style.FontColor = tierTint;

		var lvl = sel.ComputeTameLevel();
		_levelLine.Text = $"{def.DisplayName}  \u25cf  Level {lvl}";
		_levelLine.Style.FontColor = tierTint;

		GetTameXpProgressUi( sel, out var progXp, out var spanXp, out var xp01 );
		_xpNums.Text = $"{progXp:N0} / {spanXp:N0} XP";
		_xpFill.Style.Width = Length.Fraction( xp01 );

		if ( ThornsTameHudIcons.TryBindPortraitBackground( _previewImageHost, sel.Species ) )
		{
			_previewGlyphFallback.SetClass( "thorns-tames-mock-preview-glyph--hidden", true );
		}
		else
		{
			_previewImageHost.Style.BackgroundImage = null;
			var display = def.DisplayName;
			_previewGlyphFallback.Text = string.IsNullOrWhiteSpace( display ) ? "?" : display.Trim().Substring( 0, 1 ).ToUpperInvariant();
			_previewGlyphFallback.SetClass( "thorns-tames-mock-preview-glyph--hidden", false );
		}
		var isPredator = def.IsPredator;
		_previewStage.SetClass( "thorns-tames-mock-preview-stage--wolf", isPredator );
		_previewStage.SetClass( "thorns-tames-mock-preview-stage--deer", !isPredator );

		var ups = sel.TameUnspentUpgradePoints;
		if ( _upgradeBannerHost.IsValid )
		{
			_upgradeBannerHost.SetClass( PanelHiddenClass, ups <= 0 );
			_upgradePromptLabel.Text = ThornsInteractionPromptText.Format(
				ups > 1
					? $"Level up — choose an upgrade ({ups} choices left)."
					: "Level up — choose Health, Damage, or Speed." );
		}

		_statRowsHost.DeleteChildren();
		var hpCur = hp.IsValid() ? hp.CurrentHealth : 0f;
		var hpMax = hp.IsValid() ? hp.MaxHealth : 1f;
		AddStatRow( "Health", $"{hpCur:F0} / {hpMax:F0}", hpMax > 0.01f ? hpCur / hpMax : 0f );

		var dmgBase = def.MeleeDamage > 0.01f ? def.MeleeDamage : ThornsWildlifeCombat.TamedAssistFallbackDamage;
		var dmgEff = dmgBase * sel.GetEffectiveDamageMultiplier();
		AddStatRow( "Damage", $"{dmgEff:F0}", Math.Clamp( dmgEff / 45f, 0f, 1f ) );

		var chaseEff = def.ChaseSpeed * sel.GetEffectiveSpeedMultiplier();
		AddStatRow( "Speed", $"{chaseEff:F0}", Math.Clamp( chaseEff / 350f, 0f, 1f ) );

		RebuildAbilitiesDetail( sel, def );

		var sb = new StringBuilder();
		sb.AppendLine( sel.TameFollowOwnerSync ? "Following your movement." : "Staying at last ordered position." );
		sb.AppendLine();
		sb.AppendLine( "Rename below, then Save name." );
		_behaviorBody.Text = sb.ToString();
		if ( selectionChanged || !_nameEntryFocused )
			_nameEntry.Text = sel.TameDisplayNameSync ?? "";

		var followOn = sel.TameFollowOwnerSync;
		_followBtn.SetClass( "thorns-capsule-btn--accent", followOn );
		_followBtn.SetClass( "thorns-capsule-btn--secondary", !followOn );
		_stayBtn.SetClass( "thorns-capsule-btn--accent", !followOn );
		_stayBtn.SetClass( "thorns-capsule-btn--secondary", followOn );
		_followBtn.SetClass( "active", followOn );
		_stayBtn.SetClass( "active", !followOn );

		RebuildAbilityStrip( def );
	}

	void AddStatRow( string label, string valueText, float fill01 )
	{
		var row = ThornsUiPanelAdd.AddChildPanel(_statRowsHost,  "thorns-tames-stat-row" );
		row.Style.FlexDirection = FlexDirection.Row;
		row.Style.AlignItems = Align.Center;
		row.Style.MarginBottom = Length.Pixels( 8 );

		var lab = row.AddChild( new Label( label, "thorns-tames-stat-label" ) );
		lab.Style.Width = Length.Pixels( 72 );
		lab.Style.FlexShrink = 0;

		var track = ThornsUiPanelAdd.AddChildPanel(row,  "thorns-tames-stat-track" );
		track.Style.FlexGrow = 1;
		track.Style.Height = Length.Pixels( 8 );
		track.Style.MarginRight = Length.Pixels( 8 );

		var fill = ThornsUiPanelAdd.AddChildPanel(track,  "thorns-tames-stat-fill" );
		fill.Style.Width = Length.Fraction( Math.Clamp( fill01, 0f, 1f ) );

		row.AddChild( new Label( valueText, "thorns-tames-stat-value" ) );
	}

	void RebuildAbilityStrip( ThornsWildlifeSpeciesDefinition def )
	{
		_abilityStrip.DeleteChildren();
		if ( def is null )
			return;

		void AddIcon( string glyph )
		{
			var b = ThornsUiPanelAdd.AddChildPanel(_abilityStrip,  "thorns-tames-ability-icon" );
			b.AddChild( new Label( glyph, "thorns-tames-ability-icon-inner" ) );
		}

		AddIcon( def.IsPredator ? "◎" : "☘" );
		AddIcon( def.MeleeDamage > 0.01f ? "⚔" : "·" );
		AddIcon( def.UseLineOfSight ? "◎" : "○" );
		AddIcon( "↻" );
	}

	void RebuildAbilitiesDetail( ThornsWildlifeIdentity sel, ThornsWildlifeSpeciesDefinition def )
	{
		_abilitiesBody.DeleteChildren();
		if ( sel is null || !sel.IsValid() || def is null )
			return;

		AddAbilitiesLine( $"Attack Type: {ThornsTameAbilitiesCopy.AttackTypeLabel( def )}", "thorns-tames-abilities-kv" );
		ThornsUiPanelAdd.AddChildPanel( _abilitiesBody, "thorns-tames-abilities-gap" );

		if ( def.AllowPlayerMount )
		{
			AddAbilitiesLine( ThornsTameAbilitiesCopy.MountHowToSectionTitle, "thorns-tames-abilities-subhead" );
			var mountLines = new List<string>();
			ThornsTameAbilitiesCopy.CollectMountHowToLines( def, mountLines );
			foreach ( var line in mountLines )
				AddAbilitiesBullet( line );
			ThornsUiPanelAdd.AddChildPanel( _abilitiesBody, "thorns-tames-abilities-gap" );
		}

		var tier = sel.TameQualityTier;
		AddAbilitiesLine( $"{tier.DisplayName()} abilities:", "thorns-tames-abilities-subhead" );

		var rarity = new List<string>();
		ThornsTameAbilitiesCopy.CollectRarityAbilityLines( def, sel, rarity );
		foreach ( var line in rarity )
			AddAbilitiesBullet( line );

		ThornsUiPanelAdd.AddChildPanel( _abilitiesBody, "thorns-tames-abilities-gap" );
		AddAbilitiesLine( "Bond training / upgrades:", "thorns-tames-abilities-subhead" );

		var train = new List<string>();
		ThornsTameAbilitiesCopy.CollectTrainingLines( def, sel, train );
		foreach ( var line in train )
			AddAbilitiesBullet( line );
	}

	void AddAbilitiesLine( string text, string classes )
	{
		var l = _abilitiesBody.AddChild( new Label( text, classes ) );
		l.Style.WhiteSpace = WhiteSpace.Normal;
		l.Style.Width = Length.Fraction( 1f );
	}

	void AddAbilitiesBullet( string text )
	{
		var l = _abilitiesBody.AddChild( new Label( $"– {text}", "thorns-tames-abilities-bullet" ) );
		l.Style.WhiteSpace = WhiteSpace.Normal;
		l.Style.Width = Length.Fraction( 1f );
	}

	void PushSummon()
	{
		if ( _selectedWildlifeId == Guid.Empty || _pawnRoot is null || !_pawnRoot.IsValid() )
			return;

		var interactor = _pawnRoot.Components.Get<ThornsWildlifeTameInteractor>();
		if ( interactor.IsValid() )
			interactor.RequestSummonTame( _selectedWildlifeId );
	}

	void PushFollow( bool follow )
	{
		if ( _selectedWildlifeId == Guid.Empty || _pawnRoot is null || !_pawnRoot.IsValid() )
			return;

		var interactor = _pawnRoot.Components.Get<ThornsWildlifeTameInteractor>();
		if ( interactor.IsValid() )
			interactor.RequestSetTameFollow( _selectedWildlifeId, follow );
	}

	void PushFeed()
	{
		if ( _selectedWildlifeId == Guid.Empty || _pawnRoot is null || !_pawnRoot.IsValid() )
			return;

		var interactor = _pawnRoot.Components.Get<ThornsWildlifeTameInteractor>();
		if ( interactor.IsValid() )
			interactor.RequestFeedTame( _selectedWildlifeId );
	}

	void PushTameStatUpgrade( int statKind )
	{
		if ( _selectedWildlifeId == Guid.Empty || _pawnRoot is null || !_pawnRoot.IsValid() )
			return;

		var interactor = _pawnRoot.Components.Get<ThornsWildlifeTameInteractor>();
		if ( interactor.IsValid() )
			interactor.RequestApplyTameStatUpgrade( _selectedWildlifeId, statKind );
	}

	void PushApplyName()
	{
		if ( _selectedWildlifeId == Guid.Empty || _pawnRoot is null || !_pawnRoot.IsValid() )
			return;

		var interactor = _pawnRoot.Components.Get<ThornsWildlifeTameInteractor>();
		if ( interactor.IsValid() )
			interactor.RequestSetTameDisplayName( _selectedWildlifeId, _nameEntry.Text ?? "" );
		_nameEntryFocused = false;
	}
}
