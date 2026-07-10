using Dynasty.Core.Enums;
using Dynasty.Core.Identifiers;
using Dynasty.Domain.Franchise;
using Dynasty.Domain.League;

namespace Dynasty.Core;

/// <summary>
/// First-time user experience rules: tab unlocks, preseason compression, replay prompts.
/// </summary>
public static class FtueHelper
{
	public static bool IsFtueActive( LeagueState state )
		=> state?.FranchiseProgress?.IsFtueActive == true && state.Settings.IsFtueExperience;

	public static int GetEffectivePreseasonWeeks( LeagueState state )
	{
		if ( state == null )
			return 4;

		if ( !IsFtueActive( state ) || state.CurrentSeason > 1 )
			return state.Settings.PreseasonWeeks;

		return Math.Max( 1, state.Settings.FtuePreseasonWeeks );
	}

	public static bool IsTabUnlocked( LeagueState state, TeamId humanTeamId, string tab )
	{
		if ( state == null || !IsFtueActive( state ) )
			return true;

		var progress = state.FranchiseProgress;
		var phase = state.Phase;

		return tab switch
		{
			"home" or "inbox" or "team" or "schedule" => true,
			"draft" => phase == LeaguePhase.Draft,
			"news" => true,
			"legacy" => progress?.HasWonFirstGame == true || phase != LeaguePhase.Preseason && phase != LeaguePhase.Draft,
			"facilities" => progress?.HasWonFirstGame == true,
			"trades" => state.CurrentSeason > 1
				|| ( phase is LeaguePhase.RegularSeason or LeaguePhase.Playoffs && state.CurrentWeek >= 4 ),
			"freeagency" => phase is LeaguePhase.FreeAgency or LeaguePhase.Offseason,
			_ => true
		};
	}

	public static bool ShouldAutoOpenReplay( LeagueState state )
	{
		if ( state?.FranchiseProgress == null )
			return false;

		return IsFtueActive( state ) && state.FranchiseProgress.HumanGamesSimulated < 3;
	}

	public static bool ShouldPromptFourthDownDecision( LeagueState state )
	{
		if ( state?.FranchiseProgress == null )
			return false;

		return IsFtueActive( state ) && state.FranchiseProgress.HumanGamesSimulated < 3;
	}

	public static bool ShouldShowDraftCeremony( LeagueState state, TeamId pickingTeamId )
	{
		if ( !GmAssignmentHelper.IsHumanTeam( state, pickingTeamId ) )
			return false;

		if ( !IsFtueActive( state ) )
			return true;

		var progress = state.FranchiseProgress;
		return progress.HumanDraftCeremoniesShown < 3;
	}

	public static void OnHumanGameSimulated( LeagueState state, bool won )
	{
		var progress = state.FranchiseProgress ??= new FranchiseProgressState();
		progress.HumanGamesSimulated++;

		if ( won )
			progress.HasWonFirstGame = true;

		if ( progress.HumanGamesSimulated >= 3
			&& state.Phase is LeaguePhase.RegularSeason or LeaguePhase.Playoffs
			&& state.CurrentWeek >= 2 )
		{
			progress.IsFtueActive = false;
		}
	}

	public static void OnHumanDraftCeremonyShown( LeagueState state )
	{
		var progress = state.FranchiseProgress ??= new FranchiseProgressState();
		progress.HumanDraftCeremoniesShown++;
	}

	public static FranchiseProgressState EnsureProgress( LeagueState state )
	{
		state.FranchiseProgress ??= new FranchiseProgressState();
		state.FranchiseProgress.MilestonesReached ??= new HashSet<string>();
		state.FranchiseProgress.NearMissAlertsSent ??= new HashSet<string>();
		return state.FranchiseProgress;
	}
}
