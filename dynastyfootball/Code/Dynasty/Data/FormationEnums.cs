namespace Dynasty.Data;

public enum FormationSide
{
	Offense,
	Defense,
	SpecialTeams
}

/// <summary>
/// Formation presets. Registry can add Nickel, Dime, GoalLine, etc. without UI changes.
/// </summary>
public enum FormationType
{
	Offense11,
	Defense43,
	Defense34,
	Nickel,
	Dime,
	GoalLine,
	SpecialTeams
}
