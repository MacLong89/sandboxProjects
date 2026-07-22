namespace PawnShop;

/// <summary>An inspection tool the player can own and use during appraisal.</summary>
public sealed class ToolDef
{
	public InspectTool Id { get; init; }
	public string Name { get; init; }
	public string Description { get; init; }
	public int Cost { get; init; }
	public string Icon { get; init; } = "build";
	/// <summary>Seconds of patience an inspection with this tool costs the customer.</summary>
	public float UseCost { get; init; } = 3f;
}

public static class ToolCatalog
{
	public static readonly List<ToolDef> All = new()
	{
		new ToolDef { Id = InspectTool.Eyes, Name = "Visual Inspection", Cost = 0, Icon = "visibility", UseCost = 2f,
			Description = "Your own two eyes. Spots obvious damage, dirt, and visible labels." },
		new ToolDef { Id = InspectTool.Magnifier, Name = "Magnifying Glass", Cost = 150, Icon = "search", UseCost = 3f,
			Description = "Reveals fine cracks, small markings, fake printing, and maker stamps." },
		new ToolDef { Id = InspectTool.ElectronicsTester, Name = "Electronics Tester", Cost = 400, Icon = "electrical_services", UseCost = 5f,
			Description = "Tests power, battery health, and internal faults on anything with a plug." },
		new ToolDef { Id = InspectTool.MetalTester, Name = "Metal Tester", Cost = 500, Icon = "science", UseCost = 4f,
			Description = "Identifies metal type and exposes plating and fake precious metals." },
		new ToolDef { Id = InspectTool.GemTester, Name = "Gem Tester", Cost = 550, Icon = "diamond", UseCost = 4f,
			Description = "Tells real gemstones from glass and estimates stone quality." },
		new ToolDef { Id = InspectTool.UvLight, Name = "UV Light", Cost = 300, Icon = "flashlight_on", UseCost = 3f,
			Description = "Reveals hidden repairs, altered labels, security marks, and hidden signatures." },
		new ToolDef { Id = InspectTool.Database, Name = "Reference Database", Cost = 700, Icon = "storage", UseCost = 6f,
			Description = "Serial lookups, rarity data, recalls, and the stolen goods register." },
	};

	private static Dictionary<InspectTool, ToolDef> _byId;
	public static ToolDef Get( InspectTool id )
	{
		_byId ??= All.ToDictionary( d => d.Id );
		return _byId[id];
	}
}
