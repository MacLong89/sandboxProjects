#nullable disable

using System;
using System.Collections.Generic;
using System.Text;
using Sandbox.UI;

namespace Sandbox;

/// <summary>Guild roster management — add from server session or Steam friends.</summary>
public sealed class ThornsUiGuildTabBody : Panel
{
	readonly Panel _rosterList;
	readonly Panel _candidateList;
	readonly Label _rosterSubtitle;
	readonly Label _candidateSubtitle;
	readonly ThornsUiCapsuleButton _sourceServerBtn;
	readonly ThornsUiCapsuleButton _sourceFriendsBtn;

	readonly List<ThornsUiGuildMemberRow> _rosterRows = new();
	readonly List<ThornsUiGuildMemberRow> _candidateRows = new();
	readonly Label _rosterEmptyLabel;
	readonly Label _candidateEmptyLabel;

	GameObject _pawnRoot;
	ThornsGuildRoster _roster;
	bool _showFriends;
	string _lastRosterSig = "";
	string _lastCandidateSig = "";

	public ThornsUiGuildTabBody()
	{
		AddClass( "thorns-tab-guild" );
		Style.FlexGrow = 1;
		Style.FlexShrink = 1;
		Style.MinHeight = 0;
		Style.Width = Length.Fraction( 1f );
		Style.FlexDirection = FlexDirection.Column;

		var head = ThornsUiPanelAdd.AddChildPanel( this, "thorns-guild-head" );
		head.Style.FlexDirection = FlexDirection.Row;
		head.Style.AlignItems = Align.Center;
		head.Style.JustifyContent = Justify.SpaceBetween;
		head.Style.MarginBottom = Length.Pixels( 10 );
		head.Style.FlexShrink = 0;

		var headLeft = ThornsUiPanelAdd.AddChildPanel( head, "thorns-guild-head-left" );
		headLeft.Style.FlexDirection = FlexDirection.Column;
		headLeft.Style.PointerEvents = PointerEvents.None;
		headLeft.AddChild( new Label( "GUILD", "thorns-guild-title" ) );
		_rosterSubtitle = headLeft.AddChild( new Label( "0 members", "thorns-guild-subtitle" ) );

		var sourceRow = ThornsUiPanelAdd.AddChildPanel( head, "thorns-guild-source-row" );
		sourceRow.Style.FlexDirection = FlexDirection.Row;
		_sourceServerBtn = sourceRow.AddChild( new ThornsUiCapsuleButton( "On server", "accent", () => SetSource( false ) ) );
		_sourceFriendsBtn = sourceRow.AddChild( new ThornsUiCapsuleButton( "Party / friends", "secondary", () => SetSource( true ) ) );

		var body = ThornsUiPanelAdd.AddChildPanel( this, "thorns-guild-columns" );
		body.AddClass( "thorns-tab-guild-layout" );
		body.Style.FlexGrow = 1;
		body.Style.FlexShrink = 1;
		body.Style.MinHeight = 0;

		var rosterCol = ThornsUiPanelAdd.AddChildPanel( body, "thorns-guild-roster-col" );
		rosterCol.Style.FlexDirection = FlexDirection.Column;
		rosterCol.Style.FlexBasis = Length.Percent( 42f );
		rosterCol.Style.MinWidth = Length.Pixels( 220 );
		rosterCol.AddChild( new Label( "YOUR GUILD", "thorns-tab-section-title" ) ).Style.PointerEvents = PointerEvents.None;
		_rosterList = ThornsUiPanelAdd.AddChildPanel( rosterCol, "thorns-guild-roster-list" );
		_rosterList.Style.FlexGrow = 1;
		_rosterList.Style.Overflow = OverflowMode.Scroll;
		_rosterList.Style.PointerEvents = PointerEvents.All;

		var candCol = ThornsUiPanelAdd.AddChildPanel( body, "thorns-guild-candidate-col" );
		candCol.Style.FlexGrow = 1;
		candCol.Style.FlexDirection = FlexDirection.Column;
		candCol.Style.MinWidth = Length.Pixels( 260 );
		_candidateSubtitle = candCol.AddChild( new Label( "Players on this server", "thorns-tab-section-title" ) );
		_candidateSubtitle.Style.PointerEvents = PointerEvents.None;
		_candidateList = ThornsUiPanelAdd.AddChildPanel( candCol, "thorns-guild-candidate-list" );
		_candidateList.Style.FlexGrow = 1;
		_candidateList.Style.Overflow = OverflowMode.Scroll;
		_candidateList.Style.PointerEvents = PointerEvents.All;

		_rosterEmptyLabel = _rosterList.AddChild( new Label(
			"Add players from the list on the right.",
			"thorns-tab-context-placeholder" ) );
		_rosterEmptyLabel.Style.PointerEvents = PointerEvents.None;
		_rosterEmptyLabel.Style.MarginTop = Length.Pixels( 8 );
		_rosterEmptyLabel.Style.Display = DisplayMode.None;

		_candidateEmptyLabel = _candidateList.AddChild( new Label(
			"No other players on this server right now.",
			"thorns-tab-context-placeholder" ) );
		_candidateEmptyLabel.Style.PointerEvents = PointerEvents.None;
		_candidateEmptyLabel.Style.WhiteSpace = WhiteSpace.Normal;
		_candidateEmptyLabel.Style.MarginTop = Length.Pixels( 8 );
		_candidateEmptyLabel.Style.Display = DisplayMode.None;

		SetSource( false );
	}

