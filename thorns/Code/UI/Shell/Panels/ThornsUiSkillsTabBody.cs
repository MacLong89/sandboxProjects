#nullable disable

using System;
using Sandbox.UI;

namespace Sandbox;

/// <summary>Skills tab: category rail · node grid · detail + split unlock (uses <see cref="ThornsPlayerUpgrades"/>).</summary>
public sealed class ThornsUiSkillsTabBody : Panel
{
	const string PanelHiddenClass = "thorns-tames-panel-hidden";

	readonly Label _headTitle;
	readonly Panel _navRowsHost;
	readonly Panel[] _navRows;
	readonly Panel[] _navFills;
	readonly Label[] _navCounts;
	readonly Panel _treeWrap;
	readonly Panel _treeGrid;
	readonly Panel _detail;
	readonly Label _pointsLabel;
	readonly Panel _detailIconWrap;
	readonly Panel _detailIconImg;
	readonly Label _detailIconFallback;
	readonly Label _detailTitle;
	readonly Label _detailKind;
	readonly Label _detailRankUps;
	readonly Panel _detailMid;
	readonly Label _detailDesc;
	readonly Panel _detailStats;
	readonly Panel _unlockRow;
	readonly Label _unlockLeftLbl;
	readonly Label _unlockRightLbl;

	int _navIndex;
	ThornsUpgradeCategory? _selected;
	GameObject _pawnRoot;
	double _lastRebuildTime = -9999;

	static readonly (string Id, string Title, string Icon, ThornsUpgradeCategory[] Cats)[] NavDef =
	[
		(
			"persistence",
			"PERSISTENCE",
			"◎",
			[
				ThornsUpgradeCategory.Hydration,
				ThornsUpgradeCategory.IronGut,
				ThornsUpgradeCategory.StrongStomach,
				ThornsUpgradeCategory.Weathered,
				ThornsUpgradeCategory.ThickHide
			] ),
		(
			"instinct",
			"INSTINCT",
			"⚔",
			[
				ThornsUpgradeCategory.Endurance,
				ThornsUpgradeCategory.Ghost,
				ThornsUpgradeCategory.Beastmaster,
				ThornsUpgradeCategory.Hardened,
				ThornsUpgradeCategory.LuckyChamber
			] ),
		(
			"industry",
			"INDUSTRY",
			"⚒",
			[
				ThornsUpgradeCategory.Lumberjack,
				ThornsUpgradeCategory.Miner,
				ThornsUpgradeCategory.Scavenger,
				ThornsUpgradeCategory.Reinforced,
				ThornsUpgradeCategory.Technician
			] ),
	];

