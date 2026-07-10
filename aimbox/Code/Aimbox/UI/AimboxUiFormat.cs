namespace Sandbox;

public static class AimboxUiFormat
{
	public static string Score( int value ) => value.ToString( "N0" );

	public static string CompactScore( int value ) =>
		value >= 1000 ? $"{value / 1000f:0.#}K" : value.ToString();
}
