using Dynasty.Core.Enums;
using Dynasty.Data;

namespace Dynasty.Systems.Formation;

/// <summary>
/// Cached formation layouts. Add new presets here (Nickel, Dime, etc.) without touching UI.
/// </summary>
public static class FormationLayoutRegistry
{
	static readonly Dictionary<FormationType, FormationLayout> Layouts = BuildLayouts();

	public static FormationLayout Get( FormationType type )
		=> Layouts.TryGetValue( type, out var layout ) ? layout : Layouts[FormationType.Offense11];

	public static IReadOnlyList<FormationLayout> GetForSide( FormationSide side )
		=> Layouts.Values.Where( l => l.Side == side ).OrderBy( l => l.DisplayName ).ToList();

	public static FormationType GetDefaultForSide( FormationSide side ) => side switch
	{
		FormationSide.Offense => FormationType.Offense11,
		FormationSide.Defense => FormationType.Defense43,
		FormationSide.SpecialTeams => FormationType.SpecialTeams,
		_ => FormationType.Offense11
	};

	public static FormationLayout GetSpecialTeams() => Get( FormationType.SpecialTeams );

	public static IReadOnlyList<Position> GetEligiblePositions( string slotKey )
	{
		if ( string.IsNullOrWhiteSpace( slotKey ) )
			return Array.Empty<Position>();

		foreach ( var layout in Layouts.Values )
		{
			var slot = layout.Slots.FirstOrDefault( s => s.SlotKey.Equals( slotKey, StringComparison.OrdinalIgnoreCase ) );
			if ( slot != null )
				return slot.EligiblePositions;
		}

		return Array.Empty<Position>();
	}

	public static IReadOnlyList<FormationSlot> GetAllStarterSlots()
	{
		var offense = Get( FormationType.Offense11 );
		var defense = Get( FormationType.Defense43 );
		var special = Get( FormationType.SpecialTeams );
		return offense.Slots.Concat( defense.Slots ).Concat( special.Slots ).ToList();
	}

	static Dictionary<FormationType, FormationLayout> BuildLayouts()
	{
		return new Dictionary<FormationType, FormationLayout>
		{
			[FormationType.Offense11] = BuildOffense11(),
			[FormationType.Defense43] = BuildDefense43(),
			[FormationType.Defense34] = BuildDefense34(),
			[FormationType.Nickel] = BuildNickel(),
			[FormationType.Dime] = BuildDime(),
			[FormationType.GoalLine] = BuildGoalLine(),
			[FormationType.SpecialTeams] = BuildSpecialTeams()
		};
	}

	static FormationLayout BuildOffense11() => new()
	{
		Type = FormationType.Offense11,
		Side = FormationSide.Offense,
		DisplayName = "11 Personnel",
		Slots =
		[
			Slot( "LT", "LT", 0.32f, 0.50f, Position.OT ),
			Slot( "LG", "LG", 0.40f, 0.50f, Position.OG ),
			Slot( "C", "C", 0.50f, 0.50f, Position.C ),
			Slot( "RG", "RG", 0.60f, 0.50f, Position.OG ),
			Slot( "RT", "RT", 0.68f, 0.50f, Position.OT ),
			Slot( "TE", "TE", 0.76f, 0.50f, Position.TE ),
			Slot( "WR1", "WR1", 0.06f, 0.50f, Position.WR ),
			Slot( "WR2", "WR2", 0.18f, 0.36f, Position.WR ),
			Slot( "WR3", "WR3", 0.94f, 0.38f, Position.WR ),
			Slot( "QB", "QB", 0.52f, 0.72f, Position.QB ),
			Slot( "RB", "RB", 0.36f, 0.86f, Position.RB, Position.FB ),
			OptionalSlot( "FB", "FB", 0.66f, 0.80f, Position.FB, Position.RB )
		]
	};

	static FormationLayout BuildDefense43() => new()
	{
		Type = FormationType.Defense43,
		Side = FormationSide.Defense,
		DisplayName = "4-3 Base",
		Slots =
		[
			Slot( "LE", "LE", 0.16f, 0.44f, Position.DE, Position.DT ),
			Slot( "DT1", "DT1", 0.38f, 0.44f, Position.DT, Position.DE ),
			Slot( "DT2", "DT2", 0.62f, 0.44f, Position.DT, Position.DE ),
			Slot( "RE", "RE", 0.84f, 0.44f, Position.DE, Position.DT ),
			Slot( "LOLB", "LOLB", 0.24f, 0.60f, Position.LB ),
			Slot( "MLB", "MLB", 0.50f, 0.60f, Position.LB ),
			Slot( "ROLB", "ROLB", 0.76f, 0.60f, Position.LB ),
			Slot( "CB1", "CB1", 0.08f, 0.32f, Position.CB ),
			Slot( "CB2", "CB2", 0.92f, 0.32f, Position.CB ),
			Slot( "FS", "FS", 0.36f, 0.14f, Position.S ),
			Slot( "SS", "SS", 0.64f, 0.18f, Position.S )
		]
	};

