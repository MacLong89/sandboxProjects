namespace UnderPressure;

/// <summary>
/// World-space and local Z offsets so coplanar geometry never shares the same depth.
/// Every stacked surface should use the next band — minimum <see cref="Step"/> apart.
/// </summary>
public static class DepthLayers
{
	/// <summary>Minimum gap between surfaces that would otherwise be coplanar.</summary>
	public const float Step = 2f;

	// --- Terrain (world Z) ---
	public const float MapField = -8f;
	public const float MapTransition = -4f;
	public const float PlayPad = 0f;

	// --- Job content (world Z bumps on flat content) ---
	public const float PanelAbovePad = 2f;
	public const float PropAbovePad = 1f;
	public const float RoadAbovePad = 1.5f;
	public const float RoadMarking = 3.5f;
	public const float PerimeterDecal = 1f;
	public const float DecorSitOnPad = 0.05f;

	// --- CleanableSurface (local Z on the panel root) ---
	// Top face should sit on the play pad but stay below grime layers (GrimeTop = 2).
	public const float CleanBase = -1f;
	/// <summary>Etched secrets, graffiti, and symbols — always below every cleanable grime/film layer.</summary>
	public const float UnderlayLayer = 0.25f;
	public const float GrimeTop = 2f;
	public const float GrimeStep = 1.5f;

	// --- Scenery face depth (local -Y toward the viewer on houses/buildings) ---
	public const float FaceBase = 3f;

	/// <summary>Lift a flat (ground) panel that was authored sitting on the pad.</summary>
	public static Vector3 LiftPanel( Vector3 authored, Angles rotation )
	{
		if ( !IsFlat( rotation ) )
			return authored;

		// Catalog positions use z≈1 for driveways; normalize to a consistent pad clearance.
		var z = MathF.Max( authored.z, PanelAbovePad );
		return authored.WithZ( z );
	}

	/// <summary>Nudge props off the ground plane and away from coplanar panel backs.</summary>
	public static Vector3 LiftProp( Vector3 authored, Angles rotation, Vector3 size = default )
	{
		var pos = IsFlat( rotation )
			? authored.WithZ( MathF.Max( authored.z, PropAbovePad ) )
			: authored;

		if ( !IsFlat( rotation ) && size.y > 0f && size.y <= 48f )
			pos += rotation.ToRotation().Backward * Step;

		return pos;
	}

	/// <summary>Step along a building's front-facing local -Y axis.</summary>
	public static float NextFaceDepth( ref int slot ) => FaceBase + Step * slot++;

	/// <summary>Local Z for the n'th grime layer (0 = top / cleaned first).</summary>
	public static float GrimeLayerZ( int layerFromTop, int layerCount ) =>
		GrimeTop + (layerCount - 1 - layerFromTop) * GrimeStep;

	public static bool IsFlat( Angles rotation ) =>
		MathF.Abs( rotation.pitch ) < 0.01f && MathF.Abs( rotation.roll ) < 0.01f;
}
