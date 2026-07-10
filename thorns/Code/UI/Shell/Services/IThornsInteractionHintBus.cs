namespace Sandbox;

/// <summary>World interaction hint state — shell projects to screen.</summary>
public interface IThornsInteractionHintBus
{
	string Message { get; }
	bool HasWorldAnchor { get; }
	Vector3 WorldAnchor { get; }
	GameObject WorldTarget { get; }

	void Set( string message, GameObject target, Vector3 fallbackWorldAnchor, bool hasWorldAnchor );
	void Clear();
}