	static FormationLayout BuildDefense34() => new()
	{
		Type = FormationType.Defense34,
		Side = FormationSide.Defense,
		DisplayName = "3-4 Base",
		Slots =
		[
			Slot( "LE", "LE", 0.20f, 0.44f, Position.DE, Position.DT ),
			Slot( "DT1", "NT", 0.50f, 0.44f, Position.DT, Position.DE ),
			Slot( "RE", "RE", 0.80f, 0.44f, Position.DE, Position.DT ),
			Slot( "LOLB", "LOLB", 0.18f, 0.62f, Position.LB ),
			Slot( "MLB", "MLB", 0.50f, 0.62f, Position.LB ),
			Slot( "ROLB", "ROLB", 0.82f, 0.62f, Position.LB ),
			Slot( "CB1", "CB1", 0.08f, 0.32f, Position.CB ),
			Slot( "CB2", "CB2", 0.92f, 0.32f, Position.CB ),
			Slot( "FS", "FS", 0.36f, 0.14f, Position.S ),
			Slot( "SS", "SS", 0.64f, 0.18f, Position.S )
		]
	};

	static FormationLayout BuildNickel() => new()
	{
		Type = FormationType.Nickel,
		Side = FormationSide.Defense,
		DisplayName = "Nickel",
		Slots =
		[
			Slot( "LE", "LE", 0.18f, 0.44f, Position.DE, Position.DT ),
			Slot( "DT1", "DT", 0.42f, 0.44f, Position.DT, Position.DE ),
			Slot( "RE", "RE", 0.78f, 0.44f, Position.DE, Position.DT ),
			Slot( "LOLB", "WILL", 0.28f, 0.58f, Position.LB ),
			Slot( "MLB", "MIKE", 0.50f, 0.58f, Position.LB ),
			Slot( "ROLB", "SAM", 0.72f, 0.58f, Position.LB ),
			Slot( "CB1", "CB1", 0.08f, 0.30f, Position.CB ),
			Slot( "CB2", "CB2", 0.92f, 0.30f, Position.CB ),
			Slot( "NB", "NICKEL", 0.50f, 0.42f, Position.CB, Position.S, Position.LB ),
			Slot( "FS", "FS", 0.34f, 0.14f, Position.S ),
			Slot( "SS", "SS", 0.66f, 0.18f, Position.S )
		]
	};

	static FormationLayout BuildDime() => new()
	{
		Type = FormationType.Dime,
		Side = FormationSide.Defense,
		DisplayName = "Dime",
		Slots =
		[
			Slot( "LE", "LE", 0.22f, 0.44f, Position.DE, Position.DT ),
			Slot( "RE", "RE", 0.78f, 0.44f, Position.DE, Position.DT ),
			Slot( "MLB", "MLB", 0.50f, 0.56f, Position.LB ),
			Slot( "CB1", "CB1", 0.08f, 0.30f, Position.CB ),
			Slot( "CB2", "CB2", 0.92f, 0.30f, Position.CB ),
			Slot( "NB", "NICKEL", 0.30f, 0.40f, Position.CB, Position.S ),
			Slot( "DIME1", "DIME", 0.50f, 0.36f, Position.CB, Position.S ),
			Slot( "DIME2", "DIME", 0.70f, 0.40f, Position.CB, Position.S ),
			Slot( "FS", "FS", 0.36f, 0.14f, Position.S ),
			Slot( "SS", "SS", 0.64f, 0.18f, Position.S )
		]
	};

	static FormationLayout BuildSpecialTeams() => new()
	{
		Type = FormationType.SpecialTeams,
		Side = FormationSide.SpecialTeams,
		DisplayName = "Special Teams",
		Slots =
		[
			Slot( "K", "K", 0.35f, 0.45f, Position.K ),
			Slot( "P", "P", 0.65f, 0.45f, Position.P )
		]
	};

	static FormationLayout BuildGoalLine() => new()
	{
		Type = FormationType.GoalLine,
		Side = FormationSide.Offense,
		DisplayName = "Goal Line",
		Slots =
		[
			Slot( "LT", "LT", 0.24f, 0.50f, Position.OT ),
			Slot( "LG", "LG", 0.36f, 0.50f, Position.OG ),
			Slot( "C", "C", 0.50f, 0.50f, Position.C ),
			Slot( "RG", "RG", 0.64f, 0.50f, Position.OG ),
			Slot( "RT", "RT", 0.76f, 0.50f, Position.OT ),
			Slot( "TE", "TE", 0.84f, 0.50f, Position.TE ),
			Slot( "WR1", "WR", 0.08f, 0.50f, Position.WR ),
			Slot( "WR2", "WR", 0.92f, 0.50f, Position.WR ),
			Slot( "QB", "QB", 0.50f, 0.72f, Position.QB ),
			Slot( "RB", "RB", 0.42f, 0.86f, Position.RB, Position.FB ),
			OptionalSlot( "FB", "FB", 0.58f, 0.82f, Position.FB, Position.RB )
		]
	};

	static FormationSlot Slot( string key, string label, float x, float y, params Position[] positions )
		=> new()
		{
			SlotKey = key,
			DisplayLabel = label,
			NormalizedX = x,
			NormalizedY = y,
			EligiblePositions = positions,
			IsOptional = false
		};

	static FormationSlot OptionalSlot( string key, string label, float x, float y, params Position[] positions )
		=> new()
		{
			SlotKey = key,
			DisplayLabel = label,
			NormalizedX = x,
			NormalizedY = y,
			EligiblePositions = positions,
			IsOptional = true
		};
}
