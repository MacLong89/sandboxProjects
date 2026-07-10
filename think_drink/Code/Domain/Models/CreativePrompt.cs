namespace ThinkDrink.Domain;

public sealed class CreativePromptEntry
{
	public string Id { get; set; } = "";
	public string Category { get; set; } = "";
	public string Prompt { get; set; } = "";
}

public sealed class CreativePromptBank
{
	public List<CreativePromptEntry> QuipFill { get; set; } = new();
	public List<CreativePromptEntry> CaptionThis { get; set; } = new();
	public List<CreativePromptEntry> SketchQuips { get; set; } = new();
}
