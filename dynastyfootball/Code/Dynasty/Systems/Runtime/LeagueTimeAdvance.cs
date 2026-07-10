using Dynasty.Core;
using Dynasty.Core.Enums;
using Dynasty.Domain.League;
using Dynasty.Systems.Draft;
using Dynasty.Systems.Franchise;

namespace Dynasty.Systems.Runtime;

public sealed class LeagueTimeAdvance
{
	public const int MaxSteps = 600;

	public bool TryAdvanceToTarget(
		LeagueState state,
		TimeAdvanceTarget target,
		LeagueRuntime runtime,
		FranchiseWorkflowSystem workflow,
		DraftSystem draft,
		out string error )
	{
		error = "";

		if ( state == null )
		{
			error = "No league loaded.";
			return false;
		}

		if ( HasReachedTarget( state, target ) )
			return true;

		if ( target == TimeAdvanceTarget.OneWeek )
			return TryAdvanceOneStep( state, runtime, workflow, draft, fullDraftSim: false, out error );

		var steps = 0;
		while ( !HasReachedTarget( state, target ) && steps++ < MaxSteps )
		{
			var fullDraftSim = ShouldSimulateFullDraft( state, target );
			if ( !TryAdvanceOneStep( state, runtime, workflow, draft, fullDraftSim, out error ) )
				return false;
		}

		if ( !HasReachedTarget( state, target ) )
		{
			error = "Could not reach the selected milestone.";
			return false;
		}

		return true;
	}

	static bool TryAdvanceOneStep(
		LeagueState state,
		LeagueRuntime runtime,
		FranchiseWorkflowSystem workflow,
		DraftSystem draft,
		bool fullDraftSim,
		out string error )
	{
		error = "";
		ClearBlocking( state, workflow, draft, fullDraftSim );

		if ( FranchiseWorkflowSystem.HasBlockingItems( state ) )
		{
			if ( !runtime.AdvanceToNextEvent( state ) )
			{
				error = FranchiseWorkflowSystem.GetBlockingReason( state );
				return false;
			}

			return true;
		}

		if ( !runtime.AdvanceWeek( state ) )
		{
			error = FranchiseWorkflowSystem.GetBlockingReason( state );
			return false;
		}

		return true;
	}

	static void ClearBlocking(
		LeagueState state,
		FranchiseWorkflowSystem workflow,
		DraftSystem draft,
		bool fullDraftSim )
	{
		while ( state.EventQueue.Any( e => !e.IsComplete && e.IsBlocking ) )
			workflow.CompleteNextEvent( state );

		InboxSystem.PurgeStaleMessages( state );

		foreach ( var msg in state.Inbox.Where( m => !m.IsResolved && m.RequiresAction ).ToList() )
			InboxSystem.Resolve( state, msg.Id );

		if ( state.Phase != LeaguePhase.Draft || !state.Draft.IsActive )
			return;

		if ( fullDraftSim )
			draft.SimulateRestOfDraft( state );
		else if ( FranchiseWorkflowHelper.IsHumanOnDraftClock( state ) )
			draft.TryAutoPickCurrent( state );
	}

	static bool ShouldSimulateFullDraft( LeagueState state, TimeAdvanceTarget target )
	{
		if ( target == TimeAdvanceTarget.NextDraft )
			return false;

		return state.Phase == LeaguePhase.Draft && state.Draft.IsActive;
	}

	public static bool HasReachedTarget( LeagueState state, TimeAdvanceTarget target ) => target switch
	{
		TimeAdvanceTarget.OneWeek => false,
		TimeAdvanceTarget.MidSeason => HasReachedMidSeason( state ),
		TimeAdvanceTarget.EndRegularSeason => HasReachedEndRegularSeason( state ),
		TimeAdvanceTarget.SuperBowl => HasReachedSuperBowl( state ),
		TimeAdvanceTarget.NextDraft => state.Phase == LeaguePhase.Draft,
		_ => false
	};

	static bool HasReachedMidSeason( LeagueState state )
	{
		if ( state.Phase == LeaguePhase.RegularSeason )
			return state.CurrentWeek >= MidSeasonWeek( state );

		return IsPastRegularSeason( state );
	}

	static bool HasReachedEndRegularSeason( LeagueState state )
	{
		if ( state.Phase == LeaguePhase.RegularSeason )
			return state.CurrentWeek >= state.Settings.RegularSeasonWeeks;

		return IsPastRegularSeason( state );
	}

	static bool HasReachedSuperBowl( LeagueState state )
	{
		if ( state.Schedule.Games.Any( g =>
			g.Season == state.CurrentSeason
			&& g.IsPlayoffGame
			&& g.PlayoffRound == PlayoffRound.SuperBowl
			&& g.IsComplete ) )
			return true;

		return state.Phase is LeaguePhase.Offseason or LeaguePhase.FreeAgency or LeaguePhase.Draft;
	}

	static bool IsPastRegularSeason( LeagueState state )
		=> state.Phase is LeaguePhase.Playoffs or LeaguePhase.Offseason or LeaguePhase.FreeAgency or LeaguePhase.Draft;

	static int MidSeasonWeek( LeagueState state )
		=> Math.Max( 1, state.Settings.RegularSeasonWeeks / 2 );
}
