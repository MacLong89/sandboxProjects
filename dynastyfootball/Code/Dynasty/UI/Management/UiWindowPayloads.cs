namespace Dynasty.UI.Management;

using Dynasty.UI.ViewModels;

public sealed class WeekSummaryPayload
{
	public string Title { get; init; } = "Week Summary";
	public string Headline { get; init; } = "";
	public IReadOnlyList<string> Lines { get; init; } = Array.Empty<string>();

	public string Text => Headline;
}

public sealed class TeamPlayerDialogPayload
{
	public PlayerDetailViewModel Player { get; init; }
}

public sealed class FormationPickerPayload
{
	public PlayerPickerViewModel Picker { get; init; }
}