	public ThornsUiSkillsTabBody()
	{
		Log.Info( "[Thorns UI] Skills tab v2 — trees PERSISTENCE / INSTINCT / INDUSTRY (5 skills each)." );
		AddClass( "thorns-tab-skills" );
		AddClass( "thorns-skills-layout-root" );
		Style.FlexGrow = 1;
		Style.FlexShrink = 1;
		Style.MinHeight = 0;
		Style.Width = Length.Fraction( 1f );
		Style.FlexDirection = FlexDirection.Row;
		// Avoid split overflow rules in companion SCSS (validator rejects some overflow-y values there).
		Style.Overflow = OverflowMode.Hidden;

		var nav = ThornsUiPanelAdd.AddChildPanel(this,  "thorns-skills-nav" );
		nav.Style.FlexDirection = FlexDirection.Column;
		// 2:3:5 flex-grow → 20% / 30% / 50% of row after gaps (percent basis + gap would overflow 100%).
		nav.Style.FlexGrow = 2;
		nav.Style.FlexShrink = 1;
		nav.Style.FlexBasis = Length.Pixels( 0 );
		nav.Style.MinWidth = Length.Pixels( 120 );

		_headTitle = nav.AddChild( new Label( "SKILLS", "thorns-skills-nav-brand" ) );
		_headTitle.Style.PointerEvents = PointerEvents.None;
		_headTitle.Style.MarginBottom = Length.Pixels( 10 );

		_navRowsHost = ThornsUiPanelAdd.AddChildPanel(nav,  "thorns-skills-nav-rows" );
		_navRowsHost.Style.FlexDirection = FlexDirection.Column;
		_navRowsHost.Style.FlexGrow = 1;

		_navRows = new Panel[NavDef.Length];
		_navFills = new Panel[NavDef.Length];
		_navCounts = new Label[NavDef.Length];
		for ( var i = 0; i < NavDef.Length; i++ )
		{
			var idx = i;
			var row = ThornsUiPanelAdd.AddChildPanel(_navRowsHost,  "thorns-skills-nav-row" );
			row.Style.FlexDirection = FlexDirection.Row;
			row.Style.AlignItems = Align.Center;
			row.Style.PointerEvents = PointerEvents.All;
			row.Style.Padding = Length.Pixels( 10 );
			row.AddEventListener( "onmousedown", _ => SelectNav( idx ) );

			var glyph = row.AddChild( new Label( NavDef[i].Icon, "thorns-skills-nav-row-icon" ) );
			glyph.Style.PointerEvents = PointerEvents.None;
			glyph.Style.MarginRight = Length.Pixels( 10 );

			var mid = ThornsUiPanelAdd.AddChildPanel(row,  "thorns-skills-nav-row-mid" );
			mid.Style.FlexGrow = 1;
			mid.Style.FlexDirection = FlexDirection.Column;
			mid.Style.PointerEvents = PointerEvents.None;
			mid.AddChild( new Label( NavDef[i].Title, "thorns-skills-nav-row-title" ) );

			var track = ThornsUiPanelAdd.AddChildPanel(mid,  "thorns-skills-nav-mini-track" );
			track.Style.MarginTop = Length.Pixels( 6 );
			var fill = ThornsUiPanelAdd.AddChildPanel(track,  "thorns-skills-nav-mini-fill" );
			_navFills[i] = fill;

			var count = row.AddChild( new Label( "0/0", "thorns-skills-nav-row-count" ) );
			count.Style.PointerEvents = PointerEvents.None;
			count.Style.MarginLeft = Length.Pixels( 8 );
			_navCounts[i] = count;

			_navRows[i] = row;
		}

		_treeWrap = ThornsUiPanelAdd.AddChildPanel(this,  "thorns-skills-tree-wrap" );
		_treeWrap.Style.FlexGrow = 3;
		_treeWrap.Style.FlexShrink = 1;
		_treeWrap.Style.FlexBasis = Length.Pixels( 0 );
		_treeWrap.Style.MinWidth = Length.Pixels( 0 );
		_treeWrap.Style.MinHeight = 0;
		_treeWrap.Style.FlexDirection = FlexDirection.Column;
		_treeWrap.Style.Overflow = OverflowMode.Hidden;

		_treeGrid = ThornsUiPanelAdd.AddChildPanel(_treeWrap,  "thorns-skills-tree-grid" );
		_treeGrid.Style.FlexGrow = 1;
		_treeGrid.Style.FlexShrink = 0;
		_treeGrid.Style.MinHeight = Length.Pixels( 120 );
		_treeGrid.Style.Overflow = OverflowMode.Scroll;
		_treeGrid.Style.FlexDirection = FlexDirection.Column;
		_treeGrid.Style.JustifyContent = Justify.FlexStart;
		_treeGrid.Style.AlignItems = Align.Center;
		_treeGrid.Style.Padding = Length.Pixels( 14 );

		_detail = ThornsUiPanelAdd.AddChildPanel(this,  "thorns-skills-detail" );
		_detail.Style.FlexDirection = FlexDirection.Column;
		_detail.Style.JustifyContent = Justify.FlexStart;
		_detail.Style.FlexGrow = 5;
		_detail.Style.FlexShrink = 1;
		_detail.Style.FlexBasis = Length.Pixels( 0 );
		_detail.Style.MinHeight = 0;
		_detail.Style.MinWidth = Length.Pixels( 0 );
		_detail.Style.ZIndex = 1;

		_pointsLabel = _detail.AddChild( new Label( "— POINTS AVAILABLE", "thorns-skills-points" ) );
		_pointsLabel.Style.PointerEvents = PointerEvents.None;
		_pointsLabel.Style.MarginBottom = Length.Pixels( 12 );

		var hero = ThornsUiPanelAdd.AddChildPanel(_detail,  "thorns-skills-detail-hero" );
		hero.Style.FlexDirection = FlexDirection.Row;
		hero.Style.AlignItems = Align.FlexStart;
		// Bottom spacing + overflow live in `.thorns-skills-detail-hero` SCSS (avoid clipping desc).
		_detailIconWrap = ThornsUiPanelAdd.AddChildPanel(hero,  "thorns-skills-detail-icon-wrap" );
		_detailIconWrap.Style.FlexShrink = 0;
		_detailIconWrap.Style.MarginRight = Length.Pixels( 16 );
		_detailIconWrap.Style.PointerEvents = PointerEvents.None;

		_detailIconImg = ThornsUiPanelAdd.AddChildPanel(_detailIconWrap,  "thorns-skills-detail-icon-img" );
		_detailIconImg.Style.PointerEvents = PointerEvents.None;

		_detailIconFallback = _detailIconWrap.AddChild( new Label( "?", "thorns-skills-detail-icon-fallback" ) );
		_detailIconFallback.Style.PointerEvents = PointerEvents.None;
		var titles = ThornsUiPanelAdd.AddChildPanel(hero,  "thorns-skills-detail-titles" );
		titles.Style.FlexDirection = FlexDirection.Column;
		titles.Style.FlexGrow = 1;
		_detailTitle = titles.AddChild( new Label( "—", "thorns-skills-detail-name" ) );
		_detailTitle.Style.PointerEvents = PointerEvents.None;
		_detailKind = titles.AddChild( new Label( "Passive", "thorns-skills-detail-kind" ) );
		_detailKind.Style.PointerEvents = PointerEvents.None;

		_detailRankUps = titles.AddChild( new Label( "", "thorns-skills-detail-ranks" ) );
		_detailRankUps.Style.PointerEvents = PointerEvents.None;
		_detailRankUps.Style.MarginTop = Length.Pixels( 6 );

		// Outside scroll region; wrapper keeps description below glyph glow (hero padding also expands layout).
		var descBlock = ThornsUiPanelAdd.AddChildPanel(_detail,  "thorns-skills-detail-desc-block" );
		descBlock.Style.FlexDirection = FlexDirection.Column;
		descBlock.Style.FlexShrink = 0;
		descBlock.Style.Width = Length.Fraction( 1f );
		descBlock.Style.Overflow = OverflowMode.Visible;

		_detailDesc = descBlock.AddChild( new Label( "", "thorns-skills-detail-desc" ) );
		_detailDesc.Style.PointerEvents = PointerEvents.None;
		_detailDesc.Style.WhiteSpace = WhiteSpace.Normal;
		_detailDesc.Style.FlexShrink = 0;
		_detailDesc.Style.Width = Length.Fraction( 1f );
		_detailDesc.Style.MarginBottom = Length.Pixels( 10 );

		_detailMid = ThornsUiPanelAdd.AddChildPanel(_detail,  "thorns-skills-detail-mid" );
		_detailMid.Style.FlexDirection = FlexDirection.Column;
		// Do not flex-grow — was eating all space below stats and pinned UNLOCK to the panel bottom.
		_detailMid.Style.FlexGrow = 0;
		_detailMid.Style.FlexShrink = 0;
		_detailMid.Style.MinHeight = 0;
		_detailMid.Style.Width = Length.Fraction( 1f );
		_detailMid.Style.Overflow = OverflowMode.Scroll;

		_detailStats = ThornsUiPanelAdd.AddChildPanel(_detailMid,  "thorns-skills-detail-stats" );
		_detailStats.Style.FlexDirection = FlexDirection.Column;
		_detailStats.Style.FlexShrink = 0;
		_detailStats.Style.MarginBottom = Length.Pixels( 4 );

		_unlockRow = ThornsUiPanelAdd.AddChildPanel(_detail,  "thorns-skills-unlock-split" );
		_unlockRow.Style.FlexDirection = FlexDirection.Row;
		_unlockRow.Style.FlexShrink = 0;
		_unlockRow.Style.PointerEvents = PointerEvents.All;
		_unlockRow.AddEventListener( "onmousedown", () => OnUnlockPressed() );

		var left = ThornsUiPanelAdd.AddChildPanel(_unlockRow,  "thorns-skills-unlock-split-left" );
		left.Style.FlexGrow = 1;
		_unlockLeftLbl = left.AddChild( new Label( "UNLOCK", "thorns-skills-unlock-split-left-label" ) );

		var right = ThornsUiPanelAdd.AddChildPanel(_unlockRow,  "thorns-skills-unlock-split-right" );
		right.Style.FlexShrink = 0;
		_unlockRightLbl = right.AddChild( new Label( "—", "thorns-skills-unlock-split-right-label" ) );

		SelectNav( 0 );
	}

