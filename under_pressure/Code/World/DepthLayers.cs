namespace UnderPressure;

/// <summary>
/// Mild world / local Z offsets so coplanar meshes don't flicker.
/// Prefer a ±1 unit nudge — do not invent large "floating" bands.
/// </summary>
public static class DepthLayers
{
	/// <summary>Small nudge between stacked flat faces (also used for façade −Y steps).</summary>
	public const float Step = 1f;

	// --- Terrain (world Z) ---
	public const float MapField = -8f;
	public const float MapTransition = -4f;
	public const float PlayPad = 0f;

	// --- Job content (world Z) ---
	public const float PanelAbovePad = 2f;
	public const float PropAbovePad = 1f;
	public const float RoadAbovePad = 1.5f;
	public const float RoadMarking = 2.5f;
	public const float PerimeterDecal = 1f;
	public const float DecorSitOnPad = 0.05f;

	// --- CleanableSurface (local Z on the panel root) ---
	public const float CleanBase = -1f;
	public const float UnderlayLayer = 0.25f;
	public const float GrimeTop = 2f;
	public const float GrimeStep = 1.5f;

	// --- Scenery face depth (local −Y) ---
	public const float FaceBase = 3f;

	public static Vector3 LiftPanel( Vector3 authored, Angles rotation )
	{
		if ( !IsFlat( rotation ) )
			return authored;

		var z = MathF.Max( authored.z, PanelAbovePad );
		return authored.WithZ( z );
	}

	public static Vector3 LiftProp( Vector3 authored, Angles rotation, Vector3 size = default )
	{
		var pos = IsFlat( rotation )
			? authored.WithZ( MathF.Max( authored.z, PropAbovePad ) )
			: authored;

		// Thin floor decals sit +1 above the wash panel band.
		if ( IsFlat( rotation ) && size.z > 0f && size.z <= 4f )
			pos = pos.WithZ( MathF.Max( pos.z, PanelAbovePad + Step ) );

		if ( !IsFlat( rotation ) && size.y > 0f && size.y <= 48f )
			pos += rotation.ToRotation().Backward * Step;

		return pos;
	}

	public static float NextFaceDepth( ref int slot ) => FaceBase + Step * slot++;

	public static float GrimeLayerZ( int layerFromTop, int layerCount ) =>
		GrimeTop + (layerCount - 1 - layerFromTop) * GrimeStep;

	public static bool IsFlat( Angles rotation ) =>
		MathF.Abs( rotation.pitch ) < 0.01f && MathF.Abs( rotation.roll ) < 0.01f;
}