	void SetSource( bool friends )
	{
		_showFriends = friends;
		_sourceServerBtn.SetVariant( friends ? "secondary" : "accent" );
		_sourceFriendsBtn.SetVariant( friends ? "accent" : "secondary" );
		_candidateSubtitle.Text = friends ? "Party members & Steam friends in session" : "Players on this server";
		_lastCandidateSig = "";
		RefreshFromPawn( _pawnRoot, force: true );
	}

	public void RefreshFromPawn( GameObject pawnRoot, bool force )
	{
		_pawnRoot = pawnRoot;
		_roster = pawnRoot.IsValid() ? pawnRoot.Components.Get<ThornsGuildRoster>() : default;

		var rosterSig = BuildRosterSignature();
		if ( force || rosterSig != _lastRosterSig )
		{
			_lastRosterSig = rosterSig;
			RebuildRosterColumn();
		}

		var candidateSig = BuildCandidateSignature();
		if ( force || candidateSig != _lastCandidateSig )
		{
			_lastCandidateSig = candidateSig;
			RebuildCandidateColumn();
		}
	}

	string BuildRosterSignature()
	{
		if ( !_roster.IsValid() )
			return "0";

		return $"{_roster.MemberAccountKeys.Count}:{string.Join( "\n", _roster.MemberAccountKeys )}";
	}

	string BuildCandidateSignature()
	{
		if ( !_pawnRoot.IsValid() )
			return _showFriends ? "F:0" : "S:0";

		var entries = _showFriends
			? ThornsGuildLists.BuildSteamFriendEntries( _pawnRoot )
			: ThornsGuildLists.BuildServerPlayerEntries( _pawnRoot );

		var sb = new StringBuilder( _showFriends ? "F:" : "S:" );
		sb.Append( entries.Count );
		foreach ( var e in entries )
		{
			sb.Append( '|' );
			sb.Append( e.AccountKey );
			sb.Append( ':' );
			sb.Append( e.IsOnThisServer ? '1' : '0' );
		}

		return sb.ToString();
	}

	void RebuildRosterColumn()
	{
		foreach ( var r in _rosterRows )
		{
			if ( r.IsValid() )
				r.Delete();
		}

		_rosterRows.Clear();

		if ( !_roster.IsValid() )
		{
			_rosterSubtitle.Text = "0 members";
			return;
		}

		var keys = _roster.MemberAccountKeys;
		_rosterSubtitle.Text = $"{keys.Count}/{ThornsGuildRoster.MaxGuildMembers} members";

		foreach ( var key in keys )
		{
			var display = ResolveDisplayNameForKey( key );
			var row = _rosterList.AddChild( new ThornsUiGuildMemberRow(
				key,
				display,
				"On your roster — you won't damage them.",
				isMember: true,
				OnRemoveMember ) );
			_rosterRows.Add( row );
		}

		_rosterEmptyLabel.Style.Display = keys.Count == 0 ? DisplayMode.Flex : DisplayMode.None;
	}