	void SelectNav( int index )
	{
		_navIndex = Math.Clamp( index, 0, NavDef.Length - 1 );
		var cats = NavDef[_navIndex].Cats;
		_selected = cats.Length > 0 ? cats[0] : null;
		for ( var i = 0; i < _navRows.Length; i++ )
			_navRows[i].SetClass( "thorns-skills-nav-row--active", i == _navIndex );
		RefreshFromPawn( _pawnRoot, force: true );
	}

	void OnUnlockPressed()
	{
		if ( _selected is null || _pawnRoot is null || !_pawnRoot.IsValid() )
			return;
		var ups = _pawnRoot.Components.Get<ThornsPlayerUpgrades>();
		if ( !ups.IsValid() )
			return;
		var rank = ups.GetRank( _selected.Value );
		if ( rank >= ups.GetMaxRank( _selected.Value ) )
			return;
		var cost = ThornsUpgradeBalance.NextPurchaseUpgradePointCost( ups.GetBaseCost( _selected.Value ), rank );
		if ( ups.UnspentUpgradePoints < cost )
			return;
		ups.RpcRequestPurchaseUpgrade( (int)_selected.Value );
		RefreshFromPawn( _pawnRoot, force: true );
	}

	public void RefreshFromPawn( GameObject pawnRoot, bool force = false )
	{
		if ( !IsValid )
			return;

		_pawnRoot = pawnRoot;

		if ( !force && Time.Now - _lastRebuildTime < 0.18 )
			return;
		_lastRebuildTime = Time.Now;

		var ups = pawnRoot.IsValid() ? pawnRoot.Components.Get<ThornsPlayerUpgrades>() : null;

		for ( var i = 0; i < NavDef.Length; i++ )
			UpdateNavRow( i, ups );

		RebuildTree( ups );
		RefreshDetail( ups );
	}

