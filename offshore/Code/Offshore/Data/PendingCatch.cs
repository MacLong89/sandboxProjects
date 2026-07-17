namespace Offshore;

/// <summary>Pending catch awaiting KEEP / RELEASE on the New Catch screen.</summary>
public sealed class PendingCatch
{
	public CatchRecord Record { get; set; }
	public FishDefinition Definition { get; set; }
	public float PreviousBestWeight { get; set; }
	public bool IsPersonalBest { get; set; }
	public bool CoolerWouldOverflow { get; set; }
	public string SpritePath { get; set; } = "";
}
