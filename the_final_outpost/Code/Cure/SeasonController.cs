namespace FinalOutpost;

/// <summary>Season/year pacing and auto threat waves for Road to a Cure.</summary>
public sealed class SeasonController : Component
{
	public static SeasonController Instance { get; private set; }

	public bool SeasonRecapPending { get; private set; }
	public string LastSeasonSummary { get; private set; }

	private float _dayTimer;
	private bool _paused;

	protected override void OnAwake() => Instance = this;

	protected override void OnDestroy()
	{
		if ( Instance == this ) Instance = null;
	}

	public void ResetTimers()
	{
		_dayTimer = 0f;
		var save = GameCore.Instance?.Save;
		if ( save is null ) return;
		if ( save.NextThreatTimer <= 0f )
			save.NextThreatTimer = CureConstants.ThreatInterval( save.CurrentSeason ) * 0.5f;
	}

	protected override void OnUpdate()
	{
		var core = GameCore.Instance;
		if ( core is null || !core.IsCure || core.Phase != GamePhase.Day ) return;
		if ( _paused || core.IsUiBlocking ) return;

		var dt = Time.Delta;
		CureResearch.TickLabPoints( core, dt );
		TickSickness( core, dt );
		TickSeasonClock( core, dt );
		TickThreatTimer( core, dt );
	}

	public void SetPaused( bool paused ) => _paused = paused;

	private static void TickSickness( GameCore core, float dt )
	{
		var save = core.Save;
		var gain = CureConstants.SicknessPerDay( save.CurrentSeason ) * (dt / CureConstants.RealSecondsPerDay);
		gain *= TeamBonuses.SicknessGainMult( core );
		save.ColonySickness = MathF.Min( CureConstants.MaxSickness, save.ColonySickness + gain );
	}

	private void TickSeasonClock( GameCore core, float dt )
	{
		var save = core.Save;
		save.SeasonTimeAccum += dt;
		_dayTimer += dt;

		if ( _dayTimer < CureConstants.RealSecondsPerDay ) return;
		_dayTimer -= CureConstants.RealSecondsPerDay;
		save.SeasonDay++;

		if ( save.SeasonDay <= CureConstants.DaysPerSeason ) return;

		BeginSeasonEnd( core );
	}

	private void TickThreatTimer( GameCore core, float dt )
	{
		var save = core.Save;
		save.NextThreatTimer -= dt;
		if ( save.NextThreatTimer > 0f ) return;

		save.NextThreatTimer = CureConstants.ThreatInterval( save.CurrentSeason );
		core.TriggerThreat();
	}

	private void BeginSeasonEnd( GameCore core )
	{
		// ---------------------------------------------------------------------------
		// AUDIT FIX C1 — Season checkpoint ordering (2026-07)
		//
		// OLD BUG: Save() ran first while SeasonDay was already DaysPerSeason+1 (29).
		// RetrySeason restored Day 29 → next TickSeasonClock immediately re-ended season.
		//
		// NEW ORDER:
		//  1) Apply end-of-season lab lump (so it is in the checkpoint).
		//  2) Normalize calendar to "start of this season" (Day 1, threats cleared).
		//  3) Checkpoint THAT state (CheckpointYear/Season still = completed season).
		//  4) Then advance season/year for the live run.
		//
		// Revert tip: if season retry feels "too generous" or lab points double-dip,
		// check ApplySeasonLabBonus vs continuous TickLabPoints (M5 / lab policy).
		// ---------------------------------------------------------------------------
		CureResearch.ApplySeasonLabBonus( core );

		var save = core.Save;
		LastSeasonSummary = $"Year {save.CurrentYear} · {CureConstants.SeasonName( save.CurrentSeason )} complete — " +
			$"{save.ThreatsSurvivedThisSeason} threats survived, tier {save.CureResearchTier} research";

		// Normalize BEFORE checkpoint so Restore never sees SeasonDay=29.
		save.ThreatsSurvivedThisSeason = 0;
		save.SeasonDay = 1;
		save.SeasonTimeAccum = 0f;
		_dayTimer = 0f;

		SeasonCheckpoint.Save( core.Save );

		// Advance live calendar after the snapshot is locked.
		save.CurrentSeason++;
		if ( save.CurrentSeason >= 4 )
		{
			save.CurrentSeason = 0;
			save.CurrentYear++;
		}

		save.NextThreatTimer = CureConstants.ThreatInterval( save.CurrentSeason );
		RivalCivManager.ExpandSeason( core );
		SeasonRecapPending = true;
		core.BeginSeasonRecap( LastSeasonSummary );
		core.SaveManagerTouch();
	}

	public void DismissSeasonRecap()
	{
		SeasonRecapPending = false;
		GameCore.Instance?.DismissSeasonRecap();
	}
}
