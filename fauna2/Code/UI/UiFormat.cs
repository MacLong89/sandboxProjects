namespace Fauna2.UI;

/// <summary>Human-readable labels for UI — avoids Razor treating "%" as modulo.</summary>
public static class UiFormat
{
	public static string Percent( float value, int decimals = 0 )
	{
		var clamped = value.Clamp( 0f, 100f );
		return decimals switch
		{
			1 => $"{clamped:0.0}%",
			2 => $"{clamped:0.00}%",
			_ => $"{clamped:0}%",
		};
	}

	/// <summary>Percent value for CSS width attributes (no % suffix).</summary>
	public static string PercentWidth( float value ) =>
		value.Clamp( 0f, 100f ).ToString( "0" );

	public static string Money( int amount ) => $"${amount:n0}";

	public static string MoneyPerMinute( float amount )
	{
		if ( amount >= 0f )
			return $"+${amount:0}/min";

		return $"−${Math.Abs( amount ):0}/min";
	}

	public static string AnimalAge( AnimalComponent animal )
	{
		if ( animal.IsElder ) return "Elder";
		return animal.IsAdult ? "Adult" : "Baby";
	}

	public static string AnimalSpeciesLine( AnimalComponent animal )
	{
		var species = animal.Definition?.DisplayName ?? "Animal";
		return $"{species} · {AnimalAge( animal )}";
	}

	public static string AnimalWellbeing( AnimalComponent animal ) =>
		$"Happy {Percent( animal.Happiness )} · Fed {Percent( animal.Hunger )} · Health {Percent( animal.Health )}";

	public static string BiomeLabel( Biome biome ) => BiomeIdentity.Label( biome );
}