	void UpdateNavRow( int i, ThornsPlayerUpgrades ups )
	{
		var cats = NavDef[i].Cats;
		var owned = 0;
		foreach ( var c in cats )
		{
			if ( ups.IsValid() && ups.GetRank( c ) > 0 )
				owned++;
		}

		var total = cats.Length;
		_navCounts[i].Text = $"{owned}/{total}";
		var frac = total > 0 ? owned / (float)total : 0f;
		_navFills[i].Style.Width = Length.Fraction( frac );
	}

	void RebuildTree( ThornsPlayerUpgrades ups )
	{
		_treeGrid.DeleteChildren();

		if ( ups is null || !ups.IsValid() )
		{
			_treeGrid.AddChild( new Label(
				"Upgrades unavailable on this pawn.",
				"thorns-tab-context-placeholder" ) );
			return;
		}

		var cats = NavDef[_navIndex].Cats;
		// One column of nodes — horizontal rows of 76px rings overflowed the narrow center column
		// (overflow-x hidden), so only ~2 skills appeared. Vertical stack always shows all five.
		foreach ( var cat in cats )
		{
			var c = cat;
			var node = ThornsUiPanelAdd.AddChildPanel(_treeGrid,  "thorns-skills-tree-node" );
			node.Style.FlexShrink = 0;
			node.Style.PointerEvents = PointerEvents.All;
			node.AddEventListener( "onmousedown", _ => { _selected = c; RefreshDetail( ups ); RebuildTree( ups ); } );

			var rank = ups.IsValid() ? ups.GetRank( c ) : 0;
			var cap = ups.IsValid() ? ups.GetMaxRank( c ) : 0;
			var maxed = ups.IsValid() && rank >= cap;
			var cost = ups.IsValid() && !maxed
				? ThornsUpgradeBalance.NextPurchaseUpgradePointCost( ups.GetBaseCost( c ), rank )
				: 0;
			var canBuy = ups.IsValid() && !maxed && ups.UnspentUpgradePoints >= cost;

			node.SetClass( "thorns-skills-tree-node--picked", _selected == c );
			node.SetClass( "thorns-skills-tree-node--max", maxed );
			node.SetClass( "thorns-skills-tree-node--afford", !maxed && canBuy );
			node.SetClass( "thorns-skills-tree-node--locked", !maxed && !canBuy );

			var ring = ThornsUiPanelAdd.AddChildPanel(node,  "thorns-skills-tree-node-ring" );
			ring.Style.PointerEvents = PointerEvents.None;

			var iconStack = ThornsUiPanelAdd.AddChildPanel(ring,  "thorns-skills-tree-node-icon-stack" );
			iconStack.Style.PointerEvents = PointerEvents.None;

			var iconImg = ThornsUiPanelAdd.AddChildPanel(iconStack,  "thorns-skills-tree-node-icon-img" );
			iconImg.Style.PointerEvents = PointerEvents.None;
			var abbr = iconStack.AddChild( new Label( Abbr( c ), "thorns-skills-tree-node-abbr" ) );
			abbr.Style.PointerEvents = PointerEvents.None;
			BindSkillIconVisual( iconImg, abbr, c );

			if ( !maxed && !canBuy )
				ring.AddChild( new Label( "⌇", "thorns-skills-tree-node-lock" ) );
		}
	}

