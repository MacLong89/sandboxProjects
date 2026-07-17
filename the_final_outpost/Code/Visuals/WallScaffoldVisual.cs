namespace FinalOutpost;

/// <summary>
/// Symmetric timber frame + iron bars — looks the same inside and out, with clear see-through gaps.
/// </summary>
public static class WallScaffoldVisual
{
	static readonly Color WoodFallback = new( 0.55f, 0.42f, 0.28f );
	static readonly Color IronFallback = new( 0.38f, 0.4f, 0.44f );
	static readonly Color WoodTextured = Color.White;
	static readonly Color IronTextured = new( 0.72f, 0.74f, 0.78f );

	/// <param name="size">Full solid footprint the segment still occupies (length × thickness × height).</param>
	/// <param name="track">Called for each renderer so damage tints can track base colors.</param>
	/// <param name="wallCenter">World center — used to pick N/S vs E/W when the footprint is square.</param>
	public static void Build(
		GameObject root,
		Vector3 size,
		Action<ModelRenderer, Color> track,
		Vector3 wallCenter = default )
	{
		var alongX = RunsAlongX( wallCenter, size );

		var length = alongX ? size.x : size.y;
		var depth = alongX ? size.y : size.x;
		var height = size.z;

		var wood = StylizedMaterials.Wood;
		var stone = StylizedMaterials.Stone;

		var span = length * 1.02f;
		// Thin fence depth — centered so both faces match and gaps stay open.
		var frameDepth = MathF.Max( 8f, depth * 0.22f );
		var postW = MathF.Max( 7f, GameConstants.CellSize * 0.12f );
		var railH = MathF.Max( 6f, height * 0.05f );
		var postCount = Math.Max( 2, (int)MathF.Round( length / GameConstants.CellSize ) + 1 );

		void Part( Material mat, Vector3 localPos, Vector3 localSize, Color textured, Color fallback ) =>
			AddPart( root, alongX, mat, localPos, localSize, textured, fallback, track );

		var halfL = length * 0.5f;
		var halfH = height * 0.5f;
		var zBot = -halfH + railH * 0.5f;
		var zMid = 0f;
		var zTop = halfH - railH * 0.55f;

		// Three horizontal rails (kick / mid / top) — full span, centered on the wall midline.
		Part( wood, new Vector3( 0f, 0f, zBot ), new Vector3( span, frameDepth, railH ), WoodTextured, WoodFallback );
		Part( wood, new Vector3( 0f, 0f, zMid ), new Vector3( span, frameDepth * 0.9f, railH * 0.8f ), WoodTextured, WoodFallback );
		Part( wood, new Vector3( 0f, 0f, zTop ), new Vector3( span, frameDepth, railH ), WoodTextured, WoodFallback );

		// Vertical timber posts at cell edges.
		for ( var i = 0; i < postCount; i++ )
		{
			var t = postCount == 1 ? 0.5f : i / (float)(postCount - 1);
			var x = -halfL + length * t;
			Part( wood, new Vector3( x, 0f, 0f ), new Vector3( postW, frameDepth * 1.1f, height * 0.98f ), WoodTextured, WoodFallback );
		}

		// Iron bars fill each bay — see-through from both sides (no solid backer).
		var picketGap = MathF.Max( 8f, GameConstants.CellSize * 0.14f );
		var picketW = MathF.Max( 2.2f, depth * 0.04f );
		var picketH = height * 0.78f;
		var picketZ = -halfH + railH + picketH * 0.5f + 1f;
		var margin = postW * 0.75f;

		for ( var x = -halfL + margin + picketGap * 0.5f; x <= halfL - margin; x += picketGap )
		{
			var onPost = false;
			for ( var i = 0; i < postCount; i++ )
			{
				var t = postCount == 1 ? 0.5f : i / (float)(postCount - 1);
				if ( MathF.Abs( x - (-halfL + length * t) ) < postW * 0.75f )
				{
					onPost = true;
					break;
				}
			}

			if ( onPost )
				continue;

			Part( stone,
				new Vector3( x, 0f, picketZ ),
				new Vector3( picketW, frameDepth * 0.7f, picketH ),
				IronTextured,
				IronFallback );
		}
	}

