namespace FinalOutpost;

/// <summary>
/// Shared Command Post mesh — used by the player's core (OutpostManager) and
/// rival seed plots (RivalBaseVisual) so both always look identical.
///
/// The whole structure fits the 2×2-cell core footprint exactly
/// (2 × CellSize square), and every part is authored as a fraction of
/// CellSize so TileScale changes rescale it cleanly.
/// </summary>
public static class CommandPostVisual
{
	static readonly Color StoneFallback = new( 0.62f, 0.62f, 0.64f );
	static readonly Color BrickFallback = new( 0.62f, 0.32f, 0.24f );
	static readonly Color PlasterFallback = new( 0.88f, 0.84f, 0.76f );
	static readonly Color SlateFallback = new( 0.44f, 0.48f, 0.55f );
	static readonly Color WoodFallback = new( 0.5f, 0.36f, 0.22f );
	static readonly Color DarkTimber = new( 0.3f, 0.22f, 0.14f );
	static readonly Color BannerRed = new( 0.75f, 0.18f, 0.16f );
	static readonly Color GlassBlue = new( 0.35f, 0.55f, 0.7f );

	/// <param name="root">Parent object positioned at the core center.</param>
	/// <param name="track">Called per renderer so damage tinting can restore base colors. May be null.</param>
	public static void Build( GameObject root, Action<ModelRenderer, Color> track )
	{
		var box = MeshPrimitives.Box;
		var cyl = MeshPrimitives.Cylinder;
		var pyr = MeshPrimitives.Pyramid;
		var flat = MeshPrimitives.Mat;
		var stone = StylizedMaterials.Stone;
		var brick = StylizedMaterials.Brick;
		var plaster = StylizedMaterials.Plaster;
		var slate = StylizedMaterials.Slate;
		var wood = StylizedMaterials.Wood;
		var white = Color.White;
		var c = GameConstants.CellSize;

		void Part( Model model, Material mat, Vector3 localPos, Vector3 size, Color textured, Color fallback )
		{
			var useTexture = mat is not null && mat.IsValid() && mat != MeshPrimitives.Mat;
			var tint = useTexture ? textured : fallback;

			var go = new GameObject( root, true, "CorePart" );
			go.LocalPosition = localPos;
			go.LocalScale = MeshPrimitives.ScaleFor( model, size );

			var mr = go.Components.Create<ModelRenderer>();
			mr.Model = model;
			mr.MaterialOverride = mat;
			mr.Tint = tint;
			track?.Invoke( mr, tint );
		}

		void Accent( Model model, Vector3 localPos, Vector3 size, Color color ) =>
			Part( model, flat, localPos, size, color, color );

		// Plinth marking the exact 2×2-cell footprint.
		Part( box, stone, new Vector3( 0, 0, c * 0.035f ), new Vector3( c * 2f, c * 2f, c * 0.07f ), new Color( 0.85f, 0.85f, 0.88f ), StoneFallback );

		// Four corner turrets with slate cone caps.
		for ( var sx = -1; sx <= 1; sx += 2 )
		{
			for ( var sy = -1; sy <= 1; sy += 2 )
			{
				var tx = sx * c * 0.78f;
				var ty = sy * c * 0.78f;
				Part( cyl, stone, new Vector3( tx, ty, c * 0.5f ), new Vector3( c * 0.4f, c * 0.4f, c * 0.9f ), white, StoneFallback );
				Part( cyl, stone, new Vector3( tx, ty, c * 0.97f ), new Vector3( c * 0.46f, c * 0.46f, c * 0.06f ), new Color( 0.82f, 0.85f, 0.92f ), StoneFallback );
				Part( pyr, slate, new Vector3( tx, ty, c * 1.14f ), new Vector3( c * 0.44f, c * 0.44f, c * 0.28f ), white, SlateFallback );
			}
		}

		// Curtain walls linking the turrets.
		Part( box, stone, new Vector3( 0, c * 0.82f, c * 0.32f ), new Vector3( c * 1.5f, c * 0.16f, c * 0.5f ), white, StoneFallback );
		Part( box, stone, new Vector3( 0, -c * 0.82f, c * 0.32f ), new Vector3( c * 1.5f, c * 0.16f, c * 0.5f ), white, StoneFallback );
		Part( box, stone, new Vector3( -c * 0.82f, 0, c * 0.32f ), new Vector3( c * 0.16f, c * 1.5f, c * 0.5f ), white, StoneFallback );
		Part( box, stone, new Vector3( c * 0.82f, 0, c * 0.32f ), new Vector3( c * 0.16f, c * 1.5f, c * 0.5f ), white, StoneFallback );

		// Central keep: brick hall, plaster upper floor, slate roof.
		Part( box, brick, new Vector3( 0, 0, c * 0.45f ), new Vector3( c * 1.14f, c * 1.14f, c * 0.76f ), white, BrickFallback );
		Part( box, wood, new Vector3( 0, 0, c * 0.86f ), new Vector3( c * 1.22f, c * 1.22f, c * 0.07f ), white, WoodFallback );
		Part( box, plaster, new Vector3( 0, 0, c * 1.08f ), new Vector3( c * 0.92f, c * 0.92f, c * 0.38f ), white, PlasterFallback );
		Part( pyr, slate, new Vector3( 0, 0, c * 1.47f ), new Vector3( c * 1.06f, c * 1.06f, c * 0.42f ), white, SlateFallback );

		// Gate through the east curtain wall, aligned with the keep entrance.
		Accent( box, new Vector3( c * 0.82f, 0, c * 0.26f ), new Vector3( c * 0.18f, c * 0.32f, c * 0.38f ), DarkTimber );

		// Entrance, windows, and the command banner.
		Accent( box, new Vector3( c * 0.58f, 0, c * 0.28f ), new Vector3( c * 0.04f, c * 0.3f, c * 0.42f ), DarkTimber );
		Accent( box, new Vector3( c * 0.58f, c * 0.35f, c * 0.5f ), new Vector3( c * 0.03f, c * 0.14f, c * 0.18f ), GlassBlue );
		Accent( box, new Vector3( c * 0.58f, -c * 0.35f, c * 0.5f ), new Vector3( c * 0.03f, c * 0.14f, c * 0.18f ), GlassBlue );
		Accent( box, new Vector3( c * 0.47f, 0, c * 1.1f ), new Vector3( c * 0.02f, c * 0.3f, c * 0.22f ), GlassBlue );
		Part( box, wood, new Vector3( 0, 0, c * 1.85f ), new Vector3( c * 0.035f, c * 0.035f, c * 0.5f ), new Color( 0.8f, 0.72f, 0.55f ), WoodFallback );
		Accent( box, new Vector3( c * 0.11f, 0, c * 2.02f ), new Vector3( c * 0.22f, c * 0.02f, c * 0.14f ), BannerRed );
	}
}