	void RebuildCandidateColumn()
	{
		foreach ( var r in _candidateRows )
		{
			if ( r.IsValid() )
				r.Delete();
		}

		_candidateRows.Clear();

		if ( !_pawnRoot.IsValid() )
			return;

		var entries = _showFriends
			? ThornsGuildLists.BuildSteamFriendEntries( _pawnRoot )
			: ThornsGuildLists.BuildServerPlayerEntries( _pawnRoot );

		foreach ( var e in entries )
		{
			var already = _roster.IsValid() && _roster.ContainsAccountKey( e.AccountKey );
			var status = already
				? "Already in guild"
				: e.IsOnThisServer
					? "On this server"
					: e.IsOnline
						? "Online"
						: "Offline";

			var row = _candidateList.AddChild( new ThornsUiGuildMemberRow(
				e.AccountKey,
				e.DisplayName,
				status,
				isMember: already,
				already ? null : OnAddMember ) );
			_candidateRows.Add( row );
		}

		_candidateEmptyLabel.Text = _showFriends
			? "No party members or Steam friends in this session. Use \"On server\" or invite friends to your party first."
			: "No other players on this server right now.";
		_candidateEmptyLabel.Style.Display = entries.Count == 0 ? DisplayMode.Flex : DisplayMode.None;
	}

	string ResolveDisplayNameForKey( string accountKey )
	{
		if ( !_pawnRoot.IsValid() )
			return accountKey;

		foreach ( var e in ThornsGuildLists.BuildServerPlayerEntries( _pawnRoot ) )
		{
			if ( string.Equals( e.AccountKey, accountKey, StringComparison.Ordinal ) )
				return e.DisplayName;
		}

		foreach ( var e in ThornsGuildLists.BuildSteamFriendEntries( _pawnRoot ) )
		{
			if ( string.Equals( e.AccountKey, accountKey, StringComparison.Ordinal ) )
				return e.DisplayName;
		}

		if ( accountKey.StartsWith( "steam:", StringComparison.OrdinalIgnoreCase ) )
			return $"Steam {accountKey["steam:".Length..]}";

		return accountKey;
	}

	void OnAddMember( string accountKey )
	{
		if ( !_roster.IsValid() )
			return;

		_roster.RequestAddGuildMember( accountKey );
		_lastRosterSig = "";
		_lastCandidateSig = "";
		RefreshFromPawn( _pawnRoot, force: true );
	}

	void OnRemoveMember( string accountKey )
	{
		if ( !_roster.IsValid() )
			return;

		_roster.RequestRemoveGuildMember( accountKey );
		_lastRosterSig = "";
		_lastCandidateSig = "";
		RefreshFromPawn( _pawnRoot, force: true );
	}
}

sealed class ThornsUiGuildMemberRow : Panel
{
	public ThornsUiGuildMemberRow(
		string accountKey,
		string displayName,
		string statusLine,
		bool isMember,
		Action<string> onAction )
	{
		AddClass( "thorns-guild-member-row" );
		Style.PointerEvents = PointerEvents.All;
		Style.FlexDirection = FlexDirection.Row;
		Style.AlignItems = Align.Center;
		Style.JustifyContent = Justify.SpaceBetween;
		Style.Padding = Length.Pixels( 8 );
		Style.MarginBottom = Length.Pixels( 4 );

		var mid = ThornsUiPanelAdd.AddChildPanel( this, "thorns-guild-member-mid" );
		mid.Style.FlexDirection = FlexDirection.Column;
		mid.Style.FlexGrow = 1;
		mid.Style.PointerEvents = PointerEvents.None;

		var nameLbl = mid.AddChild( new Label( displayName, "thorns-guild-member-name" ) );
		nameLbl.Style.PointerEvents = PointerEvents.None;
		var statLbl = mid.AddChild( new Label( statusLine, "thorns-guild-member-status" ) );
		statLbl.Style.PointerEvents = PointerEvents.None;

		if ( onAction is not null )
		{
			var label = isMember ? "Remove" : "Add to guild";
			var variant = isMember ? "secondary" : "accent";
			var btn = AddChild( new ThornsUiCapsuleButton( label, variant, () => onAction( accountKey ) ) );
			btn.Style.FlexShrink = 0;
		}
	}
}