	static string Abbr( ThornsUpgradeCategory c ) => c switch
	{
		ThornsUpgradeCategory.Hydration => "HY",
		ThornsUpgradeCategory.IronGut => "IG",
		ThornsUpgradeCategory.StrongStomach => "SS",
		ThornsUpgradeCategory.Weathered => "WV",
		ThornsUpgradeCategory.ThickHide => "TH",
		ThornsUpgradeCategory.Endurance => "EN",
		ThornsUpgradeCategory.Ghost => "GH",
		ThornsUpgradeCategory.Beastmaster => "BM",
		ThornsUpgradeCategory.Hardened => "HD",
		ThornsUpgradeCategory.LuckyChamber => "LC",
		ThornsUpgradeCategory.Lumberjack => "LJ",
		ThornsUpgradeCategory.Miner => "MN",
		ThornsUpgradeCategory.Scavenger => "SC",
		ThornsUpgradeCategory.Reinforced => "RF",
		ThornsUpgradeCategory.Technician => "TC",
		_ => "?"
	};

	void RefreshDetail( ThornsPlayerUpgrades ups )
	{
		if ( ups is null || !ups.IsValid() )
		{
			_pointsLabel.Text = "— POINTS AVAILABLE";
			SetDetailIconVisual( null, "?" );
			_detailTitle.Text = "No pawn";
			_detailKind.Text = "";
			_detailRankUps.Text = "";
			_detailRankUps.SetClass( "thorns-skills-detail-ranks--invested", false );
			_detailDesc.Text = "Connect or spawn to view upgrades.";
			_detailStats.DeleteChildren();
			SetPanelTabShown( _unlockRow, false );
			return;
		}

		SetPanelTabShown( _unlockRow, true );

		_pointsLabel.Text = $"{ups.UnspentUpgradePoints} POINTS AVAILABLE";

		if ( _selected is null )
		{
			SetDetailIconVisual( null, "◇" );
			_detailTitle.Text = "Select a skill";
			_detailKind.Text = "";
			_detailRankUps.Text = "";
			_detailRankUps.SetClass( "thorns-skills-detail-ranks--invested", false );
			_detailDesc.Text = "Choose a node in the tree.";
			_detailStats.DeleteChildren();
			_unlockRightLbl.Text = "—";
			return;
		}

		var cat = _selected.Value;
		var rank = ups.GetRank( cat );
		var cap = ups.GetMaxRank( cat );
		var maxed = rank >= cap;
		var cost = maxed ? 0 : ThornsUpgradeBalance.NextPurchaseUpgradePointCost( ups.GetBaseCost( cat ), rank );
		var canBuy = !maxed && ups.UnspentUpgradePoints >= cost;

		SetDetailIconVisual( cat, Abbr( cat ) );
		_detailTitle.Text = Title( cat );
		_detailKind.Text = TreeKind( cat );
		_detailRankUps.Text = $"{rank} / {cap}";
		_detailRankUps.SetClass( "thorns-skills-detail-ranks--invested", rank > 0 );
		_detailDesc.Text = Description( cat );

		_detailStats.DeleteChildren();
		foreach ( var line in StatLines( cat, rank, ups ) )
		{
			var row = ThornsUiPanelAdd.AddChildPanel(_detailStats,  "thorns-skills-stat-line" );
			row.Style.FlexDirection = FlexDirection.Row;
			row.Style.MarginBottom = Length.Pixels( 4 );
			row.AddChild( new Label( "▸", "thorns-skills-stat-bullet" ) ).Style.PointerEvents = PointerEvents.None;
			row.AddChild( new Label( line, "thorns-skills-stat-text" ) ).Style.PointerEvents = PointerEvents.None;
		}

		if ( maxed )
		{
			_unlockLeftLbl.Text = "MASTERED";
			_unlockRightLbl.Text = $"Rank {rank}/{cap}";
			_unlockRow.SetClass( "thorns-skills-unlock-split--disabled", true );
		}
		else
		{
			_unlockLeftLbl.Text = "UNLOCK";
			_unlockRightLbl.Text = cost == 1 ? "1 Point" : $"{cost} Points";
			_unlockRow.SetClass( "thorns-skills-unlock-split--disabled", !canBuy );
		}
	}

