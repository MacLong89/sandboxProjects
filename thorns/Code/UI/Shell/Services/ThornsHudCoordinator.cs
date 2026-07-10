namespace Sandbox;

/// <summary>HUD orchestration — toast, interaction hints, menu buses bound to shell presenter.</summary>
public sealed class ThornsHudCoordinator
{
	public ThornsToastBus Toast { get; } = new();
	public ThornsInteractionHintBus Interaction { get; } = new();
	public ThornsMenuHost Menu { get; } = new();

	public void BindPresenter( IThornsHudPresenter presenter )
	{
		Toast.BindPresenter( presenter );
		Interaction.BindPresenter( presenter );
	}
}
