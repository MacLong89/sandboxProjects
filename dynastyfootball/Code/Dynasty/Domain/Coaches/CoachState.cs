using Dynasty.Core.Enums;
using Dynasty.Core.Identifiers;

namespace Dynasty.Domain.Coaches;

public sealed class CoachState
{
	public CoachId Id { get; set; }
	public string FirstName { get; set; } = "";
	public string LastName { get; set; } = "";
	public int Age { get; set; }
	public CoachRole Role { get; set; }
	public TeamId TeamId { get; set; }
	public CoachRatings Ratings { get; set; } = new();
	public CoachTendencies Tendencies { get; set; } = new();
	public CoachContract Contract { get; set; } = new();

	public string FullName => $"{FirstName} {LastName}".Trim();
}

public sealed class CoachRatings
{
	public int Overall { get; set; }
	public int Development { get; set; }
	public int GamePlanning { get; set; }
	public int Motivation { get; set; }
	public int Scouting { get; set; }
}

public sealed class CoachTendencies
{
	public int RunPassBalance { get; set; } = 50;
	public int Aggressiveness { get; set; } = 50;
	public int RiskTolerance { get; set; } = 50;
	public int YouthPreference { get; set; } = 50;
}

public sealed class CoachContract
{
	public int YearsRemaining { get; set; }
	public int AnnualSalary { get; set; }
}