	static string TreeKind( ThornsUpgradeCategory c )
	{
		if ( c <= ThornsUpgradeCategory.ThickHide )
			return "PERSISTENCE";
		if ( c <= ThornsUpgradeCategory.LuckyChamber )
			return "INSTINCT";
		return "INDUSTRY";
	}

	static string Title( ThornsUpgradeCategory c ) => c switch
	{
		ThornsUpgradeCategory.Hydration => "Hydration",
		ThornsUpgradeCategory.IronGut => "Iron Gut",
		ThornsUpgradeCategory.StrongStomach => "Strong Stomach",
		ThornsUpgradeCategory.Weathered => "Weathered",
		ThornsUpgradeCategory.ThickHide => "Thick Hide",
		ThornsUpgradeCategory.Endurance => "Endurance",
		ThornsUpgradeCategory.Ghost => "Ghost",
		ThornsUpgradeCategory.Beastmaster => "Beastmaster",
		ThornsUpgradeCategory.Hardened => "Hardened",
		ThornsUpgradeCategory.LuckyChamber => "Lucky Chamber",
		ThornsUpgradeCategory.Lumberjack => "Lumberjack",
		ThornsUpgradeCategory.Miner => "Miner",
		ThornsUpgradeCategory.Scavenger => "Scavenger",
		ThornsUpgradeCategory.Reinforced => "Reinforced",
		ThornsUpgradeCategory.Technician => "Technician",
		_ => c.ToString()
	};

