namespace Terraingen.UI.Core;

using Sandbox.UI;

/// <summary>Data-driven UI open request — all window opens should route through <see cref="ThornsUiManager.Request"/>.</summary>
public readonly struct ThornsUiRequest
{
	public string Id { get; init; }
	public ThornsUiWindowKind Kind { get; init; }
	public ThornsUiPriority Priority { get; init; }
	public Panel Panel { get; init; }
	public bool CapturesInput { get; init; }
	public bool BlocksGameplay { get; init; }
	public bool IsModal { get; init; }
	public Action OnEscape { get; init; }
	public Action OnConflictClose { get; init; }
	public ThornsUiManager.UiContext? Context { get; init; }

	public static ThornsUiRequest For(
		string id,
		ThornsUiWindowKind kind,
		Panel panel,
		Action onClose,
		bool isModal = true,
		bool blocksGameplay = true ) =>
		new()
		{
			Id = id,
			Kind = kind,
			Priority = ThornsUiCompatibility.DefaultPriority( kind ),
			Panel = panel,
			CapturesInput = true,
			BlocksGameplay = blocksGameplay,
			IsModal = isModal,
			OnEscape = onClose,
			OnConflictClose = onClose
		};
}
