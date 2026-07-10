using Dynasty.Core.Enums;

namespace Dynasty.Core.Attributes;

/// <summary>
/// Data-driven attribute keys per position. Extend via definitions without code changes.
/// </summary>
public static class PlayerAttributeKeys
{
	public static readonly IReadOnlyDictionary<Position, string[]> ByPosition = new Dictionary<Position, string[]>
	{
		[Position.QB] = ["accuracy", "arm_strength", "awareness", "mobility", "throw_under_pressure"],
		[Position.RB] = ["speed", "vision", "elusiveness", "catching", "pass_blocking"],
		[Position.FB] = ["speed", "vision", "elusiveness", "catching", "pass_blocking"],
		[Position.WR] = ["speed", "route_running", "catching", "release", "jump_ball"],
		[Position.TE] = ["speed", "route_running", "catching", "blocking", "strength"],
		[Position.OT] = ["pass_blocking", "run_blocking", "strength", "footwork", "awareness"],
		[Position.OG] = ["pass_blocking", "run_blocking", "strength", "footwork", "awareness"],
		[Position.C] = ["pass_blocking", "run_blocking", "strength", "footwork", "awareness"],
		[Position.DE] = ["pass_rush", "run_defense", "strength", "speed", "awareness"],
		[Position.DT] = ["pass_rush", "run_defense", "strength", "speed", "awareness"],
		[Position.LB] = ["tackling", "coverage", "pass_rush", "speed", "awareness"],
		[Position.CB] = ["man_coverage", "zone_coverage", "speed", "ball_skills", "tackling"],
		[Position.S] = ["man_coverage", "zone_coverage", "speed", "ball_skills", "tackling"],
		[Position.K] = ["accuracy", "power", "clutch", "consistency"],
		[Position.P] = ["accuracy", "power", "hangtime", "consistency"],
		[Position.LS] = ["accuracy", "consistency", "awareness"]
	};

	public static PositionGroup GetGroup( Position position ) => position switch
	{
		Position.QB => PositionGroup.Quarterback,
		Position.RB or Position.FB => PositionGroup.RunningBack,
		Position.WR => PositionGroup.WideReceiver,
		Position.TE => PositionGroup.TightEnd,
		Position.OT or Position.OG or Position.C => PositionGroup.OffensiveLine,
		Position.DE or Position.DT => PositionGroup.DefensiveLine,
		Position.LB => PositionGroup.Linebacker,
		Position.CB or Position.S => PositionGroup.DefensiveBack,
		_ => PositionGroup.SpecialTeams
	};
}
