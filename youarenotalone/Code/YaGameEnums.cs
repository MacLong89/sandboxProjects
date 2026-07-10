namespace Sandbox;

/// <summary>High-level match phase (replicated from host).</summary>
public enum YaGameState
{
	Lobby,
	Intermission,
	InRound,
	/// <summary>Brief win presentation after elimination (before intermission or practice picker).</summary>
	RoundVictory
}

/// <summary>Asymmetrical role for the current round (replicated per pawn from host).</summary>
public enum YaPlayerRole
{
	Unassigned,
	Alone,
	NotAlone
}

/// <summary>Authoritative reason a round ended (for UI / analytics hooks).</summary>
public enum YaRoundEndReason
{
	None,
	AloneEliminated,
	AllNotAloneEliminated,
	TimeExpired,
	Aborted
}
