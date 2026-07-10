namespace ThinkDrink.Studio;

/// <summary>Marks a world-panel mount as Team A or Team B (for optional team score displays).</summary>
public sealed class TeamScoreSide : Component
{
	[Property] public bool IsTeamA { get; set; } = true;
}
