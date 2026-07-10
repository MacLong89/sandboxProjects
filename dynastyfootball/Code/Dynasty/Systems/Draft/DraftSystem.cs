using Dynasty.Core;
using Dynasty.Core.Enums;
using Dynasty.Core.Events;
using Dynasty.Core.Interfaces;
using Dynasty.Core.Identifiers;
using Dynasty.Systems.Franchise;
using Dynasty.Systems.News;
using Dynasty.Data;
using DepthChartData = Dynasty.Data.DepthChart;
using Dynasty.Domain.Draft;
using Dynasty.Domain.Factories;
using Dynasty.Domain.League;
using Dynasty.Domain.Players;
using Dynasty.Domain.Teams;
using Dynasty.Systems.Formation;
using Dynasty.Systems.Season;
using Dynasty.Systems.Contracts;

namespace Dynasty.Systems.Draft;

public sealed class DraftSystem : ILeagueSystem
{
	public string SystemId => "draft";

	private LeagueSystemContext _context;
	private InboxSystem _inbox;
	private NewsSystem _news;
	private FranchiseRetentionSystem _retention;

	public void Register( LeagueSystemContext context ) => _context = context;

	public void SetInboxSystem( InboxSystem inbox ) => _inbox = inbox;

	public void SetNewsSystem( NewsSystem news ) => _news = news;

	public void SetFranchiseRetentionSystem( FranchiseRetentionSystem retention ) => _retention = retention;

	public void OnLeagueCreated( LeagueState state )
	{
		if ( state.Phase != LeaguePhase.Draft )
			return;

		BeginDraft( state );
		NotifyHumanOnClock( state );
	}

	public void OnPhaseEntered( LeaguePhase phase, LeagueState state )
	{
		if ( phase != LeaguePhase.Draft )
			return;

		if ( !state.Draft.IsActive )
			BeginDraft( state );
		else
			SyncPickClock( state );

		NotifyHumanOnClock( state );
	}

	public void OnWeekAdvanced( LeagueState state ) { }

	public void OnSeasonEnded( LeagueState state )
	{
		PrepareDraftClass( state, state.CurrentSeason + 1 );
	}

	public void BeginDraft( LeagueState state )
	{
		var season = state.CurrentSeason;

		if ( state.Settings.StartMode == DynastyStartMode.ExpansionDraft && state.Draft.Prospects.Count == 0 )
			PrepareExpansionPool( state );
		else if ( state.Draft.Season != season || state.Draft.Prospects.Count == 0 )
			PrepareDraftClass( state, season );

		state.Draft.IsActive = true;
		state.Draft.CurrentPickIndex = state.Draft.Order.Count( o => o.IsComplete );
		UpdateCurrentRound( state );
		if ( state.Draft.Order.Count == 0 )
			BuildDraftOrder( state );

		var draftHint = state.Draft.Type == DraftType.Expansion
			? $"Build your roster over {state.Settings.ExpansionDraftRounds} rounds. Use Sim to My Pick, Sim Rest of Draft, or draft manually."
			: state.Settings.IsFtueExperience
				? "New here? Hit Sim Rest of Draft to jump into the season. Want control? Draft manually when you're on the clock."
				: "Review the draft board. Use Sim to My Pick or draft manually when you are on the clock.";
		_inbox?.Add( state, InboxCategory.Draft, InboxPriority.High,
			state.Draft.Type == DraftType.Expansion ? "Expansion draft has begun" : "Rookie draft has begun",
			draftHint,
			false, navigateTab: "draft" );

		SyncPickClock( state );
		state.BumpRevision( "draft_begin" );
	}

	public bool TryProcessTimedPick( LeagueState state )
	{
		if ( !state.Draft.IsActive || state.Phase != LeaguePhase.Draft )
			return false;

		SyncPickClock( state );

		if ( state.Draft.OnClockSinceUtc == default )
			return false;

		var limit = state.Settings.DraftPickTimerSeconds;
		var elapsed = ( GetClockUtcNow() - state.Draft.OnClockSinceUtc ).TotalSeconds;
		if ( elapsed < limit )
			return false;

		return TryAutoPickCurrent( state );
	}

