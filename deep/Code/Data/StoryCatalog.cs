namespace Deep;

public sealed class StoryDefinition
{
	public string Id { get; init; }
	public string Title { get; init; }
	public string Text { get; init; }
	public float MinDepth { get; init; }
	public float MaxDepth { get; init; }
	public float WorldX { get; init; }
	public string RelatedMystery { get; init; }
	public ToolKind? RequiredTool { get; init; }
	public Color Tint { get; init; } = new( 0.55f, 0.85f, 0.95f );
}

public static class StoryCatalog
{
	public static IReadOnlyList<StoryDefinition> All { get; } =
	[
		new()
		{
			Id = "log_01", Title = "Surface Log", RelatedMystery = "The Silent Fleet",
			Text = "Day 14. The relay pinged once from below the blue line, then nothing.",
			MinDepth = 35f, MaxDepth = 55f, WorldX = -18f
		},
		new()
		{
			Id = "log_02", Title = "Relay Fragment", RelatedMystery = "The Silent Fleet",
			Text = "Black-box scrape: \"...do not surface... hold the trench...\"",
			MinDepth = 95f, MaxDepth = 125f, WorldX = 12f, RequiredTool = ToolKind.Scanner
		},
		new()
		{
			Id = "log_03", Title = "Ruin Etching", RelatedMystery = "Stone Choir",
			Text = "Carved marks spiral toward the hadal shelf. Someone counted the descents.",
			MinDepth = 160f, MaxDepth = 210f, WorldX = -8f, RequiredTool = ToolKind.Scanner
		},
		new()
		{
			Id = "log_04", Title = "Hadal Beacon", RelatedMystery = "Stone Choir",
			Text = "A cold signal. Not ours. It answers when the scanner sweeps past.",
			MinDepth = 260f, MaxDepth = 320f, WorldX = 22f, RequiredTool = ToolKind.Scanner
		},
		new()
		{
			Id = "log_05", Title = "Last Note", RelatedMystery = "The Silent Fleet",
			Text = "If you find the sunken relay, bank the haul at the bells. Trust the depth.",
			MinDepth = 340f, MaxDepth = 390f, WorldX = 0f, RequiredTool = ToolKind.Scanner
		},
	];

	public static StoryDefinition Get( string id ) =>
		All.FirstOrDefault( s => s.Id == id );
}
