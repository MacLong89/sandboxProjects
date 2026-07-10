namespace Sandbox;

/// <summary>Intermission lobby selections before the next match starts.</summary>
public sealed class AimboxLobbyState
{
	public string SelectedMapId { get; private set; } = AimboxPlayLobbyUiHelpers.DefaultMapId;
	public string LocalMapVoteId { get; private set; }

	public bool BotsEnabled { get; set; } = true;
	public bool FriendlyFire { get; set; } = false;
	public bool KillCamEnabled { get; set; } = true;
	public string HudStyle { get; set; } = "Default";
	public string TimeOfDay { get; set; } = "Day";
	public string Weather { get; set; } = "Clear";

	public string PartyPrivacy { get; set; } = "Friends Only";
	public bool VoiceChatEnabled { get; set; } = true;
	public bool AutoBalanceEnabled { get; set; } = true;
	public bool CrossplayEnabled { get; set; } = true;

	readonly Dictionary<string, int> _mapVoteCounts = new();
	readonly Dictionary<string, string> _mapVotesByVoter = new();
	readonly Dictionary<string, AimboxTeam> _teamPicks = new();

	public IReadOnlyDictionary<string, int> MapVoteCounts => _mapVoteCounts;
	public IReadOnlyDictionary<string, AimboxTeam> TeamPicks => _teamPicks;

	public void ResetForLobby()
	{
		_mapVoteCounts.Clear();
		_mapVotesByVoter.Clear();
		_teamPicks.Clear();
		LocalMapVoteId = AimboxPlayLobbyUiHelpers.DefaultMapId;
		SelectedMapId = AimboxPlayLobbyUiHelpers.DefaultMapId;
		_mapVoteCounts[AimboxPlayLobbyUiHelpers.DefaultMapId] = 1;
	}

	public AimboxTeam GetTeamPick( string actorId )
	{
		if ( string.IsNullOrWhiteSpace( actorId ) )
			return AimboxTeam.None;

		return _teamPicks.GetValueOrDefault( actorId, AimboxTeam.None );
	}

	public void SetTeamPick( string actorId, AimboxTeam team )
	{
		if ( string.IsNullOrWhiteSpace( actorId ) )
			return;

		if ( team is AimboxTeam.None )
			_teamPicks.Remove( actorId );
		else
			_teamPicks[actorId] = team;
	}

	public void ResetTeamPicks() => _teamPicks.Clear();

	public int MapVotes( string mapId ) =>
		_mapVoteCounts.GetValueOrDefault( AimboxPlayLobbyUiHelpers.NormalizeMapId( mapId ) );

	public void CastMapVote( string mapId, string voterId )
	{
		if ( string.IsNullOrWhiteSpace( voterId ) )
			return;

		mapId = AimboxPlayLobbyUiHelpers.NormalizeMapId( mapId );

		if ( !AimboxPlayLobbyUiHelpers.IsMapPlayable( mapId ) )
			return;

		if ( _mapVotesByVoter.TryGetValue( voterId, out var previous ) && previous == mapId )
			return;

		if ( _mapVotesByVoter.TryGetValue( voterId, out var oldVote ) )
		{
			_mapVoteCounts[oldVote] = Math.Max( 0, _mapVoteCounts.GetValueOrDefault( oldVote ) - 1 );
			if ( _mapVoteCounts.GetValueOrDefault( oldVote ) <= 0 )
				_mapVoteCounts.Remove( oldVote );
		}

		_mapVotesByVoter[voterId] = mapId;
		_mapVoteCounts[mapId] = _mapVoteCounts.GetValueOrDefault( mapId ) + 1;

		if ( voterId.Equals( "offline", StringComparison.OrdinalIgnoreCase )
			|| AimboxGame.Instance?.Players.Any( p => !p.IsProxy && p.AccountId == voterId ) == true )
			LocalMapVoteId = mapId;

		SelectedMapId = ResolveLeadingMap();
	}

	string ResolveLeadingMap()
	{
		var bestId = AimboxPlayLobbyUiHelpers.DefaultMapId;
		var bestVotes = -1;

		foreach ( var map in AimboxPlayLobbyUiHelpers.Maps )
		{
			if ( !map.Playable )
				continue;

			var votes = MapVotes( map.Id );
			if ( votes > bestVotes )
			{
				bestVotes = votes;
				bestId = map.Id;
			}
		}

		return bestId;
	}

	public void ForceSelectedMap( string mapId )
	{
		mapId = AimboxPlayLobbyUiHelpers.NormalizeMapId( mapId );
		if ( !AimboxPlayLobbyUiHelpers.IsMapPlayable( mapId ) )
			mapId = AimboxPlayLobbyUiHelpers.DefaultMapId;

		SelectedMapId = mapId;
		LocalMapVoteId = mapId;
		_mapVoteCounts.Clear();
		_mapVoteCounts[mapId] = 1;
	}
}