	public bool TryMakePick( LeagueState state, TeamId teamId, PlayerId prospectId )
	{
		if ( !state.Draft.IsActive )
			return false;

		var current = GetCurrentPick( state );
		if ( current == null || current.IsComplete || current.TeamId.Value != teamId.Value )
			return false;

		var prospect = state.Draft.Prospects.FirstOrDefault( p => p.Id.Value == prospectId.Value && !p.IsDrafted );
		if ( prospect == null )
			return false;

		ExecutePick( state, current, teamId, prospect );
		return true;
	}

	public bool TrySimulateOnePick( LeagueState state )
	{
		if ( !state.Draft.IsActive )
			return false;

		var current = GetCurrentPick( state );
		if ( current == null || current.IsComplete )
			return false;

		if ( GmAssignmentHelper.IsHumanTeam( state, current.TeamId ) )
			return false;

		var prospect = SelectAiProspect( state, current.TeamId );
		if ( prospect == null )
		{
			state.Draft.IsActive = false;
			return false;
		}

		ExecutePick( state, current, current.TeamId, prospect );
		NotifyHumanOnClock( state );
		return true;
	}

	public int SimulateToHumanPick( LeagueState state )
	{
		var count = 0;
		while ( state.Draft.IsActive && TrySimulateOnePick( state ) )
			count++;

		NotifyHumanOnClock( state );
		return count;
	}

	public int SimulateRestOfDraft( LeagueState state )
	{
		var count = 0;
		while ( state.Draft.IsActive && TryAutoPickCurrent( state ) )
			count++;

		return count;
	}

	/// <summary>Sim AI picks until human is on clock, or a league-wide steal/reach moment pauses the draft.</summary>
	public int SimulateSmartDraft( LeagueState state )
	{
		var count = 0;
		var lastRound = state.Draft.CurrentRound;

		while ( state.Draft.IsActive )
		{
			var current = GetCurrentPick( state );
			if ( current == null || current.IsComplete )
				break;

			if ( GmAssignmentHelper.IsHumanTeam( state, current.TeamId ) )
				break;

			if ( !TrySimulateOnePick( state ) )
				break;

			count++;

			var last = state.Draft.History.LastOrDefault();
			if ( last != null && IsNotablePick( state, last ) )
				break;

			if ( state.Draft.CurrentRound != lastRound )
			{
				lastRound = state.Draft.CurrentRound;
				break;
			}
		}

		NotifyHumanOnClock( state );
		return count;
	}

	public static bool IsNotablePick( LeagueState state, DraftHistoryEntry entry )
	{
		var prospect = state.Draft.Prospects.FirstOrDefault( p => p.Id.Value == entry.PlayerId.Value );
		if ( prospect == null || prospect.ConsensusRank <= 0 )
			return false;

		var delta = entry.OverallPick - prospect.ConsensusRank;
		return delta >= 12 || delta <= -12;
	}

	public void ProcessAiPicksUntilHuman( LeagueState state ) => SimulateToHumanPick( state );

	public bool TryAutoPickCurrent( LeagueState state )
	{
		if ( !state.Draft.IsActive )
			return false;

		var current = GetCurrentPick( state );
		if ( current == null || current.IsComplete )
			return false;

		var prospect = SelectAiProspect( state, current.TeamId );
		if ( prospect == null )
		{
			state.Draft.IsActive = false;
			SyncPickClock( state );
			return false;
		}

		ExecutePick( state, current, current.TeamId, prospect );
		NotifyHumanOnClock( state );
		return true;
	}

	public void EnsurePickClock( LeagueState state ) => SyncPickClock( state );

	void SyncPickClock( LeagueState state )
	{
		var current = GetCurrentPick( state );
		if ( !state.Draft.IsActive || current == null || current.IsComplete )
		{
			state.Draft.OnClockOverallPick = 0;
			return;
		}

		if ( state.Draft.OnClockOverallPick == current.OverallPick && state.Draft.OnClockSinceUtc != default )
			return;

		state.Draft.OnClockOverallPick = current.OverallPick;
		state.Draft.OnClockSinceUtc = GetClockUtcNow();
	}

	DateTime GetClockUtcNow() => _context?.Clock.UtcNow ?? DateTime.UtcNow;

