namespace PawnShop;

/// <summary>Rolls and applies the day's random event.</summary>
public sealed class EventSystem
{
	private readonly SaveData _save;

	public EventDef Today { get; private set; }

	public EventSystem( SaveData save )
	{
		_save = save;
		Today = EventCatalog.Get( save.TodaysEvent );
	}

	/// <summary>Roll a fresh event for a new day (~35% of days have one).</summary>
	public void RollForDay( int day )
	{
		Today = null;
		_save.TodaysEvent = null;

		if ( day <= 1 ) return; // quiet first day for the tutorial
		if ( Game.Random.Float() > 0.35f ) return;

		Today = EventCatalog.Random();
		_save.TodaysEvent = Today.Id;
	}

	/// <summary>Dev: force a specific event for today.</summary>
	public void Force( EventDef def )
	{
		Today = def;
		_save.TodaysEvent = def?.Id;
	}

	public float DemandFor( ItemCategory category )
	{
		if ( Today is null ) return 1f;
		return Today.Categories.Contains( category ) ? Today.DemandMult : 1f;
	}

	public float TrafficMult => Today?.TrafficMult ?? 1f;
	public float ScamMult => Today?.ScamMult ?? 1f;
	public float TheftMult => Today?.TheftMult ?? 1f;
	public bool PoliceActive => Today?.Id == "police_sweep";
}