	static string Description( ThornsUpgradeCategory c ) => c switch
	{
		ThornsUpgradeCategory.Hydration => "Liquids quench more thirst and thirst drains slower.",
		ThornsUpgradeCategory.IronGut => "Food satiates more hunger and hunger drains slower.",
		ThornsUpgradeCategory.StrongStomach => "Negative food effects are reduced.",
		ThornsUpgradeCategory.Weathered => "Environmental survival damage is reduced (weather stress proxy).",
		ThornsUpgradeCategory.ThickHide => "Take reduced damage from wild animals.",
		ThornsUpgradeCategory.Endurance => "Stamina lasts longer — larger pool and slower sprint drain.",
		ThornsUpgradeCategory.Ghost => "Animals sense you from a shorter distance.",
		ThornsUpgradeCategory.Beastmaster => "Begin taming creatures at higher health thresholds.",
		ThornsUpgradeCategory.Hardened => "Take reduced damage from hostile human NPCs.",
		ThornsUpgradeCategory.LuckyChamber => "Small chance to not consume ammo when firing.",
		ThornsUpgradeCategory.Lumberjack => "Chance-based bonus yield when harvesting wood and fiber.",
		ThornsUpgradeCategory.Miner => "Chance-based bonus yield when harvesting stone and ore.",
		ThornsUpgradeCategory.Scavenger => "Loot crates may roll one bonus item when first opened.",
		ThornsUpgradeCategory.Reinforced => "Weapons lose durability slower when firing.",
		ThornsUpgradeCategory.Technician => "Increases crafting tier and unlocks advanced recipes.",
		_ => ""
	};

	static void BindSkillIconVisual( Panel img, Label fallback, ThornsUpgradeCategory c )
	{
		if ( ThornsSkillHudIcons.TryBindBackground( c, img ) )
		{
			fallback.Text = "";
			fallback.SetClass( "thorns-skills-tree-node-abbr--hidden", true );
		}
		else
		{
			img.Style.BackgroundImage = null;
			fallback.Text = Abbr( c );
			fallback.SetClass( "thorns-skills-tree-node-abbr--hidden", false );
		}
	}

	void SetDetailIconVisual( ThornsUpgradeCategory? c, string fallbackGlyph )
	{
		if ( c is { } cat && ThornsSkillHudIcons.TryBindBackground( cat, _detailIconImg ) )
		{
			_detailIconFallback.Text = "";
			_detailIconFallback.SetClass( "thorns-skills-detail-icon-fallback--hidden", true );
		}
		else
		{
			_detailIconImg.Style.BackgroundImage = null;
			_detailIconFallback.Text = fallbackGlyph ?? "";
			_detailIconFallback.SetClass( "thorns-skills-detail-icon-fallback--hidden", string.IsNullOrEmpty( fallbackGlyph ) );
		}
	}

