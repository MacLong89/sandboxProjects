namespace Sandbox;

/// <summary>HUD panel creation contract — implemented by <see cref="ThornsGameShell"/> view layer.</summary>
public interface IThornsHudPresenter
{
	bool IsLocalOwned { get; }
	void EnsureGameplayOverlayPanels();
	ThornsToastBusEntry OnToastEnqueued( ThornsToastBusEntry entry );
	void OnToastRemoved( ThornsToastBusEntry entry );
	void OnInteractionHintChanged();
	Component GetComponentForHintProjection();
}
