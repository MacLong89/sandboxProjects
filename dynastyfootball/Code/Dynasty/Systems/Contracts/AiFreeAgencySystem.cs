using Dynasty.Core;
using Dynasty.Core.Enums;
using Dynasty.Core.Interfaces;
using Dynasty.Core.Identifiers;
using Dynasty.Domain.Contracts;
using Dynasty.Domain.League;
using Dynasty.Domain.Players;

namespace Dynasty.Systems.Contracts;

/// <summary>
/// AI teams bid on free agents during the FA window, creating league competition for signings.
/// </summary>
public sealed class AiFreeAgencySystem : ILeagueSystem
{
	public string SystemId => "ai_free_agency";

	private LeagueSystemContext _context;
	private ContractSystem _contracts;

	public void Register( LeagueSystemContext context ) => _context = context;

	public void SetContractSystem( ContractSystem contracts ) => _contracts = contracts;

	public void OnLeagueCreated( LeagueState state ) { }

	public void OnPhaseEntered( LeaguePhase phase, LeagueState state )
	{
		if ( phase == LeaguePhase.FreeAgency )
			RunAiSigningPeriod( state );
	}

	public void OnWeekAdvanced( LeagueState state )
	{
		if ( state.Phase == LeaguePhase.FreeAgency && state.FreeAgency.IsOpen )
			RunAiSigningPeriod( state );
	}

	public void OnSeasonEnded( LeagueState state ) { }

	void RunAiSigningPeriod( LeagueState state )
	{
		if ( _contracts == null || state.FreeAgency.AvailablePlayers.Count == 0 )
			return;

		var humanPending = state.FreeAgency.PendingOffers
			.Where( o => !o.Accepted )
			.Select( o => o.PlayerId )
			.ToHashSet();

		var aiTeams = state.Teams.Values
			.Where( t => !GmAssignmentHelper.IsHumanTeam( state, t.Id ) )
			.OrderBy( _ => _context.Random.NextInt( 0, int.MaxValue ) )
			.ToList();

		var signings = 0;
		foreach ( var team in aiTeams )
		{
			if ( signings >= 24 )
				break;

			if ( team.Finances.SalaryCapSpace < 1_500_000 )
				continue;

			var target = PickTarget( state, team, humanPending );
			if ( target == null )
				continue;

			var offer = BuildOffer( state, team.Id, target );
			if ( _contracts.TryAiSign( state, offer ) )
			{
				signings++;
				humanPending.Remove( target.Id );
			}
		}

		if ( signings > 0 )
			state.BumpRevision( "ai_free_agency" );
	}

	PlayerState PickTarget( LeagueState state, Domain.Teams.TeamState team, HashSet<PlayerId> reserved )
	{
		var needs = GetPositionNeeds( state, team );
		var candidates = state.FreeAgency.AvailablePlayers
			.Where( id => !reserved.Contains( id ) )
			.Select( id => state.Players.GetValueOrDefault( id ) )
			.Where( p => p != null && !p.IsRetired )
			.OrderByDescending( p => ScoreFreeAgent( state, team, p, needs ) )
			.ThenByDescending( p => p.Ratings.Overall )
			.Take( 12 )
			.ToList();

		if ( candidates.Count == 0 )
			return null;

		return _context.Random.Pick( candidates.Take( Math.Min( 5, candidates.Count ) ).ToList() );
	}

	static float ScoreFreeAgent( LeagueState state, Domain.Teams.TeamState team, PlayerState player, Dictionary<Position, float> needs )
	{
		var need = needs.GetValueOrDefault( player.Identity.Position, 0.5f );
		var prestigeFit = team.Prestige.Prestige / 100f;
		var ovrScore = player.Ratings.Overall / 99f;
		return need * 0.5f + ovrScore * 0.35f + prestigeFit * 0.15f;
	}

	static Dictionary<Position, float> GetPositionNeeds( LeagueState state, Domain.Teams.TeamState team )
	{
		var roster = team.RosterPlayerIds
			.Select( id => state.Players.GetValueOrDefault( id ) )
			.Where( p => p != null && !p.IsRetired )
			.ToList();

		var needs = new Dictionary<Position, float>();
		foreach ( var group in roster.GroupBy( p => p.Identity.Position ) )
		{
			var avg = group.Average( p => (float)p.Ratings.Overall );
			needs[group.Key] = Math.Clamp( 1f - avg / 90f, 0.1f, 1f );
		}

		foreach ( var pos in Enum.GetValues<Position>() )
			needs.TryAdd( pos, 0.85f );

		return needs;
	}

	ContractOffer BuildOffer( LeagueState state, TeamId teamId, PlayerState player )
	{
		var team = state.Teams[teamId];
		var baseSalary = Math.Max( 750_000, (int)( player.Ratings.Overall * 180_000L ) );
		var prestigeBonus = team.Prestige.Prestige / 100f;
		var annual = (int)( baseSalary * ( 0.85f + prestigeBonus * 0.25f + _context.Random.NextFloat() * 0.15f ) );
		annual = Math.Min( annual, (int)Math.Max( 750_000, team.Finances.SalaryCapSpace ) );

		var years = player.Identity.Age >= 32 ? 2 : player.Identity.Age >= 28 ? 3 : 4;

		return new ContractOffer
		{
			OfferId = Guid.NewGuid(),
			TeamId = teamId,
			PlayerId = player.Id,
			Years = years,
			AnnualSalary = annual,
			GuaranteedMoney = (int)( annual * years * 0.35f )
		};
	}
}
