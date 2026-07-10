namespace Dynasty.Data;

public sealed class FormationLayout
{
	public FormationType Type { get; init; }
	public FormationSide Side { get; init; }
	public string DisplayName { get; init; } = "";
	public IReadOnlyList<FormationSlot> Slots { get; init; } = Array.Empty<FormationSlot>();
}
