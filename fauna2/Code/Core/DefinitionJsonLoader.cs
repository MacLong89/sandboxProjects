namespace Fauna2;

/// <summary>Parses authored .place / .animal JSON when compiled GameResources are unavailable.</summary>
internal static class DefinitionJsonLoader
{
	public static string ReadAssetText( string path )
	{
		if ( string.IsNullOrWhiteSpace( path ) )
			return null;

		foreach ( var candidate in PathCandidates( path ) )
		{
			try
			{
				if ( FileSystem.Mounted.FileExists( candidate ) )
					return FileSystem.Mounted.ReadAllText( candidate );
			}
			catch
			{
				// Try the next candidate path.
			}

			try
			{
				return FileSystem.Mounted.ReadAllText( candidate );
			}
			catch
			{
				// Try the next candidate path.
			}
		}

		return null;
	}

	public static PlaceableDefinition ParsePlaceable( string json, string sourcePath = null )
	{
		if ( string.IsNullOrWhiteSpace( json ) )
			return null;

		var dto = Json.Deserialize<PlaceableJsonDto>( json );
		if ( dto is null || string.IsNullOrWhiteSpace( dto.DisplayName ) )
			return null;

		var category = ParseEnum( dto.Category, BuildCategory.Decorations );
		var habitatSize = ParseVector2( dto.HabitatSize, new Vector2( 512, 512 ) );

		var def = new PlaceableDefinition
		{
			DisplayName = dto.DisplayName,
			Description = dto.Description ?? "",
			Category = category,
			Cost = dto.Cost,
			UnlockLevel = dto.UnlockLevel,
			RequiredPrestige = dto.RequiredPrestige,
			AppealBonus = dto.AppealBonus,
			EnrichmentValue = dto.EnrichmentValue,
			EducationValue = dto.EducationValue,
			ComfortValue = dto.ComfortValue,
			DecorSet = dto.DecorSet ?? "",
			IsShelter = dto.IsShelter,
			IsWater = dto.IsWater,
			IsRestroom = dto.IsRestroom,
			IsRestaurant = dto.IsRestaurant,
			IsShop = dto.IsShop,
			CollectIncomePerMinute = dto.CollectIncomePerMinute,
			GuestsServed = dto.GuestsServed,
			MaxStoredRevenue = dto.MaxStoredRevenue,
			HabitatSize = habitatSize,
			HabitatBiome = ParseBiome( dto.HabitatBiome, Biome.Grassland ),
			Footprint = ParseVector2( dto.Footprint, GameConstants.StandardBuildingFootprint ),
			GridSnap = dto.GridSnap > 0f ? dto.GridSnap : 64f,
			RotationStep = dto.RotationStep > 0f ? dto.RotationStep : 45f,
			Visuals = ParseVisuals( dto.Visuals ),
		};

		return def;
	}

	public static AnimalDefinition ParseAnimal( string json, string sourcePath = null )
	{
		if ( string.IsNullOrWhiteSpace( json ) )
			return null;

		var dto = Json.Deserialize<AnimalJsonDto>( json );
		if ( dto is null || string.IsNullOrWhiteSpace( dto.DisplayName ) )
			return null;

		return new AnimalDefinition
		{
			DisplayName = dto.DisplayName,
			Species = string.IsNullOrWhiteSpace( dto.Species ) ? Defs.ResourceStem( sourcePath ?? "" ) : dto.Species,
			Description = dto.Description ?? "",
			Biome = ParseBiome( dto.Biome, Biome.Grassland ),
			MinHabitatSize = ParseEnum( dto.MinHabitatSize, HabitatSizeTier.Small ),
			Rarity = ParseEnum( dto.Rarity, AnimalRarity.Common ),
			Cost = dto.Cost,
			UnlockLevel = dto.UnlockLevel,
			RequiredPrestige = dto.RequiredPrestige,
			CatchDifficulty = dto.CatchDifficulty,
			RequiresTranquilizer = dto.RequiresTranquilizer,
			WildAggression = dto.WildAggression,
			WildAttackDifficulty = dto.WildAttackDifficulty,
			WildAttackPenaltyFraction = dto.WildAttackPenaltyFraction,
			GuestAppeal = dto.GuestAppeal,
			SpaceNeed = dto.SpaceNeed,
			BreedingHappiness = dto.BreedingHappiness,
			BreedingDifficulty = dto.BreedingDifficulty,
			IsSocial = dto.IsSocial,
			AdultAge = dto.AdultAge,
			ElderAge = dto.ElderAge,
			MoveSpeed = dto.MoveSpeed,
			Locomotion = ParseEnum( dto.Locomotion, AnimalLocomotion.Walker ),
			RealWorldHeightMeters = dto.RealWorldHeightMeters,
			BodyScale = ParseVector3( dto.BodyScale, new Vector3( 0.8f, 0.5f, 0.5f ) ),
			BodyTint = ParseColor( dto.BodyTint, Color.White ),
		};
	}

	private static IEnumerable<string> PathCandidates( string path )
	{
		var normalized = path.Replace( '\\', '/' ).TrimStart( '/' );
		yield return normalized;

		if ( !normalized.StartsWith( "assets/", StringComparison.OrdinalIgnoreCase ) )
			yield return "assets/" + normalized;
	}

