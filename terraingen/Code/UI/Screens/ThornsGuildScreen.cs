namespace Terraingen.UI.Screens;

using Sandbox.UI;
using Terraingen;
using Terraingen.GameData;
using Terraingen.Player;
using Terraingen.UI;
using Terraingen.UI.Core;
using Terraingen.UI.Screens.Guild;

/// <summary>Guild command center — strategic faction dashboard consuming host-built snapshots.</summary>
public sealed partial class ThornsGuildScreen : ThornsScreenBase
{
	Panel _left;
	Panel _identityPanel;
	Panel _centerManagement;
	TextEntry _renameEntry;
	TextEntry _noticeEntry;
	bool _showGuildManagement;
	bool _isRebuilding;
	int _lastGuildRevision = -1;
	int _lastVictoryRevision = -1;

	public ThornsGuildScreen( ThornsMenuHost host, Panel parent ) : base( host, parent ) { }

	protected override void Build() => BuildGuildLayout();

	protected override void OnRevision( UiRevisionChannel channel, int revision )
	{
		_ = revision;
		if ( channel is UiRevisionChannel.Guild or UiRevisionChannel.Victory )
			Rebuild();
	}

	public override void OnShown()
	{
		ThornsPlayerGameplay.Local?.RequestVictoryUiRefresh();
		base.OnShown();
	}

	public override void Rebuild()
	{
		if ( !ThornsUiClientState.HasSnapshot || _isRebuilding )
			return;

		var guildRevision = UiRevisionBus.GetRevision( UiRevisionChannel.Guild );
		var victoryRevision = UiRevisionBus.GetRevision( UiRevisionChannel.Victory );
		if ( guildRevision == _lastGuildRevision && victoryRevision == _lastVictoryRevision )
			return;

		_lastGuildRevision = guildRevision;
		_lastVictoryRevision = victoryRevision;

		_isRebuilding = true;
		try
		{
			RebuildAll();
		}
		finally
		{
			_isRebuilding = false;
		}
	}

	void RebuildAll()
	{
		var snap = ThornsUiClientState.Snapshot.Guild;
		var player = ThornsPlayerGameplay.Local;
		var accountKey = player?.AccountKey ?? "";

		if ( !snap.InGuild )
		{
			RebuildPending();
			return;
		}

		var command = snap.Command ?? new ThornsGuildCommandSnapshotDto();

		ThornsGuildPanelBuilders.BuildIdentityPanelConcept(
			_identityPanel, snap, accountKey,
			ref _renameEntry, ref _noticeEntry,
			OnGuildRenameCommitted, OnGuildNoticeCommitted,
			ToggleManagement );

		ThornsGuildPanelBuilders.BuildVictoryPathsPanelConcept( _centerVictory, command, Host );
		ThornsGuildPanelBuilders.BuildActivityPanelConcept( _centerActivity, command );
		ThornsGuildPanelBuilders.BuildMembersPanelConcept( _rightMembers, snap, accountKey, player, ToggleManagement );
		ThornsGuildPanelBuilders.BuildLeaderboardPanelConcept(
			_rightLeaderboard, command, snap.GuildId, (int)_leaderboardTab,
			tab => SetLeaderboardTab( (GuildLeaderboardTab)tab ) );

		if ( _showGuildManagement && _centerManagement is { IsValid: true } )
		{
			_centerManagement.Style.Display = DisplayMode.Flex;
			ThornsGuildPanelBuilders.BuildManagementPanel( _centerManagement, snap, accountKey, player );
		}
		else if ( _centerManagement is { IsValid: true } )
		{
			_centerManagement.Style.Display = DisplayMode.None;
			_centerManagement.DeleteChildren( true );
		}
	}

	void RebuildPending()
	{
		_identityPanel?.DeleteChildren( true );
		_centerVictory?.DeleteChildren( true );
		_centerActivity?.DeleteChildren( true );
		_rightMembers?.DeleteChildren( true );
		_rightLeaderboard?.DeleteChildren( true );
		ThornsTheme.CreateMuted( _identityPanel, "Setting up your guild on this server…" );
	}

	void ToggleManagement()
	{
		_showGuildManagement = !_showGuildManagement;
		_lastGuildRevision = -1;
		Rebuild();
	}

	void OnGuildRenameCommitted()
	{
		if ( _renameEntry is null || !_renameEntry.IsValid )
			return;

		var name = _renameEntry.Text?.Trim() ?? "";
		if ( string.IsNullOrEmpty( name ) )
			return;

		var snap = ThornsUiClientState.Snapshot.Guild;
		if ( string.Equals( snap.GuildName, name, StringComparison.Ordinal ) )
			return;

		ThornsPlayerGameplay.Local?.RequestGuildRename( name );
	}

	void OnGuildNoticeCommitted()
	{
		if ( _noticeEntry is null || !_noticeEntry.IsValid )
			return;

		var text = _noticeEntry.Text?.Trim() ?? "";
		var snap = ThornsUiClientState.Snapshot.Guild;
		if ( string.Equals( snap.Notice?.Message ?? snap.Announcement, text, StringComparison.Ordinal ) )
			return;

		ThornsPlayerGameplay.Local?.RequestGuildAnnouncement( text );
	}
}
