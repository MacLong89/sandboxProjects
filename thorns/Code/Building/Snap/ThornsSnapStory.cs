namespace Sandbox;

/// <summary>Snap storey band helpers — vertical module equals <see cref="ThornsBuildingModule.Cell"/> (THORNS document structure tier height bands).</summary>
public static class ThornsSnapStory
{
	static float Cs => ThornsBuildingModule.Cell;
	static float Ft => ThornsBuildingModule.FloorThickness;

	public static int KzBandFromElevation( float foundationSlabCentreWorldZ ) =>
		(int)MathF.Floor( (foundationSlabCentreWorldZ - Ft * 0.5f) / Cs );
}