	private static List<VisualPart> ParseVisuals( List<VisualPartJsonDto> visuals )
	{
		if ( visuals is null || visuals.Count == 0 )
			return new List<VisualPart>();

		var parts = new List<VisualPart>( visuals.Count );
		foreach ( var visual in visuals )
		{
			if ( visual is null || string.IsNullOrWhiteSpace( visual.Model ) )
				continue;

			parts.Add( new VisualPart
			{
				Model = visual.Model,
				Offset = ParseVector3( visual.Offset, Vector3.Zero ),
				Scale = ParseVector3( visual.Scale, Vector3.One ),
				Tint = ParseColor( visual.Tint, Color.White ),
			} );
		}

		return parts;
	}

	private static Biome ParseBiome( string value, Biome fallback ) =>
		BiomeIdentity.TryParse( value, out var biome ) ? biome : fallback;

	private static TEnum ParseEnum<TEnum>( string value, TEnum fallback ) where TEnum : struct, Enum
	{
		if ( string.IsNullOrWhiteSpace( value ) )
			return fallback;

		return Enum.TryParse( value, true, out TEnum parsed ) ? parsed : fallback;
	}

	private static Vector2 ParseVector2( string value, Vector2 fallback )
	{
		if ( !TryParseFloatPair( value, out var x, out var y ) )
			return fallback;

		return new Vector2( x, y );
	}

	private static Vector3 ParseVector3( string value, Vector3 fallback )
	{
		if ( string.IsNullOrWhiteSpace( value ) )
			return fallback;

		var parts = value.Split( ',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries );
		if ( parts.Length < 3 )
			return fallback;

		if ( !float.TryParse( parts[0], out var x ) || !float.TryParse( parts[1], out var y ) || !float.TryParse( parts[2], out var z ) )
			return fallback;

		return new Vector3( x, y, z );
	}

	private static Color ParseColor( string value, Color fallback )
	{
		if ( string.IsNullOrWhiteSpace( value ) )
			return fallback;

		var parts = value.Split( ',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries );
		if ( parts.Length < 3 )
			return fallback;

		if ( !float.TryParse( parts[0], out var r ) || !float.TryParse( parts[1], out var g ) || !float.TryParse( parts[2], out var b ) )
			return fallback;

		var a = parts.Length >= 4 && float.TryParse( parts[3], out var alpha ) ? alpha : 1f;
		return new Color( r, g, b, a );
	}

	private static bool TryParseFloatPair( string value, out float x, out float y )
	{
		x = y = 0f;
		if ( string.IsNullOrWhiteSpace( value ) )
			return false;

		var parts = value.Split( ',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries );
		if ( parts.Length < 2 )
			return false;

		return float.TryParse( parts[0], out x ) && float.TryParse( parts[1], out y );
	}

	private sealed class PlaceableJsonDto
	{
		public string DisplayName { get; set; }
		public string Description { get; set; }
		public string Category { get; set; }
		public int Cost { get; set; }
		public int UnlockLevel { get; set; } = 1;
		public int RequiredPrestige { get; set; }
		public float AppealBonus { get; set; }
		public float EnrichmentValue { get; set; }
		public float EducationValue { get; set; }
		public float ComfortValue { get; set; }
		public string DecorSet { get; set; }
		public bool IsShelter { get; set; }
		public bool IsWater { get; set; }
		public bool IsRestroom { get; set; }
		public bool IsRestaurant { get; set; }
		public bool IsShop { get; set; }
		public float CollectIncomePerMinute { get; set; }
		public int GuestsServed { get; set; }
		public float MaxStoredRevenue { get; set; }
		public string HabitatSize { get; set; }
		public string HabitatBiome { get; set; }
		public string Footprint { get; set; }
		public float GridSnap { get; set; }
		public float RotationStep { get; set; }
		public List<VisualPartJsonDto> Visuals { get; set; }
	}

	private sealed class VisualPartJsonDto
	{
		public string Model { get; set; }
		public string Offset { get; set; }
		public string Scale { get; set; }
		public string Tint { get; set; }
	}

	private sealed class AnimalJsonDto
	{
		public string DisplayName { get; set; }
		public string Species { get; set; }
		public string Description { get; set; }
		public string Biome { get; set; }
		public string MinHabitatSize { get; set; }
		public string Rarity { get; set; }
		public int Cost { get; set; }
		public int UnlockLevel { get; set; } = 1;
		public int RequiredPrestige { get; set; }
		public float CatchDifficulty { get; set; }
		public bool RequiresTranquilizer { get; set; }
		public float WildAggression { get; set; }
		public float WildAttackDifficulty { get; set; }
		public float WildAttackPenaltyFraction { get; set; }
		public float GuestAppeal { get; set; }
		public float SpaceNeed { get; set; }
		public float BreedingHappiness { get; set; }
		public float BreedingDifficulty { get; set; }
		public bool IsSocial { get; set; } = true;
		public float AdultAge { get; set; }
		public float ElderAge { get; set; }
		public float MoveSpeed { get; set; }
		public string Locomotion { get; set; }
		public float RealWorldHeightMeters { get; set; }
		public string BodyScale { get; set; }
		public string BodyTint { get; set; }
	}
}