	static IEnumerable<string> StatLines( ThornsUpgradeCategory c, int rank, ThornsPlayerUpgrades ups )
	{
		switch ( c )
		{
			case ThornsUpgradeCategory.Hydration:
				yield return
					$"Thirst drain ×{Math.Max( 0.38f, 1f - rank * ThornsUpgradeBalance.HydrationThirstDrainReductionPerRank ):F2}";
				yield return $"+{rank * ThornsUpgradeBalance.HydrationLiquidRestoreBonusPerRank * 100f:F0}% liquid thirst restore";
				break;
			case ThornsUpgradeCategory.IronGut:
				yield return
					$"Hunger drain ×{Math.Max( 0.38f, 1f - rank * ThornsUpgradeBalance.IronGutHungerDrainReductionPerRank ):F2}";
				yield return $"+{rank * ThornsUpgradeBalance.IronGutFoodRestoreBonusPerRank * 100f:F0}% food hunger restore";
				break;
			case ThornsUpgradeCategory.StrongStomach:
				yield return
					$"Poison gained ×{Math.Max( 0.12f, 1f - rank * ThornsUpgradeBalance.StrongStomachPoisonTakenReductionPerRank ):F2}";
				break;
			case ThornsUpgradeCategory.Weathered:
				yield return
					$"Env. damage ×{Math.Max( 0.35f, 1f - rank * ThornsUpgradeBalance.WeatheredEnvironmentalDamageReductionPerRank ):F2}";
				break;
			case ThornsUpgradeCategory.ThickHide:
				yield return $"-{Math.Min( 45f, rank * ThornsUpgradeBalance.ThickHideWildlifeDamageReductionPerRank * 100f ):F0}% damage from wildlife";
				break;
			case ThornsUpgradeCategory.Endurance:
				yield return $"+{rank * ThornsUpgradeBalance.EnduranceStaminaMaxBonusPerRank:F0} max stamina";
				yield return
					$"Sprint stamina drain ×{Math.Max( 0.55f, 1f - rank * ThornsUpgradeBalance.EnduranceStaminaDrainReductionPerRank ):F2}";
				break;
			case ThornsUpgradeCategory.Ghost:
				yield return
					$"Wildlife sense radius ×{ups.GetGhostWildlifeDetectionRadiusMultiplier():F2}";
				break;
			case ThornsUpgradeCategory.Beastmaster:
				yield return $"Tame when HP ≤ {ups.GetTamingHealthFractionThreshold() * 100f:F0}%";
				break;
			case ThornsUpgradeCategory.Hardened:
				yield return $"-{Math.Min( 45f, rank * ThornsUpgradeBalance.HardenedHumanNpcDamageReductionPerRank * 100f ):F0}% damage from bandit NPCs";
				break;
			case ThornsUpgradeCategory.LuckyChamber:
				yield return $"{ups.GetLuckyChamberProcChance() * 100f:F1}% chance to skip ammo use per shot";
				break;
			case ThornsUpgradeCategory.Lumberjack:
				yield return $"+{rank * ThornsUpgradeBalance.HarvestYieldBonusPerLumberjackRank * 100f:F0}% wood/fiber yield";
				break;
			case ThornsUpgradeCategory.Miner:
				yield return $"+{rank * ThornsUpgradeBalance.HarvestYieldBonusPerMinerRank * 100f:F0}% stone/ore yield";
				break;
			case ThornsUpgradeCategory.Scavenger:
				yield return
					$"{Math.Min( 58f, rank * ThornsUpgradeBalance.ScavengerExtraLootChancePerRank * 100f ):F0}% bonus loot roll on first open";
				break;
			case ThornsUpgradeCategory.Reinforced:
				yield return $"Weapon durability loss ×{ups.GetReinforcedDurabilityLossMultiplier():F2}";
				break;
			case ThornsUpgradeCategory.Technician:
				yield return $"Crafting tier T{ups.GetEffectiveCraftingTier()}";
				break;
		}
	}

	static void SetPanelTabShown( Panel panel, bool shown ) =>
		panel.SetClass( PanelHiddenClass, !shown );
}