	/// <summary>
	/// Open L corner — same timber + iron language as the sides, no solid fill.
	/// </summary>
	public static void BuildCorner(
		GameObject root,
		float thickness,
		float height,
		float outwardX,
		float outwardY,
		Action<ModelRenderer, Color> track )
	{
		var wood = StylizedMaterials.Wood;
		var stone = StylizedMaterials.Stone;
		var ox = outwardX >= 0f ? 1f : -1f;
		var oy = outwardY >= 0f ? 1f : -1f;

		var post = MathF.Max( 10f, thickness * 0.22f );
		var railH = MathF.Max( 6f, height * 0.05f );
		var arm = thickness * 0.7f;
		var frameDepth = MathF.Max( 8f, thickness * 0.22f );
		var halfH = height * 0.5f;
		var zBot = -halfH + railH * 0.5f;
		var zTop = halfH - railH * 0.55f;
		var zMid = 0f;

		void Part( Vector3 localPos, Vector3 localSize, Material mat, Color textured, Color fallback ) =>
			AddPart( root, alongX: true, mat, localPos, localSize, textured, fallback, track );

		// Corner post.
		Part( Vector3.Zero, new Vector3( post, post, height ), wood, WoodTextured, WoodFallback );

		// Rails along both arms (inward along each adjoining wall).
		void ArmRails( Vector3 mid, Vector3 size )
		{
			Part( mid + new Vector3( 0f, 0f, zBot ), size.WithZ( railH ), wood, WoodTextured, WoodFallback );
			Part( mid + new Vector3( 0f, 0f, zMid ), size.WithZ( railH * 0.8f ), wood, WoodTextured, WoodFallback );
			Part( mid + new Vector3( 0f, 0f, zTop ), size.WithZ( railH ), wood, WoodTextured, WoodFallback );
		}

		ArmRails( new Vector3( -ox * arm * 0.5f, 0f, 0f ), new Vector3( arm, frameDepth, 0f ) );
		ArmRails( new Vector3( 0f, -oy * arm * 0.5f, 0f ), new Vector3( frameDepth, arm, 0f ) );

		var picketH = height * 0.78f;
		var picketZ = -halfH + railH + picketH * 0.5f + 1f;
		var picketW = MathF.Max( 2.2f, thickness * 0.04f );
		for ( var i = 1; i <= 3; i++ )
		{
			var d = arm * (i / 4f);
			Part(
				new Vector3( -ox * d, 0f, picketZ ),
				new Vector3( picketW, frameDepth * 0.7f, picketH ),
				stone, IronTextured, IronFallback );
			Part(
				new Vector3( 0f, -oy * d, picketZ ),
				new Vector3( frameDepth * 0.7f, picketW, picketH ),
				stone, IronTextured, IronFallback );
		}
	}

	/// <summary>
	/// True when the wall runs along world X (north/south ring). Prefers ring position over
	/// size comparison — critical when length == thickness (1×1 cells).
	/// </summary>
	public static bool RunsAlongX( Vector3 wallCenter, Vector3 size )
	{
		var half = GameConstants.ArenaHalf;
		var distN = MathF.Abs( MathF.Abs( wallCenter.y ) - half );
		var distE = MathF.Abs( MathF.Abs( wallCenter.x ) - half );

		if ( distE + 1f < distN )
			return false;
		if ( distN + 1f < distE )
			return true;

		if ( MathF.Abs( size.x - size.y ) > 1f )
			return size.x > size.y;

		return WallApproach.FromWorldPosition( wallCenter, Vector3.Zero )
			is WallApproachSide.North or WallApproachSide.South;
	}

	static void AddPart(
		GameObject root,
		bool alongX,
		Material mat,
		Vector3 localPos,
		Vector3 localSize,
		Color textured,
		Color fallback,
		Action<ModelRenderer, Color> track )
	{
		var useTexture = mat is not null && mat.IsValid() && mat != MeshPrimitives.Mat;
		var tint = useTexture ? textured : fallback;
		var box = MeshPrimitives.Box;

		var go = new GameObject( root, true, "Scaffold" );
		go.LocalPosition = Local( alongX, localPos );
		go.LocalScale = MeshPrimitives.ScaleFor( box, Local( alongX, localSize ) );

		var mr = go.Components.Create<ModelRenderer>();
		mr.Model = box;
		mr.MaterialOverride = mat;
		mr.Tint = tint;
		track?.Invoke( mr, tint );
	}

	static Vector3 Local( bool alongX, Vector3 alongDepthUp ) =>
		alongX
			? alongDepthUp
			: new Vector3( alongDepthUp.y, alongDepthUp.x, alongDepthUp.z );
}
