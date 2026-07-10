namespace Terraingen.GameData;

using Sandbox;
using Terraingen.Combat;

/// <summary>UI-facing tame traits, icons, and species metadata.</summary>
public static class ThornsTameCatalog
{
	public const int MaxTameSlots = 12;

	/// <summary>~2 meters in world inches.</summary>
	public const float SummonOffsetInches = 78.74f;

	public static readonly ThornsTameCommand[] UiCommands =
	{
		ThornsTameCommand.Follow,
		ThornsTameCommand.Stay,
		ThornsTameCommand.Attack,
		ThornsTameCommand.Guard,
		ThornsTameCommand.Passive
	};

	public static string CommandDescription( ThornsTameCommand command ) => command switch
	{
		ThornsTameCommand.Follow => "Your tame follows you.",
		ThornsTameCommand.Stay => "Your tame holds its position.",
		ThornsTameCommand.Attack => "Your tame engages hostile targets.",
		ThornsTameCommand.Guard => "Your tame patrols and defends this area.",
		ThornsTameCommand.Passive => "Your tame will not fight unless attacked.",
		ThornsTameCommand.Summon => "Teleport your tame to your side.",
		_ => ""
	};

	/// <summary>Portrait PNG under <c>Assets/ui/iconsv8/{speciesKey}.png</c>.</summary>
	public static string CreaturePortraitPath( string speciesKey )
	{
		return ThornsIconRegistry.Creature( speciesKey );
	}

	public static string StatIconPath( string statKey ) => ThornsIconRegistry.TameStat( statKey );

	public static string CommandIconPath( ThornsTameCommand command ) => ThornsIconRegistry.TameCommand( command );

	public static string FeedCommandIconPath() => ThornsIconRegistry.Item( "apple" );

	public static string TraitIconPath( string traitId ) => ThornsIconRegistry.TameTrait( traitId );

	public static IReadOnlyList<ThornsTameTraitDto> GetTraitsForSpecies( string speciesKey ) => speciesKey switch
	{
		"wolf" => new[]
		{
			Trait( "pack_hunter", "Pack Hunter", "Nearby tames deal 5% more damage." ),
			Trait( "keen_senses", "Keen Senses", "Detects threats from further away." )
		},
		"panther" => new[]
		{
			Trait( "ambush", "Ambush Predator", "First attack after stalking deals bonus damage." ),
			Trait( "night_stalker", "Night Stalker", "Moves faster and sees farther at night." )
		},
		"deer" => new[]
		{
			Trait( "fleet_footed", "Fleet Footed", "Escapes danger faster when fleeing." ),
			Trait( "forager", "Forager", "Finds edible plants while wandering." )
		},
		"moose" => new[]
		{
			Trait( "ironhide", "Ironhide", "Takes less damage from melee attacks." ),
			Trait( "trample", "Trample", "Charges can knock back smaller foes." )
		},
		_ => new[] { Trait( "survivor", "Survivor", "A hardened companion of the wild." ) }
	};

	public static int PerceptionScore( float detectionRange )
	{
		const float wolfBaseline = 2775f;
		return (int)Math.Clamp( detectionRange / wolfBaseline * 85f, 10f, 250f );
	}

	public static int SpeedPercent( float moveSpeed )
	{
		const float deerBaseline = 280f;
		return (int)Math.Clamp( moveSpeed / deerBaseline * 100f, 25f, 500f );
	}

	public static string FormatTierLevelLine( int tier, string speciesName, int level ) =>
		$"{ShortTierLabel( tier )} {( speciesName ?? "Tame" ).ToUpperInvariant()} - LEVEL {Math.Max( 1, level )}";

	public static string FormatTamePickupLabel( int tier, string speciesName ) =>
		$"{ShortTierLabel( tier )} {speciesName ?? "Tame"}";

	public static string ShortTierLabel( int tier ) => tier switch
	{
		1 => "TIER I",
		2 => "TIER II",
		3 => "TIER III",
		4 => "TIER IV",
		5 => "TIER V",
		_ => "TIER —"
	};

	public static string TierRarityName( int tier ) => tier switch
	{
		1 => "Common",
		2 => "Uncommon",
		3 => "Rare",
		4 => "Epic",
		5 => "Legendary",
		_ => "Unknown"
	};

	public static string FormatTierRarityLine( int tier ) =>
		$"{ShortTierLabel( Math.Clamp( tier, 1, 5 ) )} · {TierRarityName( tier )}";

	public static Color TierColor( int tier ) => Math.Clamp( tier, 1, 5 ) switch
	{
		5 => new Color( 0.78f, 0.42f, 0.95f, 1f ),
		_ => ThornsWeaponTierVisuals.TitleTint( tier )
	};

	public const int BreedCooldownSeconds = 600;

	public static bool IsOnBreedCooldown( long untilUtcTicks ) =>
		untilUtcTicks > DateTime.UtcNow.Ticks;

	public static int BreedCooldownSecondsRemaining( long untilUtcTicks )
	{
		if ( untilUtcTicks <= DateTime.UtcNow.Ticks )
			return 0;

		return (int)Math.Ceiling( (untilUtcTicks - DateTime.UtcNow.Ticks) / (double)TimeSpan.TicksPerSecond );
	}

	public static string FormatBreedCooldownRemaining( long untilUtcTicks )
	{
		var secs = BreedCooldownSecondsRemaining( untilUtcTicks );
		if ( secs <= 0 )
			return "";

		var minutes = secs / 60;
		var seconds = secs % 60;
		return minutes > 0 ? $"{minutes}m {seconds}s" : $"{seconds}s";
	}

	static ThornsTameTraitDto Trait( string id, string title, string description ) => new()
	{
		Id = id,
		Title = title,
		Description = description,
		IconPath = TraitIconPath( id )
	};
}
