namespace SceneLab;

/// <summary>
/// Owns the junction asphalt. Sidewalks stay in the four outside corners only —
/// never as strips across the driving lanes.
/// Pass the same <paramref name="junctionHalf"/> used by <see cref="RoadNetwork.BuildAxisSegments"/>.
/// </summary>
public static class IntersectionPiece
{
	/// <summary>Extra asphalt past the corridor cut so ground never shows at seams.</summary>
	public const float DeckPad = 12f;

	public static GameObject Build( GameObject parent, Vector3 localPos, RoadCorridorPiece.Spec road, float junctionHalf )
	{
		var root = new GameObject( parent, true, PieceIds.Intersection );
		root.LocalPosition = localPos;

		var roadW = road.RoadWidth;
		var walkW = road.SidewalkWidth;
		var roadTop = RoadCorridorPiece.RoadTopZ( road );
		var walkTop = roadTop + Depth.Step * 2f;
		var roadH = road.RoadThickness;
		var walkH = road.SidewalkThickness;

		// Priority asphalt covers the full corridor cutout (+ pad). Slightly above corridor road.
		var deckHalf = junctionHalf + DeckPad;
		var deckSize = deckHalf * 2f;
		var deckTop = roadTop + Depth.Step;

		KitBox.Box( root, "Deck",
			new Vector3( 0f, 0f, deckTop - roadH * 0.5f ),
			new Vector3( deckSize, deckSize, roadH ),
			Palette.Asphalt );

		KitBox.Box( root, "MarkX",
			new Vector3( 0f, 0f, deckTop + Depth.Step + 0.75f ),
			new Vector3( roadW * 0.45f, 5f, 1.5f ),
			Palette.LaneMark );
		KitBox.Box( root, "MarkY",
			new Vector3( 0f, 0f, deckTop + Depth.Step + 0.75f ),
			new Vector3( 5f, roadW * 0.45f, 1.5f ),
			Palette.LaneMark );

		// Corner sidewalk pads only — outside both road corridors (the four yard quadrants).
		var corner = roadW * 0.5f + walkW * 0.5f;
		foreach ( var sx in new[] { -1f, 1f } )
		foreach ( var sy in new[] { -1f, 1f } )
		{
			KitBox.Box( root, "WalkCorner",
				new Vector3( sx * corner, sy * corner, walkTop + Depth.Step - walkH * 0.5f ),
				new Vector3( walkW, walkW, walkH ),
				Palette.Sidewalk );

			// Short curb on the two road-facing edges of each corner (not across the street).
			var curbH = road.CurbHeight;
			var curbTop = deckTop + Depth.Step;
			var curbLen = walkW * 0.85f;
			KitBox.Box( root, "CurbX",
				new Vector3( sx * (roadW * 0.5f + 4f), sy * corner, curbTop - curbH * 0.5f ),
				new Vector3( 8f, curbLen, curbH ),
				Palette.Curb );
			KitBox.Box( root, "CurbY",
				new Vector3( sx * corner, sy * (roadW * 0.5f + 4f), curbTop - curbH * 0.5f ),
				new Vector3( curbLen, 8f, curbH ),
				Palette.Curb );
		}

		return root;
	}
}
