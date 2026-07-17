namespace Deep;

/// <summary>Surface day counter + in-dive clock for the meta HUD chip.</summary>
public sealed class DayClock
{
	public int DayNumber { get; private set; } = 1;
	public float TimeOfDayHours { get; private set; } = 8f + 35f / 60f; // 08:35

	public string TimeFormatted
	{
		get
		{
			var totalMinutes = (int)MathF.Floor( TimeOfDayHours * 60f );
			totalMinutes = ((totalMinutes % (24 * 60)) + (24 * 60)) % (24 * 60);
			var h24 = totalMinutes / 60;
			var m = totalMinutes % 60;
			var am = h24 < 12;
			var h12 = h24 % 12;
			if ( h12 == 0 ) h12 = 12;
			return $"{h12:00}:{m:00} {(am ? "AM" : "PM")}";
		}
	}

	public void AdvanceDuringDive( float diveSeconds )
	{
		// 1 real dive-second ≈ 8 in-world seconds.
		TimeOfDayHours += (diveSeconds * 8f) / 3600f;
		WrapDays();
	}

	public void AdvanceAfterDive( float diveSeconds, bool success )
	{
		// Dock / turnaround time only — dive time already advanced live.
		_ = diveSeconds;
		if ( TimeOfDayHours >= 18f || !success )
		{
			DayNumber++;
			TimeOfDayHours = 8f + (DayNumber % 5) * 0.15f;
		}
		else
		{
			TimeOfDayHours += 0.35f;
			WrapDays();
		}
	}

	public void ApplySave( int day, float hours )
	{
		DayNumber = Math.Max( 1, day );
		TimeOfDayHours = Math.Clamp( hours, 0f, 23.99f );
	}

	private void WrapDays()
	{
		while ( TimeOfDayHours >= 24f )
		{
			TimeOfDayHours -= 24f;
			DayNumber++;
		}
	}
}