	void ExecutePick( LeagueState state, DraftOrderEntry current, TeamId teamId, ProspectState prospect )
	{
		prospect.IsDrafted = true;
		prospect.DraftedByTeamId = teamId;
		prospect.Player.TeamId = teamId;
		prospect.Player.Contract.YearsRemaining = state.Draft.Type == DraftType.Expansion ? 3 : 4;
		prospect.Player.Contract.SignedWithTeamId = teamId;
		prospect.Player.Contract.AnnualSalary = SalaryCapHelper.RookieContractSalary( current.OverallPick );
		prospect.Player.Contract.GuaranteedMoney = prospect.Player.Contract.AnnualSalary * prospect.Player.Contract.YearsRemaining;
		prospect.Player.RookieSeason = state.Draft.Type == DraftType.Rookie ? state.CurrentSeason : 0;

		if ( !state.Players.ContainsKey( prospect.Id ) )
			state.Players[prospect.Id] = prospect.Player;

		if ( !state.Teams[teamId].RosterPlayerIds.Contains( prospect.Id ) )
			state.Teams[teamId].RosterPlayerIds.Add( prospect.Id );

		ApplyScoutingRevealForPick( prospect.Player, teamId, state );

		state.Draft.History.Add( new DraftHistoryEntry
		{
			Season = state.Draft.Season,
			Round = current.Round,
			OverallPick = current.OverallPick,
			TeamId = teamId,
			PlayerId = prospect.Id
		} );

		current.IsComplete = true;
		state.Draft.CurrentPickIndex++;
		UpdateCurrentRound( state );

		if ( state.Draft.CurrentPickIndex >= state.Draft.Order.Count )
			state.Draft.IsActive = false;

		var teamAbbr = state.Teams[teamId].Identity.Abbreviation;
		_news?.Publish( state, NewsCategory.Draft,
			$"Pick {current.OverallPick}: {teamAbbr} selects {prospect.Player.Identity.FullName}",
			$"{prospect.Player.Identity.Position} · OVR {prospect.Player.Ratings.Overall} from {prospect.Player.Identity.College}" );

		_context.Events.Publish( new DraftPickMadeEvent(
			_context.Events.NextSequence(),
			_context.Clock.UtcNow,
			state.Draft.Season,
			current.Round,
			current.OverallPick,
			teamId,
			prospect.Id ) );

		_retention?.OnDraftPick( state, current.OverallPick, teamId, prospect.Id );
		DraftPickRegistry.ConsumePick( state, current.PickAssetId );
		SalaryCapHelper.RecalculateCapSpace( state, state.Teams[teamId] );

		SyncPickClock( state );
		state.BumpRevision( "draft_pick" );
	}

	ProspectState SelectAiProspect( LeagueState state, TeamId teamId )
	{
		var available = state.Draft.Prospects.Where( p => !p.IsDrafted ).ToList();
		if ( available.Count == 0 )
			return null;

		var uncovered = GetUncoveredPositions( state, teamId );
		if ( uncovered.Count > 0 )
		{
			var positional = available
				.Where( p => uncovered.Contains( p.Player.Identity.Position ) )
				.OrderByDescending( p => p.Player.Ratings.Overall )
				.ThenBy( p => p.ConsensusRank )
				.FirstOrDefault();

			if ( positional != null )
				return positional;
		}

		var needs = GetPositionNeeds( state, teamId );
		var weighted = available
			.Select( p => new
			{
				Prospect = p,
				Score = ( 1000 - p.ConsensusRank )
					+ ( needs.GetValueOrDefault( p.Player.Identity.Position, 0 ) * 25 )
					+ _context.Random.NextInt( 0, 15 )
			} )
			.OrderByDescending( x => x.Score )
			.First();

		return weighted.Prospect;
	}

	static HashSet<Position> GetUncoveredPositions( LeagueState state, TeamId teamId )
	{
		var uncovered = new HashSet<Position>( Enum.GetValues<Position>() );
		if ( !state.Teams.TryGetValue( teamId, out var team ) )
			return uncovered;

		foreach ( var playerId in team.RosterPlayerIds )
		{
			if ( !state.Players.TryGetValue( playerId, out var player ) )
				continue;

			uncovered.Remove( player.Identity.Position );
		}

		return uncovered;
	}

