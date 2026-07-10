namespace Fauna2;

/// <summary>
/// Guest ticket revenue: guests × base rate × satisfaction × difficulty multiplier.
/// </summary>
public static class GuestRevenue
{
	public static float Multiplier => GameSettings.Current.GuestRevenueMultiplier;

	public static float SatisfactionMultiplier( float satisfaction ) =>
		0.45f + satisfaction / 200f;

	public static float PerSecond( int guestCount, float satisfaction ) =>
		GameConstants.AtGamePace( guestCount * GameConstants.IncomePerGuestPerSecond
			* SatisfactionMultiplier( satisfaction ) * Multiplier );

	public static float PerMinute( int guestCount, float satisfaction ) =>
		PerSecond( guestCount, satisfaction ) * 60f;
}
