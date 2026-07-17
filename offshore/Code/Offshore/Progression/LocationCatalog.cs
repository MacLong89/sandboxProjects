namespace Offshore;

public sealed class LocationDefinition
{
	public string Id { get; set; }
	public string DisplayName { get; set; }
	public string Description { get; set; }
	public float UnlockCost { get; set; }
	public string RequiredBoatId { get; set; } = "";
	public float WaterTintR { get; set; } = 0.12f;
	public float WaterTintG { get; set; } = 0.35f;
	public float WaterTintB { get; set; } = 0.62f;
	public float DepthBias { get; set; }
}

public static class LocationCatalog
{
	public static IReadOnlyList<LocationDefinition> All { get; } =
	[
		new() { Id = "old_dock", DisplayName = "Old Dock", Description = "Shallow starter water.", UnlockCost = 0f },
		new() { Id = "quiet_bay", DisplayName = "Quiet Bay", Description = "Calm mid-depth bay.", UnlockCost = 150f, RequiredBoatId = "rowboat", WaterTintR = 0.1f, WaterTintG = 0.4f, WaterTintB = 0.7f, DepthBias = 2f },
		new() { Id = "coastal", DisplayName = "Coastal Waters", Description = "Deeper blue with bigger fish.", UnlockCost = 450f, RequiredBoatId = "bay_boat", WaterTintR = 0.06f, WaterTintG = 0.22f, WaterTintB = 0.55f, DepthBias = 5f },
		new() { Id = "open_ocean", DisplayName = "Open Ocean", Description = "Wide water and legends.", UnlockCost = 1200f, RequiredBoatId = "sport_fisher", WaterTintR = 0.04f, WaterTintG = 0.12f, WaterTintB = 0.4f, DepthBias = 10f },
		new() { Id = "legendary_waters", DisplayName = "Legendary Waters", Description = "Endgame hunting grounds.", UnlockCost = 3000f, RequiredBoatId = "trawler", WaterTintR = 0.08f, WaterTintG = 0.05f, WaterTintB = 0.28f, DepthBias = 14f },
	];

	public static LocationDefinition Get( string id )
	{
		foreach ( var loc in All )
			if ( string.Equals( loc.Id, id, StringComparison.OrdinalIgnoreCase ) )
				return loc;
		return null;
	}
}

public static class LocationManager
{
	public static string CurrentDisplayName( PlayerProgressionData p ) =>
		LocationCatalog.Get( p.CurrentLocationId )?.DisplayName ?? "Unknown";

	public static bool TryTravel( OffshoreGameController game, string locationId )
	{
		if ( game is null )
			return false;

		var loc = LocationCatalog.Get( locationId );
		if ( loc is null )
			return false;

		if ( !game.Progression.UnlockedLocationIds.Contains( locationId ) )
		{
			game.SetStatus( $"{loc.DisplayName} is locked" );
			return false;
		}

		if ( !string.IsNullOrEmpty( loc.RequiredBoatId ) &&
		     !game.Progression.OwnedBoatIds.Contains( loc.RequiredBoatId ) &&
		     locationId != "old_dock" )
		{
			game.SetStatus( $"Need boat: {loc.RequiredBoatId}" );
			return false;
		}

		game.Progression.CurrentLocationId = locationId;
		TimeWeatherSystem.AdvanceOnTravel( game.Progression );
		OffshoreSaveSystem.Save( game.Progression );
		game.SetStatus( $"Arrived at {loc.DisplayName}" );
		return true;
	}

	public static void CheckAutoUnlocks( OffshoreGameController game )
	{
		foreach ( var loc in LocationCatalog.All )
		{
			if ( game.Progression.UnlockedLocationIds.Contains( loc.Id ) )
				continue;
			if ( loc.UnlockCost <= 0f )
				continue;
			if ( game.Progression.LifetimeMoneyEarned < loc.UnlockCost )
				continue;
			if ( !string.IsNullOrEmpty( loc.RequiredBoatId ) && !game.Progression.OwnedBoatIds.Contains( loc.RequiredBoatId ) )
				continue;

			game.Progression.UnlockedLocationIds.Add( loc.Id );
			game.SetStatus( $"Unlocked location: {loc.DisplayName}" );
		}
	}
}
