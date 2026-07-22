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
		var save = core.Save;
		LastSeasonSummary = $"Year {save.CurrentYear} · {CureConstants.SeasonName( save.CurrentSeason )} complete — " +
			$"{save.ThreatsSurvivedThisSeason} threats survived, tier {save.CureResearchTier} research";

		// Advance calendar first so the checkpoint is Day 1 of the newly started season.
		save.CurrentSeason++;
		if ( save.CurrentSeason >= 4 )
		{
			save.CurrentSeason = 0;
			save.CurrentYear++;
		}

		save.ThreatsSurvivedThisSeason = 0;
		save.SeasonDay = 1;
		save.SeasonTimeAccum = 0f;
		_dayTimer = 0f;
		save.NextThreatTimer = CureConstants.ThreatInterval( save.CurrentSeason );

		PlotManager.Instance?.SaveClearProgress( save );
		SeasonCheckpoint.Save( core.Save );

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
