namespace Deep;

/// <summary>Time-of-day tint overlaid on the dive camera clear color.</summary>
public static class DayNightVisuals
{
	public static Color Apply( Color baseWater, DayClock clock )
	{
		if ( clock is null ) return baseWater;

		var h = clock.TimeOfDayHours;
		float warm;
		float dim;
		if ( h >= 6f && h < 10f )
		{
			warm = 0.08f;
			dim = 0f;
		}
		else if ( h >= 10f && h < 16f )
		{
			warm = 0f;
			dim = 0f;
		}
		else if ( h >= 16f && h < 19f )
		{
			warm = 0.12f;
			dim = 0.08f;
		}
		else
		{
			warm = 0.02f;
			dim = 0.28f;
		}

		var tinted = Color.Lerp( baseWater, new Color( 0.9f, 0.55f, 0.35f ), warm );
		return Color.Lerp( tinted, new Color( 0.02f, 0.04f, 0.1f ), dim );
	}
}
