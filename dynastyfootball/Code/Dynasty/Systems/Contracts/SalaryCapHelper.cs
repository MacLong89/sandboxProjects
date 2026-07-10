using Dynasty.Domain.League;
using Dynasty.Domain.Players;
using Dynasty.Domain.Teams;

namespace Dynasty.Systems.Contracts;

/// <summary>
/// Single source of truth for salary cap math. All roster/contract mutations should call <see cref="RecalculateCapSpace"/>.
/// </summary>
public static class SalaryCapHelper
{
	public static long GetTotalCapHit( LeagueState state, TeamState team )
	{
		var payroll = team.RosterPlayerIds
			.Select( id => state.Players.GetValueOrDefault( id ) )
			.Where( p => p != null && !p.IsRetired )
			.Sum( p => (long)p.Contract.AnnualSalary );

		return payroll + team.Finances.DeadCap;
	}

	public static void RecalculateCapSpace( LeagueState state, TeamState team )
	{
		var cap = state.Settings.SalaryCap;
		team.Finances.SalaryCapSpace = cap - GetTotalCapHit( state, team );
	}

	public static void RecalculateAllTeams( LeagueState state )
	{
		foreach ( var team in state.Teams.Values )
			RecalculateCapSpace( state, team );
	}

	public static bool CanAffordAnnual( TeamState team, long annualSalary )
		=> team.Finances.SalaryCapSpace >= annualSalary;

	public static int RookieContractSalary( int overallPick )
		=> Math.Clamp( 12_000_000 - overallPick * 45_000, 750_000, 8_500_000 );
}
