namespace FinalOutpost;

/// <summary>
/// Deprecated: kill-combo scrap multipliers rewarded passive tower fire as "active play".
/// Kept as a no-op shell so old scene/component references do not crash; HUD and combat no longer use it.
/// </summary>
public sealed class ComboSystem : Component
{
	public static ComboSystem Instance { get; private set; }

	public int Streak => 0;
	public float Multiplier => 1f;
	public float WindowFraction => 0f;

	protected override void OnAwake() => Instance = this;

	protected override void OnDestroy()
	{
		if ( Instance == this ) Instance = null;
	}

	public void RegisterKill() { }

	public void ResetCombo() { }
}
