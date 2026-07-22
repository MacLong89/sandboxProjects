namespace NoFly;

public enum TutorialTipPhase
{
	Any,
	Preparation,
	SecurityOpen
}

public sealed class TutorialTipDef
{
	public string Id { get; init; }
	public string Title { get; init; }
	public string Body { get; init; }
	public string Icon { get; init; } = "tips_and_updates";
	public int Priority { get; init; }
	public RoleType Role { get; init; }
	public TutorialTipPhase Phase { get; init; } = TutorialTipPhase.Any;
}

/// <summary>Role-specific coach tips — Final Outpost style.</summary>
public static class TutorialTips
{
	public static readonly IReadOnlyList<TutorialTipDef> All = BuildAll();

	public static bool ShouldRun() => !NoFlyClientPrefs.HideTutorialTips;

	public static TutorialTipDef PickNext( NoFlyPlayer local, RoundState state, bool uiBlocking )
	{
		if ( !ShouldRun() || local is null || uiBlocking )
			return null;

		if ( state is not (RoundState.Preparation or RoundState.AirportOpen or RoundState.Boarding or RoundState.Chase) )
			return null;

		var shown = NoFlyClientPrefs.TutorialTipsShown;
		var phase = state == RoundState.Preparation
			? TutorialTipPhase.Preparation
			: TutorialTipPhase.SecurityOpen;

		foreach ( var tip in All.OrderByDescending( t => t.Priority ) )
		{
			if ( shown.Contains( tip.Id ) )
				continue;
			if ( tip.Role != local.Role )
				continue;
			if ( tip.Phase != TutorialTipPhase.Any && tip.Phase != phase )
				continue;

			return tip;
		}

		return null;
	}

	public static void MarkShown( string id ) => NoFlyClientPrefs.MarkTipShown( id );

	static IReadOnlyList<TutorialTipDef> BuildAll()
	{
		var tips = new List<TutorialTipDef>();

		void Add( RoleType role, TutorialTipPhase phase, int priority, string id, string title, string body, string icon = "tips_and_updates" )
		{
			tips.Add( new TutorialTipDef
			{
				Id = id,
				Role = role,
				Phase = phase,
				Priority = priority,
				Title = title,
				Body = body,
				Icon = icon
			} );
		}

		Add( RoleType.Smuggler, TutorialTipPhase.Preparation, 100, "smuggler_prep", "Prep time",
			"You have 30 seconds before security opens. Forge one document field, then hide your contraband.", "timer" );
		Add( RoleType.Smuggler, TutorialTipPhase.SecurityOpen, 90, "smuggler_play", "Stay hidden",
			"Queue through document check and bag scan without getting caught, then board your flight.", "flight" );

		Add( RoleType.DocumentAgent, TutorialTipPhase.Preparation, 100, "doc_prep", "Man your desk",
			"Walk to the DOCUMENTS desk and press E to man your station. When security opens, inspection popups appear.", "badge" );
		Add( RoleType.DocumentAgent, TutorialTipPhase.SecurityOpen, 90, "doc_play", "Inspect passengers",
			"A passenger is at your desk when the inspection panel opens. Compare docs, then Approve or Reject.", "fact_check" );

		Add( RoleType.ScannerAgent, TutorialTipPhase.Preparation, 100, "scan_prep", "Man bag scan",
			"Walk to the BAG SCAN desk and press E to man your station. When security opens, scan popups appear.", "luggage" );
		Add( RoleType.ScannerAgent, TutorialTipPhase.SecurityOpen, 90, "scan_play", "Scan bags",
			"Compare Declared vs X-Ray carefully. Search a suspicious item — or Clear if it looks clean. Call Security to detain.", "radar" );

		Add( RoleType.SecurityOfficer, TutorialTipPhase.Preparation, 100, "sec_prep", "Man security",
			"Walk to the SECURITY desk and press E to man your station. Then respond to tablet alerts.", "shield" );
		Add( RoleType.SecurityOfficer, TutorialTipPhase.SecurityOpen, 90, "sec_play", "Stop the smuggler",
			"Watch the tablet. Respond to flags and stop the Smuggler.", "gavel" );

		Add( RoleType.UndercoverAgent, TutorialTipPhase.Preparation, 100, "uc_prep", "Blend in",
			"Wait in the lobby until security opens, then blend in and hunt using your clues.", "visibility" );
		Add( RoleType.UndercoverAgent, TutorialTipPhase.SecurityOpen, 90, "uc_play", "Hunt carefully",
			"Use your clues to find the suspect. One wrong arrest exposes you.", "search" );

		Add( RoleType.RegularPassenger, TutorialTipPhase.Preparation, 100, "pass_prep", "Waiting area",
			"Wait in the waiting area. When security opens, queue for document check.", "groups" );
		Add( RoleType.RegularPassenger, TutorialTipPhase.SecurityOpen, 90, "pass_play", "Clear security",
			"Join the document queue, then bag scan, then head to your gate.", "directions_walk" );

		// No same-phase "done" tips — prep → play is already gated by round phase.

		return tips;
	}
}