	static Dictionary<Position, int> GetPositionNeeds( LeagueState state, TeamId teamId )
	{
		var needs = new Dictionary<Position, int>();
		if ( !state.Teams.TryGetValue( teamId, out var team ) )
			return needs;

		var rosterCounts = new Dictionary<Position, int>();
		foreach ( var pid in team.RosterPlayerIds )
		{
			if ( !state.Players.TryGetValue( pid, out var player ) )
				continue;

			rosterCounts[player.Identity.Position] = rosterCounts.GetValueOrDefault( player.Identity.Position, 0 ) + 1;
		}

		foreach ( var pos in Enum.GetValues<Position>() )
		{
			var minRequired = GetMinimumRequiredAtPosition( pos );
			var count = rosterCounts.GetValueOrDefault( pos, 0 );
			if ( count < minRequired )
				needs[pos] = minRequired - count;
		}

		AddFormationSlotNeeds( team, needs );
		return needs;
	}

	static int GetMinimumRequiredAtPosition( Position position )
		=> position switch
		{
			Position.QB => 1,
			Position.K => 1,
			Position.P => 1,
			Position.LS => 1,
			Position.OT => 2,
			Position.OG => 2,
			Position.WR => 3,
			Position.DE => 2,
			Position.DT => 2,
			Position.LB => 3,
			Position.CB => 2,
			Position.S => 2,
			_ => 1
		};

	static void AddFormationSlotNeeds( TeamState team, Dictionary<Position, int> needs )
	{
		var offense = FormationLayoutRegistry.Get( team.ActiveOffenseFormation );
		var defense = FormationLayoutRegistry.Get( team.ActiveDefenseFormation );

		foreach ( var slot in offense.Slots.Concat( defense.Slots ) )
		{
			if ( !DepthChartData.GetStarter( team.DepthChart, slot.SlotKey ).IsEmpty )
				continue;

			foreach ( var pos in slot.EligiblePositions )
				needs[pos] = needs.GetValueOrDefault( pos, 0 ) + 3;
		}
	}

	void PrepareDraftClass( LeagueState state, int season )
	{
		state.Draft = new DraftState { Season = season, Type = DraftType.Rookie };
		var rookies = LeaguePlayerGenerator.GenerateRookieClass(
			_context.Definitions,
			_context.Random,
			state.Settings.RookieProspectCount,
			state.Settings.MinPlayersPerPositionPerTeam );

		foreach ( var player in rookies )
		{
			ApplyProspectScouting( player );
			state.Draft.Prospects.Add( new ProspectState
			{
				Id = player.Id,
				Player = player,
				ConsensusRank = state.Draft.Prospects.Count + 1
			} );
		}

		ShuffleAndRankProspects( state );
	}

	void PrepareExpansionPool( LeagueState state )
	{
		state.Draft = new DraftState { Season = state.CurrentSeason, Type = DraftType.Expansion };

		var poolPlayers = state.Players.Values
			.Where( p => !p.IsRetired && p.TeamId.IsEmpty )
			.ToList();

		if ( poolPlayers.Count == 0 )
		{
			LeaguePlayerGenerator.GenerateLeaguePool(
				state,
				_context.Definitions,
				_context.Random,
				state.Settings.LeaguePlayerPoolSize,
				state.Settings.MinPlayersPerPositionPerTeam );

			poolPlayers = state.Players.Values
				.Where( p => !p.IsRetired && p.TeamId.IsEmpty )
				.ToList();
		}

		foreach ( var player in poolPlayers )
		{
			ApplyProspectScouting( player );
			state.Draft.Prospects.Add( new ProspectState
			{
				Id = player.Id,
				Player = player,
				ConsensusRank = state.Draft.Prospects.Count + 1
			} );
		}

		ShuffleAndRankProspects( state );
	}

	void ApplyProspectScouting( PlayerState player )
	{
		var random = _context.Random;
		var scouting = player.Scouting;
		scouting.ScoutConfidence = random.NextInt( 35, 75 );
		scouting.OverallRevealed = random.Chance( 0.65f );
		scouting.PotentialRevealed = random.Chance( 0.45f );

		if ( player.Ratings.Attributes.Count > 0 )
		{
			var revealCount = random.NextInt( 1, Math.Min( 4, player.Ratings.Attributes.Count ) + 1 );
			foreach ( var key in player.Ratings.Attributes.Keys.OrderBy( _ => random.NextInt( 0, int.MaxValue ) ).Take( revealCount ) )
				scouting.RevealedAttributes.Add( key );
		}

		foreach ( var trait in player.Traits )
		{
			if ( random.Chance( 0.6f ) )
				scouting.RevealedTraits.Add( trait );
		}
	}

