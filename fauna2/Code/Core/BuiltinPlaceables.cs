namespace Fauna2;

/// <summary>Guaranteed minimum buildables when compiled .place assets are missing or incomplete.</summary>
public static class BuiltinPlaceables
{
	private static readonly PlaceableDefinition EntranceDef = CreateEntrance();
	private static readonly PlaceableDefinition PathStraightDef = CreatePathStraight();

	public static bool IsCorePath( string id ) =>
		string.Equals( id, "entrance", StringComparison.OrdinalIgnoreCase )
		|| string.Equals( id, "path_straight", StringComparison.OrdinalIgnoreCase );

	public static PlaceableDefinition Get( string id )
	{
		if ( string.Equals( id, "entrance", StringComparison.OrdinalIgnoreCase ) )
			return EntranceDef;

		if ( string.Equals( id, "path_straight", StringComparison.OrdinalIgnoreCase ) )
			return PathStraightDef;

		return null;
	}

	public static IEnumerable<(string Id, PlaceableDefinition Def)> All()
	{
		yield return ("entrance", EntranceDef);
		yield return ("path_straight", PathStraightDef);
	}

	private static PlaceableDefinition CreateEntrance() => new()
	{
		DisplayName = "Zoo Entrance",
		Description = "Where guests arrive. Paths must connect here before visitors can enter your zoo.",
		Category = BuildCategory.Paths,
		Cost = 500,
		UnlockLevel = 0,
		AppealBonus = 8,
		Footprint = GameConstants.EntranceFootprint,
		GridSnap = 64,
		RotationStep = 90,
		Visuals = new List<VisualPart>
		{
			new() { Model = "models/dev/box.vmdl", Offset = new Vector3( 0, 0, 2 ), Scale = new Vector3( 5.12f, 2.56f, 0.08f ), Tint = new Color( 0.68f, 0.64f, 0.58f ) },
			new() { Model = "models/dev/box.vmdl", Offset = new Vector3( -96, 0, 48 ), Scale = new Vector3( 0.5f, 0.5f, 1.8f ), Tint = new Color( 0.82f, 0.58f, 0.38f ) },
			new() { Model = "models/dev/box.vmdl", Offset = new Vector3( 96, 0, 48 ), Scale = new Vector3( 0.5f, 0.5f, 1.8f ), Tint = new Color( 0.82f, 0.58f, 0.38f ) },
		}
	};

	private static PlaceableDefinition CreatePathStraight() => new()
	{
		DisplayName = "Stone Path",
		Description = "Guides guests through your zoo. Guests linger near paths.",
		Category = BuildCategory.Paths,
		Cost = 40,
		UnlockLevel = 0,
		AppealBonus = 0.2f,
		Footprint = new Vector2( 64, 64 ),
		GridSnap = 64,
		RotationStep = 90,
		Visuals = new List<VisualPart>
		{
			new() { Model = "models/dev/box.vmdl", Offset = new Vector3( 0, 0, 1.5f ), Scale = new Vector3( 2.56f, 2.56f, 0.06f ), Tint = new Color( 0.74f, 0.70f, 0.58f ) },
		}
	};
}
