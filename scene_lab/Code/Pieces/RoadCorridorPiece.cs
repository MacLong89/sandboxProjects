namespace SceneLab;

/// <summary>
/// Street corridor: asphalt, sidewalks, curbs, grass banks with explicit Z stacking (no coplanar tops).
/// Local +X = along road, +Y = across, +Z = up.
/// </summary>
public static class RoadCorridorPiece
{
	public sealed class Spec
	{
		public float Length = CityScale.StreetHalfLen;
		public float RoadWidth = CityScale.RoadWidth;
		public float SidewalkWidth = CityScale.SidewalkWidth;
		public float EmbankmentWidth = CityScale.EmbankmentWidth;
		public float RoadThickness = 6f;
		public float SidewalkThickness = 6f;
		public float CurbHeight = 8f;
		public float EmbankmentThickness = 6f;
		public bool CenterLine = true;
		/// <summary>Extra world-Z lift for the whole corridor (cross streets use Depth.CrossStreetLift).</summary>
		public float BaseLift = 0f;
		/// <summary>When false, skip grass banks (useful for short connectors).</summary>
		public bool BuildEmbankments = true;
	}

	public static GameObject Build( GameObject parent, Vector3 worldPos, float yaw, Spec spec = null )
	{
		spec ??= new Spec();
		var root = new GameObject( parent, true, PieceIds.RoadCorridor );
		root.LocalPosition = worldPos.WithZ( worldPos.z + spec.BaseLift );
		root.LocalRotation = Rotation.FromYaw( yaw );

		var len = spec.Length;
		var roadW = spec.RoadWidth;
		var walkW = spec.SidewalkWidth;
		var bankW = spec.EmbankmentWidth;

		// Surface tops (absolute local Z) — each band owns its own plane
		var roadTop = spec.RoadThickness;
		var curbTop = roadTop + Depth.Step;
		var walkTop = curbTop + Depth.Step;
		var bankTop = walkTop + Depth.Step;
		var lipTop = bankTop + Depth.Step;
		KitBox.Box( root, "Road",
			new Vector3( 0f, 0f, roadTop - spec.RoadThickness * 0.5f ),
			new Vector3( len, roadW, spec.RoadThickness ),
			Palette.Asphalt );

		if ( spec.CenterLine )
		{
			// Fully above road top — never share the asphalt plane
			const float lineH = 1.5f;
			KitBox.Box( root, "CenterLine",
				new Vector3( 0f, 0f, roadTop + Depth.Step + lineH * 0.5f ),
				new Vector3( len * 0.92f, 6f, lineH ),
				Palette.LaneMark );
		}

		PlaceSide( root, +1f, len, roadW, walkW, bankW, spec, curbTop, walkTop, bankTop, lipTop );
		PlaceSide( root, -1f, len, roadW, walkW, bankW, spec, curbTop, walkTop, bankTop, lipTop );

		return root;
	}

	private static void PlaceSide(
		GameObject root,
		float sideSign,
		float len,
		float roadW,
		float walkW,
		float bankW,
		Spec spec,
		float curbTop,
		float walkTop,
		float bankTop,
		float lipTop )
	{
		var side = sideSign >= 0 ? "Pos" : "Neg";
		var walkCenterY = sideSign * (roadW * 0.5f + walkW * 0.5f);
		var curbCenterY = sideSign * (roadW * 0.5f + 4f);
		var bankCenterY = sideSign * (roadW * 0.5f + walkW + bankW * 0.5f);

		KitBox.Box( root, $"Curb_{side}",
			new Vector3( 0f, curbCenterY, curbTop - spec.CurbHeight * 0.5f ),
			new Vector3( len, 8f, spec.CurbHeight ),
			Palette.Curb );

		KitBox.Box( root, $"Sidewalk_{side}",
			new Vector3( 0f, walkCenterY, walkTop - spec.SidewalkThickness * 0.5f ),
			new Vector3( len, walkW, spec.SidewalkThickness ),
			Palette.Sidewalk );

		if ( !spec.BuildEmbankments )
			return;

		KitBox.Box( root, $"Embankment_{side}",
			new Vector3( 0f, bankCenterY, bankTop - spec.EmbankmentThickness * 0.5f ),
			new Vector3( len, bankW, spec.EmbankmentThickness ),
			Palette.Grass );

		var lipY = sideSign * (roadW * 0.5f + walkW + bankW - 6f);
		KitBox.Box( root, $"BankLip_{side}",
			new Vector3( 0f, lipY, lipTop - 1.5f ),
			new Vector3( len, 10f, 3f ),
			Palette.GrassDark );
	}

	public static float SidewalkCenterY( Spec spec, float sideSign )
	{
		spec ??= new Spec();
		return sideSign * (spec.RoadWidth * 0.5f + spec.SidewalkWidth * 0.5f);
	}

	public static float EmbankmentCenterY( Spec spec, float sideSign )
	{
		spec ??= new Spec();
		return sideSign * (spec.RoadWidth * 0.5f + spec.SidewalkWidth + spec.EmbankmentWidth * 0.5f);
	}

	/// <summary>Local Z of embankment top (for yards / props sitting outside the bank).</summary>
	public static float EmbankmentTopZ( Spec spec )
	{
		spec ??= new Spec();
		// roadTop + Step(curb) + Step(walk) + Step(bank)
		return spec.RoadThickness + Depth.Step * 3f;
	}

	/// <summary>Local Z of road driving surface top.</summary>
	public static float RoadTopZ( Spec spec ) => (spec ?? new Spec()).RoadThickness;
}
