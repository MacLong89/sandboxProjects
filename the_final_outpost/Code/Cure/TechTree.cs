namespace FinalOutpost;

public sealed class TechNodeDef
{
	public string Id { get; init; }
	public string Name { get; init; }
	public string Icon { get; init; }
	public string Description { get; init; }
	public int RequiredSeason { get; init; } = 1;
	public string[] Prerequisites { get; init; } = Array.Empty<string>();
	public double KnowledgeCost { get; init; }
	public double ScrapCost { get; init; }
	public string[] UnlocksBuildings { get; init; } = Array.Empty<string>();
}

public static class TechTreeCatalog
{
	public static readonly IReadOnlyList<TechNodeDef> All = new List<TechNodeDef>
	{
		new() { Id = "agriculture", Name = "Agriculture", Icon = "agriculture", Description = "Unlock Farms and Farmers.", RequiredSeason = 1, KnowledgeCost = 40, UnlocksBuildings = new[] { "farm" } },
		new() { Id = "industry", Name = "Industry", Icon = "factory", Description = "Unlock Factories and Operators.", RequiredSeason = 1, Prerequisites = new[] { "agriculture" }, KnowledgeCost = 55, UnlocksBuildings = new[] { "factory" } },
		new() { Id = "literacy", Name = "Literacy", Icon = "menu_book", Description = "Unlock Libraries, Schools, and Scholars.", RequiredSeason = 2, KnowledgeCost = 60, UnlocksBuildings = new[] { "library", "school" } },
		new() { Id = "medicine", Name = "Medicine", Icon = "local_hospital", Description = "Unlock Hospitals and Medics.", RequiredSeason = 2, Prerequisites = new[] { "agriculture" }, KnowledgeCost = 70, UnlocksBuildings = new[] { "hospital" } },
		new() { Id = "commerce", Name = "Commerce", Icon = "storefront", Description = "Unlock Shops and Merchants — steady scrap income.", RequiredSeason = 2, Prerequisites = new[] { "industry" }, KnowledgeCost = 50, ScrapCost = 100, UnlocksBuildings = new[] { "shop" } },
		new() { Id = "tactics", Name = "Field Tactics", Icon = "military_tech", Description = "Soldiers respond faster to move orders.", RequiredSeason = 1, KnowledgeCost = 35 },
		new() { Id = "diplomacy", Name = "Diplomacy", Icon = "handshake", Description = "Better trades with neighboring colonies.", RequiredSeason = 3, KnowledgeCost = 80, Prerequisites = new[] { "commerce" } },
		new() { Id = "synthesis", Name = "Advanced Synthesis", Icon = "science", Description = "+25% lab output.", RequiredSeason = 3, Prerequisites = new[] { "literacy", "medicine" }, KnowledgeCost = 120 }
	};

	public static TechNodeDef Get( string id ) => All.FirstOrDefault( n => n.Id == id );

	public static bool IsUnlocked( SaveData save, string id ) =>
		save?.UnlockedTech?.Contains( id ) == true;

	public static bool CanResearch( GameCore core, TechNodeDef node )
	{
		if ( core?.Save is null || node is null ) return false;
		if ( IsUnlocked( core.Save, node.Id ) ) return false;
		if ( CureConstants.ProgressSeason( core.Save ) < node.RequiredSeason ) return false;

		foreach ( var pre in node.Prerequisites )
			if ( !IsUnlocked( core.Save, pre ) ) return false;

		if ( core.Resources.Get( ResourceKind.Knowledge ) < node.KnowledgeCost ) return false;
		if ( node.ScrapCost > 0 && core.Wallet.Scrap < node.ScrapCost ) return false;

		return true;
	}

	public static bool TryUnlock( GameCore core, string id )
	{
		var node = Get( id );
		if ( node is null || !CanResearch( core, node ) ) return false;

		if ( node.KnowledgeCost > 0 && !core.Resources.TrySpend( ResourceKind.Knowledge, node.KnowledgeCost ) )
			return false;
		if ( node.ScrapCost > 0 && !core.Wallet.TrySpend( node.ScrapCost ) )
			return false;

		core.Save.UnlockedTech ??= new List<string>();
		if ( !core.Save.UnlockedTech.Contains( id ) )
			core.Save.UnlockedTech.Add( id );

		core.SaveManagerTouch();
		Sfx.Play( Sfx.Purchase, "TechUnlock" );
		return true;
	}

	public static bool BuildingUnlockedByTech( SaveData save, string buildingKey )
	{
		if ( save is null ) return false;
		foreach ( var node in All )
		{
			if ( !IsUnlocked( save, node.Id ) ) continue;
			if ( node.UnlocksBuildings.Contains( buildingKey ) ) return true;
		}
		return false;
	}

	public static string GateLabelForBuilding( SaveData save, string buildingKey )
	{
		if ( BuildingUnlockedByTech( save, buildingKey ) )
			return "Unlocked";

		foreach ( var node in All )
		{
			if ( !node.UnlocksBuildings.Contains( buildingKey ) ) continue;
			return node.Name;
		}

		return "Tech Locked";
	}

	public static TechNodeDef NodeForBuilding( string buildingKey ) =>
		All.FirstOrDefault( n => n.UnlocksBuildings.Contains( buildingKey ) );
}
