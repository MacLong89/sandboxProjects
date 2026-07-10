using Dynasty.Core.Attributes;
using Dynasty.Core.Enums;

namespace Dynasty.Core.Stats;

public static class PlayerStatKeys
{
	public const string Games = "games";
	public const string Snaps = "snaps";

	public const string PassYds = "pass_yds";
	public const string PassTd = "pass_td";
	public const string PassInt = "pass_int";
	public const string Comp = "comp";
	public const string Att = "att";

	public const string RushYds = "rush_yds";
	public const string RushTd = "rush_td";
	public const string RushAtt = "rush_att";

	public const string Rec = "rec";
	public const string RecYds = "rec_yds";
	public const string RecTd = "rec_td";
	public const string Targets = "targets";

	public const string Tackles = "tackles";
	public const string Sacks = "sacks";
	public const string Int = "def_int";
	public const string Tfl = "tfl";

	public const string FgMade = "fg_made";
	public const string FgAtt = "fg_att";
	public const string XpMade = "xp_made";

	public static readonly string[] OffensiveSkill =
	[
		Games, PassYds, PassTd, PassInt, Comp, Att,
		RushYds, RushTd, RushAtt,
		Rec, RecYds, RecTd, Targets
	];

	public static readonly string[] DefensiveSkill =
	[
		Games, Tackles, Sacks, Int, Tfl
	];

	public static readonly string[] KickerSkill = [Games, FgMade, FgAtt, XpMade];

	public static string[] ForPosition( Position position )
	{
		return PlayerAttributeKeys.GetGroup( position ) switch
		{
			PositionGroup.Quarterback => [Games, Att, Comp, PassYds, PassTd, PassInt, RushAtt, RushYds, RushTd],
			PositionGroup.RunningBack => [Games, RushAtt, RushYds, RushTd, Targets, Rec, RecYds, RecTd],
			PositionGroup.WideReceiver or PositionGroup.TightEnd => [Games, Targets, Rec, RecYds, RecTd, RushAtt, RushYds],
			PositionGroup.OffensiveLine => [Games, Snaps],
			PositionGroup.DefensiveLine or PositionGroup.Linebacker or PositionGroup.DefensiveBack =>
				[Games, Tackles, Sacks, Tfl, Int],
			PositionGroup.SpecialTeams when position == Position.K => [Games, FgMade, FgAtt, XpMade],
			PositionGroup.SpecialTeams => [Games, Snaps],
			_ => [Games]
		};
	}

	public static string FormatLabel( string key ) => key switch
	{
		PassYds => "Pass Yds",
		PassTd => "Pass TD",
		PassInt => "INT",
		Comp => "Comp",
		Att => "Att",
		RushYds => "Rush Yds",
		RushTd => "Rush TD",
		RushAtt => "Rush Att",
		Rec => "Rec",
		RecYds => "Rec Yds",
		RecTd => "Rec TD",
		Targets => "Tgt",
		Tackles => "Tackles",
		Sacks => "Sacks",
		Int => "INT",
		Tfl => "TFL",
		FgMade => "FGM",
		FgAtt => "FGA",
		XpMade => "XPM",
		Games => "Games",
		Snaps => "Snaps",
		_ => key.Replace( '_', ' ' )
	};
}
