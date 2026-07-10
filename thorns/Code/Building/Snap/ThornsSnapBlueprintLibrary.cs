namespace Sandbox;

/// <summary>Snap geometry for dev-box-aligned pieces (<see cref="ThornsBuildingModule"/>). No rounding at runtime — authored constants only.</summary>
public static class ThornsSnapBlueprintLibrary
{
	static float Hh => ThornsBuildingModule.Cell * 0.5f;

	static Vector3 FwN => new( 0f, 1f, 0f );
	static Vector3 FwE => new( 1f, 0f, 0f );
	static Vector3 FwS => new( 0f, -1f, 0f );
	static Vector3 FwW => new( -1f, 0f, 0f );

	static float Wh => ThornsBuildingModule.WallHeight;

	static float Ft => ThornsBuildingModule.FloorThickness;

	static HashSet<ThornsSnapChannel> Mk( params ThornsSnapChannel[] channels )
	{
		var h = new HashSet<ThornsSnapChannel>();
		foreach ( var c in channels )
			h.Add( c );

		return h;
	}

	static Vector3 WallTopCentreSocketLocal =>
		new( 0f, 0f, Wh * 0.5f );

	public static ThornsSnapSocketBlueprint[] GetSocketsFor( string defId ) =>
		defId switch
		{
			"wood_foundation" => new ThornsSnapSocketBlueprint[]
			{
				new( 0, new Vector3( 0f, Hh, 0f ), FwN, Mk( ThornsSnapChannel.FoundationEdgeMate,
					ThornsSnapChannel.WallSeatOnFoundationEdge ) ),
				new( 1, new Vector3( Hh, 0f, 0f ), FwE, Mk( ThornsSnapChannel.FoundationEdgeMate,
					ThornsSnapChannel.WallSeatOnFoundationEdge ) ),
				new( 2, new Vector3( 0f, -Hh, 0f ), FwS, Mk( ThornsSnapChannel.FoundationEdgeMate,
					ThornsSnapChannel.WallSeatOnFoundationEdge ) ),
				new( 3, new Vector3( -Hh, 0f, 0f ), FwW, Mk( ThornsSnapChannel.FoundationEdgeMate,
					ThornsSnapChannel.WallSeatOnFoundationEdge ) ),
				new( 4, new Vector3( 0f, 0f, Ft * 0.5f ), FwN,
					Mk( ThornsSnapChannel.RampSeatOnFoundationTop ) ),
			},
			"wood_wall" => new ThornsSnapSocketBlueprint[]
			{
				new( 0, WallTopCentreSocketLocal, FwN, Mk( ThornsSnapChannel.FloorSeatOnWallTop ) ),
			},
			"wood_window" => new ThornsSnapSocketBlueprint[]
			{
				new( 0, WallTopCentreSocketLocal, FwN, Mk( ThornsSnapChannel.FloorSeatOnWallTop ) ),
			},
			"wood_ramp" => Array.Empty<ThornsSnapSocketBlueprint>(),
			"wood_doorframe" =>
				new ThornsSnapSocketBlueprint[]
				{
					new( 0,
						new Vector3( 0f, 0f, ThornsBuildingModule.WallHeight * 0.5f ),
						FwS,
						Mk( ThornsSnapChannel.DoorPanelIntoFrame ) ),
					new( 1, WallTopCentreSocketLocal, FwN, Mk( ThornsSnapChannel.FloorSeatOnWallTop ) ),
				},
			_ => Array.Empty<ThornsSnapSocketBlueprint>()
		};

	public static ThornsSnapPlugBlueprint[] GetPlugsFor( string defId ) =>
		defId switch
		{
			"wood_foundation" => new ThornsSnapPlugBlueprint[]
			{
				new( 0, ThornsSnapChannel.FoundationEdgeMate ),
				new( 1, ThornsSnapChannel.FoundationEdgeMate ),
				new( 2, ThornsSnapChannel.FoundationEdgeMate ),
				new( 3, ThornsSnapChannel.FoundationEdgeMate ),
				new( 4, ThornsSnapChannel.FloorSeatOnWallTop ),
			},
			"wood_wall" =>
				new ThornsSnapPlugBlueprint[] { new( 0, ThornsSnapChannel.WallSeatOnFoundationEdge ), },
			"wood_window" =>
				new ThornsSnapPlugBlueprint[] { new( 0, ThornsSnapChannel.WallSeatOnFoundationEdge ), },
			"wood_ramp" =>
				new ThornsSnapPlugBlueprint[] { new( 0, ThornsSnapChannel.RampSeatOnFoundationTop ), },
			"wood_doorframe" =>
				new ThornsSnapPlugBlueprint[] { new( 0, ThornsSnapChannel.WallSeatOnFoundationEdge ), },
			"wood_door" =>
				new ThornsSnapPlugBlueprint[] { new( 0, ThornsSnapChannel.DoorPanelIntoFrame ), },
			_ => Array.Empty<ThornsSnapPlugBlueprint>()
		};
}
