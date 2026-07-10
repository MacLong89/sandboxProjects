namespace Fauna2;

/// <summary>Breakdown of a habitat's 0–100 quality score.</summary>
public struct HabitatScoreBreakdown
{
	public float Space;
	public float BiomeMatch;
	public float Shelter;
	public float Water;
	public float Enrichment;
	public float Decoration;
	public float Total;
}

/// <summary>
/// Pure scoring logic, shared by host simulation and any client UI that wants
/// to preview quality. Factors: space, biome match, shelter, water, enrichment
/// and decoration.
/// </summary>
public static class HabitatScoring
{
	private static readonly List<AnimalComponent> _animalScratch = new();
	private static readonly List<PlaceableComponent> _contentScratch = new();

	public static HabitatScoreBreakdown Score( HabitatComponent habitat )
	{
		var result = new HabitatScoreBreakdown();

		_animalScratch.Clear();
		foreach ( var animal in AnimalRegistry.InHabitat( habitat.HabitatId ) )
			_animalScratch.Add( animal );

		_contentScratch.Clear();
		foreach ( var placeable in PlaceableRegistry.InsideRect( habitat.GameObject.WorldPosition, habitat.Size ) )
			_contentScratch.Add( placeable );

		var animals = _animalScratch;
		var contents = _contentScratch;

		// ── Space ───────────────────────────────────────────
		var area = habitat.Size.x * habitat.Size.y;
		var needed = animals.Sum( a => a.Definition?.SpaceNeed ?? 0f );
		result.Space = needed <= 0f ? 100f : (area / needed).Clamp( 0f, 1f ) * 100f;

		// ── Biome match ─────────────────────────────────────
		if ( animals.Count == 0 )
		{
			result.BiomeMatch = 100f;
		}
		else
		{
			var matching = animals.Count( a => a.Definition?.Biome == habitat.Biome );
			result.BiomeMatch = (float)matching / animals.Count * 100f;
		}

		// ── Shelter (one per 4 animals) ─────────────────────
		var shelters = contents.Count( p => p.Definition?.IsShelter ?? false );
		var sheltersNeeded = Math.Max( 1, (animals.Count + 3) / 4 );
		result.Shelter = ((float)shelters / sheltersNeeded).Clamp( 0f, 1f ) * 100f;

		// ── Water ───────────────────────────────────────────
		result.Water = contents.Any( p => p.Definition?.IsWater ?? false ) ? 100f : 0f;

		// ── Enrichment ──────────────────────────────────────
		var enrichment = contents.Sum( p => p.Definition?.EnrichmentValue ?? 0f );
		var enrichmentNeeded = Math.Max( 2f, animals.Count * 2f );
		result.Enrichment = (enrichment / enrichmentNeeded).Clamp( 0f, 1f ) * 100f;

		// ── Decoration / beauty ─────────────────────────────
		var beauty = contents.Sum( p => p.Definition?.AppealBonus ?? 0f );
		result.Decoration = (beauty / 8f).Clamp( 0f, 1f ) * 100f;

		result.Total =
			result.Space * 0.30f +
			result.BiomeMatch * 0.25f +
			result.Shelter * 0.15f +
			result.Water * 0.10f +
			result.Enrichment * 0.12f +
			result.Decoration * 0.08f;

		var weather = WeatherSeasonSystem.Instance;
		if ( weather is not null )
			result.Total += BiomeIdentity.HabitatScoreBonus( habitat.Biome, weather.Season, weather.Weather );

		result.Total += StaffSystem.Instance?.KeeperHabitatBonus ?? 0f;
		result.Total += ResearchSystem.Instance?.HabitatScoreBonus ?? 0f;
		result.Total = result.Total.Clamp( 0f, 100f );

		return result;
	}
}
