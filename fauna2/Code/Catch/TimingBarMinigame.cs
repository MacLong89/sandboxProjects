namespace Fauna2;

/// <summary>Shared oscillating timing-bar logic for catch and wild-attack minigames.</summary>
internal static class TimingBarMinigame
{
	public static void TickBar( ref float barPosition, ref bool ascending, float barSpeed, float deltaTime )
	{
		var step = barSpeed * deltaTime;
		barPosition += ascending ? step : -step;

		if ( barPosition >= 1f )
		{
			barPosition = 1f;
			ascending = false;
		}
		else if ( barPosition <= 0f )
		{
			barPosition = 0f;
			ascending = true;
		}
	}

	public static void RandomizeGreenZone( out float greenStart, out float greenEnd, float zoneWidth )
	{
		zoneWidth = zoneWidth.Clamp( 0.08f, 0.5f );
		greenStart = Game.Random.Float( 0.12f, 0.88f - zoneWidth );
		greenEnd = greenStart + zoneWidth;
	}

	public static void AdvanceRound( ref float barPosition, ref bool ascending, out float greenStart, out float greenEnd, float zoneWidth, float barSpeed )
	{
		barPosition = 0f;
		ascending = true;
		RandomizeGreenZone( out greenStart, out greenEnd, zoneWidth );
	}

	public static bool IsInGreenZone( float barPosition, float greenStart, float greenEnd ) =>
		barPosition >= greenStart && barPosition <= greenEnd;
}