	void ApplyScoutingRevealForPick( PlayerState player, TeamId teamId, LeagueState state )
	{
		player.Scouting.OverallRevealed = true;
		player.Scouting.PotentialRevealed = true;
		foreach ( var key in player.Ratings.Attributes.Keys )
			player.Scouting.RevealedAttributes.Add( key );
		foreach ( var trait in player.Traits )
			player.Scouting.RevealedTraits.Add( trait );
	}

	void ShuffleAndRankProspects( LeagueState state )
	{
		state.Draft.Prospects = state.Draft.Prospects.OrderBy( _ => _context.Random.NextInt( 0, int.MaxValue ) ).ToList();
		for ( var i = 0; i < state.Draft.Prospects.Count; i++ )
			state.Draft.Prospects[i].ConsensusRank = i + 1;
	}

	void BuildDraftOrder( LeagueState state )
	{
		state.Draft.Order.Clear();
		var orderedTeams = GetPickOrderTeams( state );
		var rounds = GetDraftRoundCount( state );
		var season = state.Draft.Season;
		var pick = 1;

		for ( var round = 1; round <= rounds; round++ )
		{
			var roundTeams = round % 2 == 0 ? orderedTeams.AsEnumerable().Reverse() : orderedTeams;
			foreach ( var teamId in roundTeams )
			{
				var pickAssetId = ResolvePickAssetId( state, teamId, season, round );

				state.Draft.Order.Add( new DraftOrderEntry
				{
					OverallPick = pick,
					Round = round,
					PickInRound = ((pick - 1) % state.Settings.TeamCount) + 1,
					TeamId = teamId,
					PickAssetId = pickAssetId
				} );
				pick++;
			}
		}
	}

	static DraftPickId ResolvePickAssetId( LeagueState state, TeamId teamId, int season, int round )
	{
		if ( state.Teams.TryGetValue( teamId, out var team ) )
		{
			var existing = team.DraftPicks.FirstOrDefault( p =>
				p.Season == season && p.Round == round && p.CurrentOwnerId.Value == teamId.Value );

			if ( existing != null )
				return existing.Id;
		}

		return DraftPickId.New();
	}

	List<TeamId> GetPickOrderTeams( LeagueState state )
		=> DraftOrderHelper.GetPickOrder( state, _context.Random );

	void UpdateCurrentRound( LeagueState state )
	{
		var current = GetCurrentPick( state );
		state.Draft.CurrentRound = current?.Round ?? GetDraftRoundCount( state );
	}

	DraftOrderEntry GetCurrentPick( LeagueState state )
		=> state.Draft.Order.ElementAtOrDefault( state.Draft.CurrentPickIndex );

	static int GetDraftRoundCount( LeagueState state )
		=> state.Draft.Type == DraftType.Expansion
			? state.Settings.ExpansionDraftRounds
			: state.Settings.RookieDraftRounds;

	public void NotifyHumanOnClockPublic( LeagueState state ) => NotifyHumanOnClock( state );

	void NotifyHumanOnClock( LeagueState state )
	{
		if ( !state.Draft.IsActive )
			return;

		var current = GetCurrentPick( state );
		if ( current != null && GmAssignmentHelper.IsHumanTeam( state, current.TeamId ) )
			_inbox?.AddDraftInboxIfNeeded( state );
	}

	public static void RevealHiddenTraitsForRookies( LeagueState state )
	{
		if ( state.Phase != LeaguePhase.RegularSeason )
			return;

		var revealWeek = Math.Max( 1, state.Settings.RegularSeasonWeeks / 2 );
		if ( state.CurrentWeek < revealWeek )
			return;

		foreach ( var player in state.Players.Values.Where( p => !p.IsRetired && !p.HiddenTraitsRevealed ) )
		{
			if ( player.RookieSeason != state.CurrentSeason )
				continue;

			if ( player.HiddenTraits.Count == 0 )
			{
				player.HiddenTraitsRevealed = true;
				continue;
			}

			player.HiddenTraitsRevealed = true;
			foreach ( var trait in player.HiddenTraits )
			{
				if ( !player.Traits.Contains( trait ) )
					player.Traits.Add( trait );
			}

			player.HiddenTraits.Clear();
		}

		state.BumpRevision( "hidden_traits_revealed" );
	}
}
