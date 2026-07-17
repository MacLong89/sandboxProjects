namespace Offshore;

public sealed class JournalEntry
{
	public string FishId { get; set; } = "";
	public int TimesCaught { get; set; }
	public float LargestSize { get; set; }
	public float HeaviestWeight { get; set; }
	public float HighestValue { get; set; }
	public bool Discovered { get; set; }
}

public static class JournalService
{
	public static void RegisterCatch( PlayerProgressionData p, CatchRecord catchRecord )
	{
		if ( p is null || catchRecord is null || string.IsNullOrEmpty( catchRecord.FishId ) )
			return;

		p.DiscoveredFishIds.Add( catchRecord.FishId );
		if ( !p.Journal.TryGetValue( catchRecord.FishId, out var entry ) )
		{
			entry = new JournalEntry { FishId = catchRecord.FishId, Discovered = true };
			p.Journal[catchRecord.FishId] = entry;
		}

		entry.Discovered = true;
		entry.TimesCaught++;
		if ( catchRecord.Size > entry.LargestSize )
		{
			entry.LargestSize = catchRecord.Size;
			catchRecord.IsPersonalRecord = true;
		}
		if ( catchRecord.Weight > entry.HeaviestWeight )
			entry.HeaviestWeight = catchRecord.Weight;
		if ( catchRecord.FinalValue > entry.HighestValue )
			entry.HighestValue = catchRecord.FinalValue;
	}

	public static JournalEntry Get( PlayerProgressionData p, string fishId )
	{
		if ( p.Journal.TryGetValue( fishId, out var entry ) )
			return entry;
		return new JournalEntry { FishId = fishId, Discovered = p.DiscoveredFishIds.Contains( fishId ) };
	}
}

public sealed class CollectionSetDefinition
{
	public string Id { get; set; }
	public string DisplayName { get; set; }
	public string[] FishIds { get; set; }
	public float RewardMoney { get; set; }
	public float RewardXp { get; set; }
}

public static class CollectionSystem
{
	public static IReadOnlyList<CollectionSetDefinition> Sets { get; } =
	[
		new() { Id = "dock_set", DisplayName = "Old Dock Species", FishIds = ["bluegill", "perch", "catfish", "bass", "trout"], RewardMoney = 75f, RewardXp = 40f },
		new() { Id = "bay_set", DisplayName = "Quiet Bay Set", FishIds = ["carp", "pike", "walleye"], RewardMoney = 150f, RewardXp = 60f },
		new() { Id = "coastal_set", DisplayName = "Coastal Set", FishIds = ["redsnapper", "tuna", "mahi"], RewardMoney = 300f, RewardXp = 100f },
		new() { Id = "legend_set", DisplayName = "Legendary Set", FishIds = ["marlin", "great_white"], RewardMoney = 1000f, RewardXp = 250f },
	];

	public static void NotifyCatch( PlayerProgressionData p, CatchRecord c )
	{
		foreach ( var set in Sets )
		{
			if ( p.CompletedCollectionIds.Contains( set.Id ) )
				continue;

			var complete = true;
			foreach ( var id in set.FishIds )
			{
				if ( !p.DiscoveredFishIds.Contains( id ) )
				{
					complete = false;
					break;
				}
			}

			if ( !complete )
				continue;

			p.CompletedCollectionIds.Add( set.Id );
			p.Money += set.RewardMoney;
			p.LifetimeMoneyEarned += set.RewardMoney;
			p.Experience += set.RewardXp;
		}
	}
}

public sealed class ContractDefinition
{
	public string Id { get; set; }
	public string DisplayName { get; set; } = "";
	public string Description { get; set; }
	public string RequiredFishId { get; set; } = "";
	public FishRarity? RequiredRarity { get; set; }
	public int RequiredCount { get; set; } = 1;
	public float RewardMoney { get; set; }
}

public static class ContractSystem
{
	public static IReadOnlyList<ContractDefinition> ActivePool { get; } =
	[
		new() { Id = "catch_3_bluegill", DisplayName = "A Good Catch", Description = "Catch 3 Bluegill.", RequiredFishId = "bluegill", RequiredCount = 3, RewardMoney = 30f },
		new() { Id = "catch_2_uncommon", DisplayName = "Something Special", Description = "Catch 2 Uncommon fish.", RequiredRarity = FishRarity.Uncommon, RequiredCount = 2, RewardMoney = 55f },
		new() { Id = "catch_bass", DisplayName = "Bass Ambition", Description = "Catch a Bass.", RequiredFishId = "bass", RequiredCount = 1, RewardMoney = 40f },
		new() { Id = "fill_cooler_value", DisplayName = "Market Day", Description = "Sell a haul worth $50+.", RequiredCount = 1, RewardMoney = 25f },
	];

	public static void NotifyCatch( PlayerProgressionData p, CatchRecord c )
	{
		EnsureSlots( p );
		foreach ( var id in p.ActiveContractIds.ToArray() )
		{
			var def = Get( id );
			if ( def is null || p.CompletedContractIds.Contains( id ) )
				continue;

			var ok = true;
			if ( !string.IsNullOrEmpty( def.RequiredFishId ) &&
			     !string.Equals( def.RequiredFishId, c.FishId, StringComparison.OrdinalIgnoreCase ) )
				ok = false;
			if ( def.RequiredRarity.HasValue && c.Rarity != def.RequiredRarity.Value )
				ok = false;
			if ( def.Id == "fill_cooler_value" )
				ok = false; // handled on sell

			if ( !ok )
				continue;

			p.ContractProgress[id] = p.ContractProgress.TryGetValue( id, out var cur ) ? cur + 1 : 1;
			if ( p.ContractProgress[id] >= def.RequiredCount )
				Complete( p, def );
		}
	}

	public static void NotifySale( PlayerProgressionData p, float total )
	{
		EnsureSlots( p );
		var def = Get( "fill_cooler_value" );
		if ( def is null || p.CompletedContractIds.Contains( def.Id ) )
			return;
		if ( total < 50f )
			return;
		if ( !p.ActiveContractIds.Contains( def.Id ) )
			return;
		Complete( p, def );
	}

	public static void EnsureSlots( PlayerProgressionData p )
	{
		if ( p.ActiveContractIds.Count > 0 )
			return;
		foreach ( var c in ActivePool )
		{
			if ( p.CompletedContractIds.Contains( c.Id ) )
				continue;
			p.ActiveContractIds.Add( c.Id );
			if ( p.ActiveContractIds.Count >= 3 )
				break;
		}
	}

	private static void Complete( PlayerProgressionData p, ContractDefinition def )
	{
		p.CompletedContractIds.Add( def.Id );
		p.ActiveContractIds.Remove( def.Id );
		p.Money += def.RewardMoney;
		p.LifetimeMoneyEarned += def.RewardMoney;
		p.ContractsCompleted++;
	}

	public static ContractDefinition Get( string id )
	{
		foreach ( var c in ActivePool )
			if ( string.Equals( c.Id, id, StringComparison.OrdinalIgnoreCase ) )
				return c;
		return null;
	}
}
