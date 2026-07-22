namespace FinalOutpost;

/// <summary>
/// Shared multi-part meshes for placed buildings and the placement ghost.
/// Keep one builder so the ghost always matches the real structure.
///
/// Every structure sits on a plinth exactly one cell wide (CellSize × CellSize)
/// and all part sizes are authored as fractions of CellSize, so changing
/// TileScale rescales every building without touching this file.
/// </summary>
public static class BuildingVisual
{
	public sealed class Result
	{
		public List<(ModelRenderer Renderer, Color Base)> Parts { get; } = new();
		public GameObject HeadRoot { get; set; }
		public Vector3 MuzzleLocal { get; set; } = new( 30f, 0f, 0f );
		public ModelRenderer Rubble { get; set; }
	}

	// Fallback colors used when a textured material fails to load.
	static readonly Color StoneFallback = new( 0.62f, 0.62f, 0.64f );
	static readonly Color WoodFallback = new( 0.5f, 0.36f, 0.22f );
	static readonly Color RoofFallback = new( 0.78f, 0.28f, 0.16f );
	static readonly Color BrickFallback = new( 0.62f, 0.32f, 0.24f );
	static readonly Color MetalFallback = new( 0.46f, 0.49f, 0.54f );
	static readonly Color PlasterFallback = new( 0.88f, 0.84f, 0.76f );
	static readonly Color ThatchFallback = new( 0.76f, 0.6f, 0.3f );
	static readonly Color CropsFallback = new( 0.38f, 0.55f, 0.26f );
	static readonly Color AwningFallback = new( 0.72f, 0.25f, 0.2f );
	static readonly Color SlateFallback = new( 0.44f, 0.48f, 0.55f );

	// Flat-material accent colors (doors, glass, iron fittings).
	static readonly Color DarkTimber = new( 0.3f, 0.22f, 0.14f );
	static readonly Color GlassBlue = new( 0.35f, 0.55f, 0.7f );
	static readonly Color GlassCyan = new( 0.4f, 0.85f, 0.9f );
	static readonly Color IronDark = new( 0.24f, 0.25f, 0.28f );
	static readonly Color BannerRed = new( 0.75f, 0.18f, 0.16f );
	static readonly Color BannerBlue = new( 0.25f, 0.42f, 0.75f );
	static readonly Color GlowGreen = new( 0.45f, 0.95f, 0.5f );
	static readonly Color CrossRed = new( 0.85f, 0.15f, 0.15f );

	/// <summary>
	/// Horizontal muzzle offset for all defenses — past the 1×1 cell rim so shots never
	/// spawn inside the tower's own stone/timber volume (the classic "blocked vision" bug).
	/// </summary>
	public static float DefenseMuzzleReach( float cell ) => cell * 0.56f;
	public static float DefenseMuzzleLift( float cell ) => cell * 0.08f;

	static readonly Color RankBronze = new( 0.62f, 0.42f, 0.22f );
	static readonly Color RankSilver = new( 0.72f, 0.74f, 0.78f );
	static readonly Color RankGold = new( 0.88f, 0.72f, 0.28f );
	static readonly Color RankPlatinum = new( 0.55f, 0.88f, 0.95f );

	/// <param name="worldPos">World center — used to orient wall scaffolds.</param>
	/// <param name="level">1–MaxLevel; higher tiers add subtle trim / gear (placement ghost uses 1).</param>
	public static Result Build( GameObject root, BuildableId type, Vector3 worldPos, bool includeRubble = false, int level = 1 )
	{
		var result = new Result();
		var box = MeshPrimitives.Box;
		var cyl = MeshPrimitives.Cylinder;
		var pyr = MeshPrimitives.Pyramid;
		var flat = MeshPrimitives.Mat;
		var stone = StylizedMaterials.Stone;
		var wood = StylizedMaterials.Wood;
		var brick = StylizedMaterials.Brick;
		var metal = StylizedMaterials.Metal;
		var plaster = StylizedMaterials.Plaster;
		var thatch = StylizedMaterials.Thatch;
		var crops = StylizedMaterials.Crops;
		var awning = StylizedMaterials.Awning;
		var slate = StylizedMaterials.Slate;
		var white = Color.White;
		var c = GameConstants.CellSize;
		level = Math.Clamp( level, 1, 5 );
		bool Lv( int min ) => level >= min;
		Color RankTint( int mark ) => mark switch
		{
			0 => RankBronze,
			1 => RankSilver,
			2 => RankGold,
			_ => RankPlatinum
		};

		ModelRenderer Part(
			Model model,
			Material mat,
			Vector3 localPos,
			Vector3 size,
			Color textured,
			Color fallback,
			GameObject parent = null,
			Rotation? rotation = null,
			bool track = true )
		{
			var useTexture = mat is not null && mat.IsValid() && mat != MeshPrimitives.Mat;
			var tint = useTexture ? textured : fallback;

			var go = new GameObject( parent ?? root, true, "Part" );
			go.LocalPosition = localPos;
			if ( rotation.HasValue )
				go.LocalRotation = rotation.Value;
			go.LocalScale = MeshPrimitives.ScaleFor( model, size );

			var mr = go.Components.Create<ModelRenderer>();
			mr.Model = model;
			mr.MaterialOverride = mat;
			mr.Tint = tint;

			if ( track )
				result.Parts.Add( (mr, tint) );

			return mr;
		}

		// Flat-colored accent (door, glass, iron) — always uses the solid color.
		void Accent( Model model, Vector3 localPos, Vector3 size, Color color, GameObject parent = null, Rotation? rotation = null ) =>
			Part( model, flat, localPos, size, color, color, parent, rotation );

		// One-cell plinth marking the exact grid footprint.
		void Plinth() =>
			Part( box, stone, new Vector3( 0, 0, c * 0.025f ), new Vector3( c, c, c * 0.05f ), new Color( 0.85f, 0.85f, 0.88f ), StoneFallback );

		/// <summary>Small front plaques — one per level above 1 (bronze → platinum).</summary>
		void RankMarks()
		{
			for ( var i = 0; i < level - 1; i++ )
			{
				var y = (i - 1.5f) * c * 0.11f;
				Accent( box, new Vector3( c * 0.485f, y, c * 0.055f ), new Vector3( c * 0.035f, c * 0.07f, c * 0.028f ), RankTint( i ) );
			}
		}

		void EnsureHeadRoot( float pivotZ )
		{
			result.HeadRoot = new GameObject( root, true, "TurretHead" );
			result.HeadRoot.LocalPosition = new Vector3( 0f, 0f, pivotZ );
		}

		void HeadPart( Model model, Material mat, Vector3 worldLocalPos, float pivotZ, Vector3 size, Color textured, Color fallback, Rotation? rotation = null )
		{
			var local = new Vector3( worldLocalPos.x, worldLocalPos.y, worldLocalPos.z - pivotZ );
			Part( model, mat, local, size, textured, fallback, result.HeadRoot, rotation );
		}

		switch ( type )
		{
			case BuildableId.GunTower:
			{
				// Round stone tower. Rotating guns sit ABOVE the merlons with a clear 360° arc.
				Plinth();
				RankMarks();
				Part( cyl, stone, new Vector3( 0, 0, c * 0.2f ), new Vector3( c * 0.82f, c * 0.82f, c * 0.34f ), white, StoneFallback );
				Part( cyl, brick, new Vector3( 0, 0, c * 0.5f ), new Vector3( c * 0.68f, c * 0.68f, c * 0.28f ), white, BrickFallback );
				Part( cyl, stone, new Vector3( 0, 0, c * 0.68f ), new Vector3( c * 0.8f, c * 0.8f, c * 0.08f ), new Color( 0.85f, 0.88f, 0.95f ), StoneFallback );
				if ( Lv( 2 ) )
					Part( cyl, metal, new Vector3( 0, 0, c * 0.42f ), new Vector3( c * 0.84f, c * 0.84f, c * 0.04f ), new Color( 0.7f, 0.72f, 0.76f ), MetalFallback );
				// Diagonal merlons only — never sit on the cardinal aim lines.
				var merlonH = Lv( 3 ) ? c * 0.11f : c * 0.08f;
				for ( var i = 0; i < 4; i++ )
				{
					var ang = i * MathF.PI * 0.5f + MathF.PI * 0.25f;
					var mx = MathF.Cos( ang ) * c * 0.34f;
					var my = MathF.Sin( ang ) * c * 0.34f;
					Part( box, stone, new Vector3( mx, my, c * 0.75f ), new Vector3( c * 0.12f, c * 0.12f, merlonH ), white, StoneFallback );
				}

				var gunPivot = c * 0.86f;
				EnsureHeadRoot( gunPivot );
				HeadPart( cyl, metal, new Vector3( 0, 0, gunPivot + c * 0.04f ), gunPivot, new Vector3( c * 0.42f, c * 0.42f, c * 0.06f ), white, MetalFallback );
				HeadPart( box, metal, new Vector3( 0, 0, gunPivot + c * 0.14f ), gunPivot, new Vector3( c * 0.3f, c * 0.26f, c * 0.16f ), white, MetalFallback );
				var barrelLen = Lv( 4 ) ? c * 0.48f : c * 0.42f;
				var barrelTint = Lv( 5 ) ? RankGold : new Color( 0.6f, 0.62f, 0.68f );
				HeadPart( box, metal, new Vector3( c * 0.38f, c * 0.06f, gunPivot + c * 0.14f ), gunPivot, new Vector3( barrelLen, c * 0.05f, c * 0.05f ), barrelTint, IronDark );
				HeadPart( box, metal, new Vector3( c * 0.38f, -c * 0.06f, gunPivot + c * 0.14f ), gunPivot, new Vector3( barrelLen, c * 0.05f, c * 0.05f ), barrelTint, IronDark );
				if ( Lv( 5 ) )
					HeadPart( box, metal, new Vector3( 0, 0, gunPivot + c * 0.28f ), gunPivot, new Vector3( c * 0.04f, c * 0.04f, c * 0.14f ), RankGold, RankGold );
				result.MuzzleLocal = new Vector3( DefenseMuzzleReach( c ), 0f, DefenseMuzzleLift( c ) );
				break;
			}

			case BuildableId.CannonTower:
			{
				// Open-top bastion: low stone ring + wood deck. Barrel is a +X box (not a pitched
				// cylinder) so it always tracks LookAt forward and clears the rim all around.
				Plinth();
				RankMarks();
				Part( cyl, stone, new Vector3( 0, 0, c * 0.16f ), new Vector3( c * 0.88f, c * 0.88f, c * 0.26f ), white, StoneFallback );
				Part( cyl, stone, new Vector3( 0, 0, c * 0.32f ), new Vector3( c * 0.94f, c * 0.94f, c * 0.06f ), new Color( 0.8f, 0.82f, 0.88f ), StoneFallback );
				Part( box, wood, new Vector3( 0, 0, c * 0.38f ), new Vector3( c * 0.7f, c * 0.7f, c * 0.06f ), white, WoodFallback );
				if ( Lv( 2 ) )
				{
					Part( box, wood, new Vector3( c * 0.22f, c * 0.22f, c * 0.42f ), new Vector3( c * 0.16f, c * 0.12f, c * 0.08f ), new Color( 0.7f, 0.55f, 0.35f ), WoodFallback );
					Part( box, wood, new Vector3( -c * 0.22f, -c * 0.2f, c * 0.42f ), new Vector3( c * 0.14f, c * 0.14f, c * 0.08f ), new Color( 0.7f, 0.55f, 0.35f ), WoodFallback );
				}
				if ( Lv( 4 ) )
					Part( cyl, metal, new Vector3( 0, 0, c * 0.34f ), new Vector3( c * 0.98f, c * 0.98f, c * 0.035f ), new Color( 0.65f, 0.67f, 0.72f ), MetalFallback );

				var cannonPivot = c * 0.42f;
				EnsureHeadRoot( cannonPivot );
				HeadPart( box, metal, new Vector3( -c * 0.06f, 0, cannonPivot + c * 0.12f ), cannonPivot, new Vector3( c * 0.28f, c * 0.28f, c * 0.22f ), white, MetalFallback );
				var barrelW = Lv( 3 ) ? c * 0.16f : c * 0.14f;
				var barrelL = Lv( 5 ) ? c * 0.62f : c * 0.55f;
				var barrelTint = Lv( 5 ) ? RankGold : new Color( 0.5f, 0.52f, 0.56f );
				HeadPart( box, metal, new Vector3( c * 0.36f, 0, cannonPivot + c * 0.14f ), cannonPivot, new Vector3( barrelL, barrelW, barrelW ), barrelTint, IronDark );
				HeadPart( cyl, wood, new Vector3( 0, c * 0.18f, cannonPivot + c * 0.06f ), cannonPivot, new Vector3( c * 0.2f, c * 0.2f, c * 0.05f ), new Color( 0.75f, 0.6f, 0.42f ), WoodFallback, Rotation.FromRoll( 90f ) );
				HeadPart( cyl, wood, new Vector3( 0, -c * 0.18f, cannonPivot + c * 0.06f ), cannonPivot, new Vector3( c * 0.2f, c * 0.2f, c * 0.05f ), new Color( 0.75f, 0.6f, 0.42f ), WoodFallback, Rotation.FromRoll( 90f ) );
				if ( Lv( 3 ) )
					HeadPart( cyl, metal, new Vector3( c * 0.58f, 0, cannonPivot + c * 0.14f ), cannonPivot, new Vector3( c * 0.12f, c * 0.12f, c * 0.04f ), new Color( 0.45f, 0.46f, 0.5f ), IronDark, Rotation.FromRoll( 90f ) );
				result.MuzzleLocal = new Vector3( DefenseMuzzleReach( c ), 0f, DefenseMuzzleLift( c ) );
				break;
			}

			case BuildableId.LongRangeTower:
			{
				// Open timber frame + compact rotating cabin. Sniper tube is an external mount
				// past the cabin face so legs/roof never sit in the firing line.
				Plinth();
				RankMarks();
				for ( var sx = -1; sx <= 1; sx += 2 )
					for ( var sy = -1; sy <= 1; sy += 2 )
						Part( box, wood, new Vector3( sx * c * 0.32f, sy * c * 0.32f, c * 0.48f ), new Vector3( c * 0.08f, c * 0.08f, c * 0.9f ), white, WoodFallback );
				Part( box, wood, new Vector3( 0, 0, c * 0.34f ), new Vector3( c * 0.7f, c * 0.06f, c * 0.06f ), new Color( 0.85f, 0.78f, 0.62f ), WoodFallback );
				Part( box, wood, new Vector3( 0, 0, c * 0.34f ), new Vector3( c * 0.06f, c * 0.7f, c * 0.06f ), new Color( 0.85f, 0.78f, 0.62f ), WoodFallback );
				Part( box, wood, new Vector3( 0, 0, c * 0.7f ), new Vector3( c * 0.7f, c * 0.06f, c * 0.06f ), new Color( 0.85f, 0.78f, 0.62f ), WoodFallback );
				Part( box, wood, new Vector3( 0, 0, c * 0.7f ), new Vector3( c * 0.06f, c * 0.7f, c * 0.06f ), new Color( 0.85f, 0.78f, 0.62f ), WoodFallback );
				Part( box, wood, new Vector3( 0, 0, c * 0.96f ), new Vector3( c * 0.72f, c * 0.72f, c * 0.06f ), white, WoodFallback );
				if ( Lv( 2 ) )
					Part( box, metal, new Vector3( 0, 0, c * 0.5f ), new Vector3( c * 0.08f, c * 0.08f, c * 0.55f ), new Color( 0.6f, 0.62f, 0.68f ), IronDark );
				if ( Lv( 4 ) )
					for ( var sx = -1; sx <= 1; sx += 2 )
						for ( var sy = -1; sy <= 1; sy += 2 )
							Part( box, metal, new Vector3( sx * c * 0.32f, sy * c * 0.32f, c * 0.08f ), new Vector3( c * 0.12f, c * 0.12f, c * 0.06f ), new Color( 0.55f, 0.57f, 0.62f ), IronDark );

				var sniperPivot = c * 1.0f;
				EnsureHeadRoot( sniperPivot );
				HeadPart( box, wood, new Vector3( 0, 0, sniperPivot + c * 0.12f ), sniperPivot, new Vector3( c * 0.52f, c * 0.52f, c * 0.24f ), white, WoodFallback );
				HeadPart( box, flat, new Vector3( c * 0.27f, 0, sniperPivot + c * 0.14f ), sniperPivot, new Vector3( c * 0.03f, c * 0.34f, c * 0.1f ), GlassBlue, GlassBlue );
				HeadPart( pyr, slate, new Vector3( 0, 0, sniperPivot + c * 0.34f ), sniperPivot, new Vector3( c * 0.64f, c * 0.64f, c * 0.2f ), white, SlateFallback );
				var tubeLen = Lv( 3 ) ? c * 0.56f : c * 0.48f;
				var tubeTint = Lv( 5 ) ? RankGold : new Color( 0.55f, 0.57f, 0.62f );
				HeadPart( box, metal, new Vector3( c * 0.48f, 0, sniperPivot + c * 0.1f ), sniperPivot, new Vector3( tubeLen, c * 0.05f, c * 0.05f ), tubeTint, IronDark );
				if ( Lv( 5 ) )
					HeadPart( cyl, metal, new Vector3( c * 0.22f, 0, sniperPivot + c * 0.18f ), sniperPivot, new Vector3( c * 0.08f, c * 0.08f, c * 0.06f ), RankPlatinum, RankPlatinum );
				result.MuzzleLocal = new Vector3( DefenseMuzzleReach( c ), 0f, DefenseMuzzleLift( c ) );
				break;
			}

			case BuildableId.Spotlight:
			{
				Plinth();
				RankMarks();
				Part( box, metal, new Vector3( 0, 0, c * 0.08f ), new Vector3( c * 0.36f, c * 0.36f, c * 0.1f ), white, MetalFallback );
				Part( cyl, metal, new Vector3( 0, 0, c * 0.55f ), new Vector3( c * 0.1f, c * 0.1f, c * 0.85f ), white, MetalFallback );
				Part( cyl, metal, new Vector3( 0, 0, c * 1.05f ), new Vector3( c * 0.42f, c * 0.42f, c * 0.08f ), new Color( 0.85f, 0.85f, 0.7f ), MetalFallback );
				Accent( cyl, new Vector3( 0, 0, c * 1.12f ), new Vector3( c * 0.32f, c * 0.32f, c * 0.06f ), new Color( 1f, 0.95f, 0.55f ) );
				if ( Lv( 3 ) )
					Accent( box, new Vector3( c * 0.2f, 0, c * 0.9f ), new Vector3( c * 0.08f, c * 0.08f, c * 0.2f ), IronDark );
				if ( Lv( 5 ) )
					Accent( cyl, new Vector3( 0, 0, c * 1.2f ), new Vector3( c * 0.14f, c * 0.14f, c * 0.05f ), RankGold );
				break;
			}

			case BuildableId.Minefield:
			{
				Plinth();
				RankMarks();
				Part( box, metal, new Vector3( 0, 0, c * 0.04f ), new Vector3( c * 0.9f, c * 0.9f, c * 0.05f ), new Color( 0.45f, 0.4f, 0.35f ), MetalFallback );
				for ( var i = 0; i < 4 + (Lv( 3 ) ? 2 : 0); i++ )
				{
					var ang = i * MathF.PI * 0.5f + (Lv( 2 ) ? 0.2f : 0f);
					var mx = MathF.Cos( ang ) * c * 0.22f;
					var my = MathF.Sin( ang ) * c * 0.22f;
					Part( cyl, metal, new Vector3( mx, my, c * 0.1f ), new Vector3( c * 0.16f, c * 0.16f, c * 0.08f ), new Color( 0.35f, 0.32f, 0.28f ), IronDark );
					Accent( cyl, new Vector3( mx, my, c * 0.15f ), new Vector3( c * 0.06f, c * 0.06f, c * 0.03f ), BannerRed );
				}
				if ( Lv( 5 ) )
					Accent( box, new Vector3( 0, 0, c * 0.12f ), new Vector3( c * 0.2f, c * 0.2f, c * 0.04f ), RankGold );
				break;
			}

			case BuildableId.OilSlick:
			{
				Plinth();
				RankMarks();
				Part( box, flat, new Vector3( 0, 0, c * 0.03f ), new Vector3( c * 0.92f, c * 0.92f, c * 0.04f ), new Color( 0.12f, 0.1f, 0.09f ), new Color( 0.12f, 0.1f, 0.09f ) );
				Part( cyl, metal, new Vector3( -c * 0.18f, c * 0.1f, c * 0.22f ), new Vector3( c * 0.28f, c * 0.28f, c * 0.28f ), white, MetalFallback );
				Accent( cyl, new Vector3( -c * 0.18f, c * 0.1f, c * 0.38f ), new Vector3( c * 0.22f, c * 0.22f, c * 0.05f ), IronDark );
				Part( box, metal, new Vector3( c * 0.2f, -c * 0.15f, c * 0.14f ), new Vector3( c * 0.35f, c * 0.18f, c * 0.12f ), new Color( 0.5f, 0.45f, 0.35f ), MetalFallback );
				Part( box, flat, new Vector3( c * 0.15f, c * 0.2f, c * 0.05f ), new Vector3( c * 0.4f, c * 0.35f, c * 0.03f ), new Color( 0.18f, 0.14f, 0.1f ), new Color( 0.18f, 0.14f, 0.1f ) );
				if ( Lv( 3 ) )
					Part( box, flat, new Vector3( -c * 0.25f, -c * 0.25f, c * 0.045f ), new Vector3( c * 0.35f, c * 0.3f, c * 0.025f ), new Color( 0.15f, 0.12f, 0.1f ), new Color( 0.15f, 0.12f, 0.1f ) );
				if ( Lv( 5 ) )
					Accent( box, new Vector3( c * 0.2f, -c * 0.15f, c * 0.24f ), new Vector3( c * 0.08f, c * 0.08f, c * 0.08f ), RankGold );
				break;
			}

			case BuildableId.AmmoDepot:
			{
				Plinth();
				RankMarks();
				Part( box, wood, new Vector3( 0, 0, c * 0.16f ), new Vector3( c * 0.7f, c * 0.55f, c * 0.28f ), white, WoodFallback );
				Part( box, wood, new Vector3( 0, 0, c * 0.36f ), new Vector3( c * 0.62f, c * 0.48f, c * 0.08f ), new Color( 0.65f, 0.5f, 0.3f ), WoodFallback );
				Part( box, metal, new Vector3( -c * 0.18f, c * 0.05f, c * 0.22f ), new Vector3( c * 0.22f, c * 0.18f, c * 0.16f ), new Color( 0.55f, 0.45f, 0.28f ), MetalFallback );
				Part( box, metal, new Vector3( c * 0.16f, -c * 0.08f, c * 0.2f ), new Vector3( c * 0.2f, c * 0.16f, c * 0.14f ), new Color( 0.5f, 0.42f, 0.26f ), MetalFallback );
				Accent( box, new Vector3( 0, -c * 0.28f, c * 0.18f ), new Vector3( c * 0.12f, c * 0.08f, c * 0.2f ), BannerRed );
				if ( Lv( 3 ) )
					Accent( cyl, new Vector3( c * 0.22f, c * 0.18f, c * 0.42f ), new Vector3( c * 0.1f, c * 0.1f, c * 0.08f ), IronDark );
				if ( Lv( 5 ) )
					Accent( box, new Vector3( 0, 0, c * 0.44f ), new Vector3( c * 0.18f, c * 0.18f, c * 0.05f ), RankGold );
				break;
			}

			case BuildableId.Hardpoint:
			{
				Plinth();
				RankMarks();
				var sandTint = new Color( 0.72f, 0.62f, 0.42f );
				Part( box, flat, new Vector3( 0, c * 0.28f, c * 0.12f ), new Vector3( c * 0.85f, c * 0.22f, c * 0.2f ), sandTint, sandTint );
				Part( box, flat, new Vector3( c * 0.28f, 0, c * 0.12f ), new Vector3( c * 0.22f, c * 0.7f, c * 0.2f ), new Color( 0.7f, 0.6f, 0.4f ), sandTint );
				Part( box, flat, new Vector3( -c * 0.28f, 0, c * 0.12f ), new Vector3( c * 0.22f, c * 0.7f, c * 0.2f ), new Color( 0.68f, 0.58f, 0.38f ), sandTint );
				Part( box, wood, new Vector3( 0, 0, c * 0.08f ), new Vector3( c * 0.4f, c * 0.4f, c * 0.1f ), white, WoodFallback );
				if ( Lv( 3 ) )
					Part( box, flat, new Vector3( 0, -c * 0.22f, c * 0.1f ), new Vector3( c * 0.55f, c * 0.16f, c * 0.14f ), new Color( 0.66f, 0.56f, 0.36f ), sandTint );
				if ( Lv( 5 ) )
					Accent( box, new Vector3( 0, c * 0.28f, c * 0.26f ), new Vector3( c * 0.2f, c * 0.08f, c * 0.06f ), RankGold );
				break;
			}

			case BuildableId.RadioMast:
			{
				Plinth();
				RankMarks();
				Part( box, metal, new Vector3( 0, 0, c * 0.1f ), new Vector3( c * 0.4f, c * 0.4f, c * 0.12f ), white, MetalFallback );
				Part( cyl, metal, new Vector3( 0, 0, c * 0.7f ), new Vector3( c * 0.08f, c * 0.08f, c * 1.1f ), white, MetalFallback );
				Part( box, metal, new Vector3( 0, 0, c * 1.25f ), new Vector3( c * 0.45f, c * 0.06f, c * 0.05f ), new Color( 0.7f, 0.72f, 0.75f ), IronDark );
				Part( box, metal, new Vector3( 0, 0, c * 1.25f ), new Vector3( c * 0.06f, c * 0.45f, c * 0.05f ), new Color( 0.7f, 0.72f, 0.75f ), IronDark );
				Accent( cyl, new Vector3( 0, 0, c * 1.35f ), new Vector3( c * 0.1f, c * 0.1f, c * 0.08f ), BannerRed );
				if ( Lv( 3 ) )
					Accent( box, new Vector3( c * 0.12f, 0, c * 0.55f ), new Vector3( c * 0.08f, c * 0.08f, c * 0.2f ), IronDark );
				if ( Lv( 5 ) )
					Accent( cyl, new Vector3( 0, 0, c * 1.45f ), new Vector3( c * 0.07f, c * 0.07f, c * 0.05f ), RankGold );
				break;
			}

			case BuildableId.Artillery:
			{
				Plinth();
				RankMarks();
				Part( box, stone, new Vector3( 0, 0, c * 0.12f ), new Vector3( c * 0.85f, c * 0.85f, c * 0.16f ), white, StoneFallback );
				Part( box, wood, new Vector3( 0, 0, c * 0.24f ), new Vector3( c * 0.7f, c * 0.7f, c * 0.08f ), white, WoodFallback );
				Part( box, wood, new Vector3( c * 0.28f, c * 0.28f, c * 0.2f ), new Vector3( c * 0.22f, c * 0.14f, c * 0.12f ), new Color( 0.7f, 0.58f, 0.38f ), WoodFallback );
				Part( box, wood, new Vector3( -c * 0.28f, -c * 0.22f, c * 0.2f ), new Vector3( c * 0.2f, c * 0.16f, c * 0.12f ), new Color( 0.7f, 0.58f, 0.38f ), WoodFallback );

				var artyPivot = c * 0.32f;
				EnsureHeadRoot( artyPivot );
				var barrelL = Lv( 4 ) ? c * 0.72f : c * 0.6f;
				HeadPart( box, metal, new Vector3( -c * 0.08f, 0, artyPivot + c * 0.14f ), artyPivot, new Vector3( c * 0.32f, c * 0.28f, c * 0.24f ), white, MetalFallback );
				HeadPart( box, metal, new Vector3( c * 0.28f, 0, artyPivot + c * 0.18f ), artyPivot, new Vector3( barrelL, c * 0.16f, c * 0.16f ), new Color( 0.4f, 0.42f, 0.38f ), IronDark );
				HeadPart( cyl, wood, new Vector3( -c * 0.05f, c * 0.2f, artyPivot + c * 0.06f ), artyPivot, new Vector3( c * 0.22f, c * 0.22f, c * 0.06f ), new Color( 0.7f, 0.55f, 0.35f ), WoodFallback, Rotation.FromRoll( 90f ) );
				HeadPart( cyl, wood, new Vector3( -c * 0.05f, -c * 0.2f, artyPivot + c * 0.06f ), artyPivot, new Vector3( c * 0.22f, c * 0.22f, c * 0.06f ), new Color( 0.7f, 0.55f, 0.35f ), WoodFallback, Rotation.FromRoll( 90f ) );
				if ( Lv( 5 ) )
					HeadPart( box, metal, new Vector3( c * 0.55f, 0, artyPivot + c * 0.18f ), artyPivot, new Vector3( c * 0.1f, c * 0.18f, c * 0.18f ), RankGold, RankGold );
				result.MuzzleLocal = new Vector3( DefenseMuzzleReach( c ), 0f, DefenseMuzzleLift( c ) );
				break;
			}

			case BuildableId.WallPiece:
			{
				// Always lay the scaffold along local +X; BuildManager yaw (R) turns the whole root
				// so walls join flush in either direction.
				var wallH = GameConstants.WallHeight;
				var frame = new GameObject( root, true, "WallFrame" );
				frame.LocalPosition = new Vector3( 0f, 0f, wallH * 0.5f );
				var footprint = new Vector3( c, c * 0.55f, wallH );
				WallScaffoldVisual.Build(
					frame,
					footprint,
					( mr, tint ) => result.Parts.Add( (mr, tint) ),
					Vector3.Zero,
					level );
				break;
			}

			case BuildableId.Barracks:
			{
				// Military longhouse: timber walls, hipped slate roof, flag and door.
				Plinth();
				RankMarks();
				Part( box, wood, new Vector3( 0, 0, c * 0.26f ), new Vector3( c * 0.92f, c * 0.8f, c * 0.42f ), white, WoodFallback );
				Part( pyr, slate, new Vector3( 0, 0, c * 0.59f ), new Vector3( c * 1f, c * 0.88f, c * 0.24f ), white, SlateFallback );
				Accent( box, new Vector3( c * 0.465f, 0, c * 0.2f ), new Vector3( c * 0.03f, c * 0.2f, c * 0.3f ), DarkTimber );
				Accent( box, new Vector3( c * 0.465f, c * 0.26f, c * 0.3f ), new Vector3( c * 0.02f, c * 0.12f, c * 0.12f ), GlassBlue );
				Accent( box, new Vector3( c * 0.465f, -c * 0.26f, c * 0.3f ), new Vector3( c * 0.02f, c * 0.12f, c * 0.12f ), GlassBlue );
				Part( box, wood, new Vector3( c * 0.4f, c * 0.43f, c * 0.35f ), new Vector3( c * 0.03f, c * 0.03f, c * 0.7f ), new Color( 0.8f, 0.72f, 0.55f ), WoodFallback );
				var banner = Lv( 4 ) ? BannerBlue : BannerRed;
				if ( Lv( 5 ) ) banner = RankGold;
				Accent( box, new Vector3( c * 0.4f, c * 0.365f, c * 0.64f ), new Vector3( c * 0.02f, c * 0.12f, c * 0.09f ), banner );
				if ( Lv( 2 ) )
					Accent( box, new Vector3( -c * 0.465f, 0, c * 0.3f ), new Vector3( c * 0.02f, c * 0.2f, c * 0.12f ), GlassBlue );
				if ( Lv( 3 ) )
					Part( box, wood, new Vector3( -c * 0.4f, -c * 0.43f, c * 0.3f ), new Vector3( c * 0.03f, c * 0.03f, c * 0.55f ), new Color( 0.8f, 0.72f, 0.55f ), WoodFallback );
				break;
			}

			case BuildableId.Lab:
			{
				// Research lab: plaster block, flat steel roof, glowing reactor vat and pipework.
				Plinth();
				RankMarks();
				Part( box, plaster, new Vector3( 0, 0, c * 0.27f ), new Vector3( c * 0.88f, c * 0.78f, c * 0.44f ), white, PlasterFallback );
				Part( box, metal, new Vector3( 0, 0, c * 0.515f ), new Vector3( c * 0.92f, c * 0.82f, c * 0.05f ), white, MetalFallback );
				Accent( box, new Vector3( c * 0.45f, 0, c * 0.33f ), new Vector3( c * 0.03f, c * 0.5f, c * 0.16f ), GlassCyan );
				Part( cyl, metal, new Vector3( -c * 0.18f, -c * 0.14f, c * 0.68f ), new Vector3( c * 0.34f, c * 0.34f, c * 0.28f ), white, MetalFallback );
				var glow = Lv( 5 ) ? RankPlatinum : Lv( 3 ) ? new Color( 0.55f, 1f, 0.65f ) : GlowGreen;
				Accent( cyl, new Vector3( -c * 0.18f, -c * 0.14f, c * 0.845f ), new Vector3( c * 0.28f, c * 0.28f, c * 0.05f ), glow );
				Part( box, metal, new Vector3( c * 0.12f, -c * 0.14f, c * 0.6f ), new Vector3( c * 0.28f, c * 0.05f, c * 0.05f ), new Color( 0.6f, 0.62f, 0.68f ), IronDark );
				Part( cyl, metal, new Vector3( c * 0.24f, -c * 0.14f, c * 0.6f ), new Vector3( c * 0.08f, c * 0.08f, c * 0.16f ), new Color( 0.6f, 0.62f, 0.68f ), IronDark );
				if ( Lv( 2 ) )
					Part( cyl, metal, new Vector3( c * 0.28f, c * 0.2f, c * 0.58f ), new Vector3( c * 0.1f, c * 0.1f, c * 0.12f ), new Color( 0.6f, 0.62f, 0.68f ), IronDark );
				if ( Lv( 4 ) )
					Accent( box, new Vector3( -c * 0.45f, 0, c * 0.33f ), new Vector3( c * 0.03f, c * 0.35f, c * 0.12f ), GlassCyan );
				break;
			}

			case BuildableId.Farm:
			{
				// Working field: tilled crop rows filling the cell, thatched shed and hay bales.
				Part( box, crops, new Vector3( 0, 0, c * 0.035f ), new Vector3( c * 0.98f, c * 0.98f, c * 0.07f ), white, CropsFallback );
				Part( box, wood, new Vector3( c * 0.28f, c * 0.28f, c * 0.18f ), new Vector3( c * 0.34f, c * 0.3f, c * 0.22f ), white, WoodFallback );
				Part( pyr, thatch, new Vector3( c * 0.28f, c * 0.28f, c * 0.37f ), new Vector3( c * 0.42f, c * 0.38f, c * 0.16f ), white, ThatchFallback );
				Part( box, thatch, new Vector3( -c * 0.32f, -c * 0.3f, c * 0.13f ), new Vector3( c * 0.14f, c * 0.12f, c * 0.12f ), white, ThatchFallback );
				Part( box, thatch, new Vector3( -c * 0.32f, -c * 0.14f, c * 0.12f ), new Vector3( c * 0.12f, c * 0.11f, c * 0.1f ), new Color( 0.92f, 0.85f, 0.6f ), ThatchFallback );
				Accent( box, new Vector3( -c * 0.3f, c * 0.35f, c * 0.1f ), new Vector3( c * 0.24f, c * 0.1f, c * 0.07f ), DarkTimber );
				for ( var i = 0; i < level - 1; i++ )
				{
					var y = (i - 1.5f) * c * 0.1f;
					Accent( box, new Vector3( c * 0.46f, y, c * 0.05f ), new Vector3( c * 0.04f, c * 0.06f, c * 0.025f ), RankTint( i ) );
				}
				if ( Lv( 2 ) )
					Part( box, thatch, new Vector3( -c * 0.18f, -c * 0.32f, c * 0.11f ), new Vector3( c * 0.11f, c * 0.1f, c * 0.09f ), white, ThatchFallback );
				if ( Lv( 3 ) )
					Part( box, wood, new Vector3( c * 0.05f, c * 0.35f, c * 0.12f ), new Vector3( c * 0.2f, c * 0.08f, c * 0.1f ), new Color( 0.75f, 0.6f, 0.4f ), WoodFallback );
				if ( Lv( 4 ) )
					Part( box, crops, new Vector3( -c * 0.05f, 0, c * 0.08f ), new Vector3( c * 0.5f, c * 0.35f, c * 0.06f ), new Color( 0.95f, 0.95f, 0.7f ), CropsFallback );
				if ( Lv( 5 ) )
					Part( box, wood, new Vector3( c * 0.28f, c * 0.28f, c * 0.48f ), new Vector3( c * 0.04f, c * 0.04f, c * 0.18f ), RankGold, RankGold );
				break;
			}

			case BuildableId.Factory:
			{
				// Brick works: brick hall, steel roof, twin smokestacks, loading door and crate.
				Plinth();
				RankMarks();
				Part( box, brick, new Vector3( 0, 0, c * 0.25f ), new Vector3( c * 0.92f, c * 0.8f, c * 0.4f ), white, BrickFallback );
				Part( box, metal, new Vector3( 0, 0, c * 0.48f ), new Vector3( c * 0.96f, c * 0.84f, c * 0.06f ), white, MetalFallback );
				var stackH = Lv( 3 ) ? c * 0.52f : c * 0.44f;
				Part( cyl, brick, new Vector3( c * 0.26f, c * 0.22f, c * 0.7f ), new Vector3( c * 0.14f, c * 0.14f, stackH ), white, BrickFallback );
				Accent( cyl, new Vector3( c * 0.26f, c * 0.22f, c * 0.7f + stackH * 0.52f ), new Vector3( c * 0.17f, c * 0.17f, c * 0.04f ), IronDark );
				Part( cyl, brick, new Vector3( c * 0.26f, -c * 0.06f, c * 0.64f ), new Vector3( c * 0.13f, c * 0.13f, c * 0.32f ), white, BrickFallback );
				Accent( cyl, new Vector3( c * 0.26f, -c * 0.06f, c * 0.815f ), new Vector3( c * 0.16f, c * 0.16f, c * 0.04f ), IronDark );
				Accent( box, new Vector3( c * 0.465f, 0, c * 0.19f ), new Vector3( c * 0.03f, c * 0.3f, c * 0.28f ), IronDark );
				Part( box, wood, new Vector3( -c * 0.36f, c * 0.36f, c * 0.12f ), new Vector3( c * 0.14f, c * 0.14f, c * 0.14f ), white, WoodFallback );
				if ( Lv( 2 ) )
					Part( box, wood, new Vector3( -c * 0.36f, c * 0.2f, c * 0.1f ), new Vector3( c * 0.12f, c * 0.12f, c * 0.1f ), new Color( 0.75f, 0.6f, 0.4f ), WoodFallback );
				if ( Lv( 4 ) )
					Part( cyl, brick, new Vector3( -c * 0.2f, c * 0.22f, c * 0.62f ), new Vector3( c * 0.11f, c * 0.11f, c * 0.28f ), white, BrickFallback );
				if ( Lv( 5 ) )
					Accent( cyl, new Vector3( c * 0.26f, c * 0.22f, c * 0.7f + stackH * 0.65f ), new Vector3( c * 0.08f, c * 0.08f, c * 0.08f ), RankGold );
				break;
			}

			case BuildableId.Library:
			{
				// Civic library: plaster hall with a columned portico and a blue banner.
				Plinth();
				RankMarks();
				Part( box, plaster, new Vector3( -c * 0.04f, 0, c * 0.28f ), new Vector3( c * 0.84f, c * 0.76f, c * 0.46f ), white, PlasterFallback );
				Part( cyl, plaster, new Vector3( c * 0.42f, c * 0.2f, c * 0.26f ), new Vector3( c * 0.09f, c * 0.09f, c * 0.42f ), white, PlasterFallback );
				Part( cyl, plaster, new Vector3( c * 0.42f, -c * 0.2f, c * 0.26f ), new Vector3( c * 0.09f, c * 0.09f, c * 0.42f ), white, PlasterFallback );
				if ( Lv( 3 ) )
				{
					Part( cyl, plaster, new Vector3( c * 0.42f, c * 0.05f, c * 0.26f ), new Vector3( c * 0.07f, c * 0.07f, c * 0.38f ), white, PlasterFallback );
					Part( cyl, plaster, new Vector3( c * 0.42f, -c * 0.05f, c * 0.26f ), new Vector3( c * 0.07f, c * 0.07f, c * 0.38f ), white, PlasterFallback );
				}
				Part( box, slate, new Vector3( c * 0.4f, 0, c * 0.495f ), new Vector3( c * 0.18f, c * 0.58f, c * 0.05f ), white, SlateFallback );
				Part( pyr, slate, new Vector3( -c * 0.04f, 0, c * 0.64f ), new Vector3( c * 0.94f, c * 0.86f, c * 0.26f ), white, SlateFallback );
				Accent( box, new Vector3( c * 0.385f, 0, c * 0.22f ), new Vector3( c * 0.03f, c * 0.2f, c * 0.32f ), DarkTimber );
				var libBanner = Lv( 5 ) ? RankGold : BannerBlue;
				Accent( box, new Vector3( c * 0.4f, 0, c * 0.4f ), new Vector3( c * 0.02f, Lv( 2 ) ? c * 0.38f : c * 0.3f, c * 0.12f ), libBanner );
				Part( box, stone, new Vector3( c * 0.44f, 0, c * 0.08f ), new Vector3( c * 0.12f, c * 0.5f, c * 0.06f ), new Color( 0.8f, 0.8f, 0.85f ), StoneFallback );
				if ( Lv( 4 ) )
					Accent( box, new Vector3( -c * 0.45f, 0, c * 0.35f ), new Vector3( c * 0.02f, c * 0.22f, c * 0.14f ), GlassBlue );
				break;
			}

			case BuildableId.School:
			{
				// Schoolhouse: warm plaster hall, slate roof, bell cupola on top.
				Plinth();
				RankMarks();
				Part( box, plaster, new Vector3( 0, 0, c * 0.27f ), new Vector3( c * 0.86f, c * 0.76f, c * 0.44f ), new Color( 1f, 0.92f, 0.75f ), new Color( 0.9f, 0.8f, 0.6f ) );
				Part( pyr, slate, new Vector3( 0, 0, c * 0.62f ), new Vector3( c * 0.94f, c * 0.86f, c * 0.26f ), white, SlateFallback );
				for ( var sx = -1; sx <= 1; sx += 2 )
					for ( var sy = -1; sy <= 1; sy += 2 )
						Part( box, wood, new Vector3( sx * c * 0.07f, sy * c * 0.07f, c * 0.75f ), new Vector3( c * 0.035f, c * 0.035f, c * 0.14f ), white, WoodFallback );
				Part( pyr, slate, new Vector3( 0, 0, c * 0.87f ), new Vector3( c * 0.24f, c * 0.24f, c * 0.11f ), white, SlateFallback );
				var bell = Lv( 5 ) ? RankGold : new Color( 0.72f, 0.6f, 0.25f );
				Accent( box, new Vector3( 0, 0, c * 0.76f ), new Vector3( c * 0.06f, c * 0.06f, Lv( 3 ) ? c * 0.1f : c * 0.07f ), bell );
				Accent( box, new Vector3( c * 0.435f, 0, c * 0.2f ), new Vector3( c * 0.03f, c * 0.2f, c * 0.3f ), DarkTimber );
				Accent( box, new Vector3( c * 0.435f, c * 0.25f, c * 0.32f ), new Vector3( c * 0.02f, c * 0.14f, c * 0.14f ), GlassBlue );
				Accent( box, new Vector3( c * 0.435f, -c * 0.25f, c * 0.32f ), new Vector3( c * 0.02f, c * 0.14f, c * 0.14f ), GlassBlue );
				if ( Lv( 2 ) )
				{
					Accent( box, new Vector3( -c * 0.435f, c * 0.25f, c * 0.32f ), new Vector3( c * 0.02f, c * 0.12f, c * 0.12f ), GlassBlue );
					Accent( box, new Vector3( -c * 0.435f, -c * 0.25f, c * 0.32f ), new Vector3( c * 0.02f, c * 0.12f, c * 0.12f ), GlassBlue );
				}
				if ( Lv( 4 ) )
					Accent( box, new Vector3( 0, 0, c * 0.92f ), new Vector3( c * 0.03f, c * 0.03f, c * 0.1f ), IronDark );
				break;
			}

			case BuildableId.Hospital:
			{
				// Clinic: bright plaster block, flat roof, bold red crosses on roof and door.
				Plinth();
				RankMarks();
				Part( box, plaster, new Vector3( 0, 0, c * 0.29f ), new Vector3( c * 0.88f, c * 0.8f, c * 0.48f ), new Color( 1f, 1f, 1f ), PlasterFallback );
				Part( box, plaster, new Vector3( 0, 0, c * 0.555f ), new Vector3( c * 0.92f, c * 0.84f, c * 0.05f ), new Color( 0.9f, 0.9f, 0.94f ), PlasterFallback );
				var crossScale = Lv( 3 ) ? 1.15f : 1f;
				Accent( box, new Vector3( 0, 0, c * 0.6f ), new Vector3( c * 0.42f * crossScale, c * 0.13f, c * 0.05f ), CrossRed );
				Accent( box, new Vector3( 0, 0, c * 0.605f ), new Vector3( c * 0.13f, c * 0.42f * crossScale, c * 0.062f ), CrossRed );
				Accent( box, new Vector3( c * 0.445f, 0, c * 0.2f ), new Vector3( c * 0.03f, c * 0.26f, c * 0.3f ), GlassBlue );
				Accent( box, new Vector3( c * 0.455f, 0, c * 0.42f ), new Vector3( c * 0.02f, c * 0.2f, c * 0.06f ), CrossRed );
				Accent( box, new Vector3( c * 0.455f, 0, c * 0.42f ), new Vector3( c * 0.032f, c * 0.06f, c * 0.2f ), CrossRed );
				if ( Lv( 2 ) )
					Accent( box, new Vector3( -c * 0.445f, 0, c * 0.33f ), new Vector3( c * 0.02f, c * 0.28f, c * 0.14f ), GlassBlue );
				if ( Lv( 4 ) )
					Part( box, metal, new Vector3( c * 0.3f, c * 0.32f, c * 0.62f ), new Vector3( c * 0.12f, c * 0.12f, c * 0.08f ), new Color( 0.7f, 0.72f, 0.76f ), MetalFallback );
				if ( Lv( 5 ) )
					Accent( box, new Vector3( 0, 0, c * 0.68f ), new Vector3( c * 0.08f, c * 0.08f, c * 0.06f ), RankGold );
				break;
			}

			case BuildableId.Shop:
			{
				// Market shop: timber storefront with a striped awning, stall and crates.
				Plinth();
				RankMarks();
				Part( box, wood, new Vector3( -c * 0.05f, 0, c * 0.25f ), new Vector3( c * 0.74f, c * 0.74f, c * 0.4f ), white, WoodFallback );
				Part( box, wood, new Vector3( -c * 0.05f, 0, c * 0.475f ), new Vector3( c * 0.8f, c * 0.8f, c * 0.05f ), new Color( 0.7f, 0.55f, 0.38f ), WoodFallback );
				var awningW = Lv( 3 ) ? c * 0.82f : c * 0.72f;
				Part( box, awning, new Vector3( c * 0.37f, 0, c * 0.42f ), new Vector3( c * 0.26f, awningW, c * 0.035f ), white, AwningFallback, rotation: Rotation.FromPitch( 18f ) );
				Accent( box, new Vector3( c * 0.32f, 0, c * 0.28f ), new Vector3( c * 0.02f, c * 0.4f, c * 0.16f ), GlassBlue );
				Part( box, wood, new Vector3( c * 0.38f, 0, c * 0.1f ), new Vector3( c * 0.16f, c * 0.5f, c * 0.12f ), new Color( 0.8f, 0.66f, 0.45f ), WoodFallback );
				Part( box, wood, new Vector3( c * 0.34f, -c * 0.4f, c * 0.12f ), new Vector3( c * 0.13f, c * 0.13f, c * 0.13f ), white, WoodFallback );
				Part( box, wood, new Vector3( c * 0.2f, -c * 0.4f, c * 0.1f ), new Vector3( c * 0.1f, c * 0.1f, c * 0.1f ), new Color( 0.85f, 0.72f, 0.5f ), WoodFallback );
				var shopBanner = Lv( 5 ) ? RankGold : BannerRed;
				Accent( box, new Vector3( -c * 0.05f, c * 0.39f, c * 0.4f ), new Vector3( c * 0.14f, c * 0.02f, c * 0.1f ), shopBanner );
				if ( Lv( 2 ) )
					Part( box, wood, new Vector3( c * 0.34f, c * 0.38f, c * 0.12f ), new Vector3( c * 0.12f, c * 0.12f, c * 0.12f ), white, WoodFallback );
				if ( Lv( 4 ) )
					Accent( box, new Vector3( c * 0.32f, c * 0.22f, c * 0.28f ), new Vector3( c * 0.02f, c * 0.16f, c * 0.12f ), GlassBlue );
				break;
			}

			case BuildableId.Observatory:
			{
				// Stone drum + dome + short telescope.
				Plinth();
				RankMarks();
				Part( cyl, stone, new Vector3( 0, 0, c * 0.22f ), new Vector3( c * 0.72f, c * 0.72f, c * 0.36f ), white, StoneFallback );
				Part( cyl, slate, new Vector3( 0, 0, c * 0.48f ), new Vector3( c * 0.78f, c * 0.78f, c * 0.1f ), white, SlateFallback );
				Part( cyl, plaster, new Vector3( 0, 0, c * 0.62f ), new Vector3( c * 0.62f, c * 0.62f, c * 0.28f ), new Color( 0.75f, 0.8f, 0.95f ), PlasterFallback );
				Accent( box, new Vector3( c * 0.28f, 0, c * 0.72f ), new Vector3( c * 0.42f, c * 0.08f, c * 0.08f ), IronDark );
				Accent( cyl, new Vector3( c * 0.48f, 0, c * 0.72f ), new Vector3( c * 0.1f, c * 0.1f, c * 0.1f ), GlassBlue );
				if ( Lv( 3 ) )
					Accent( box, new Vector3( -c * 0.28f, 0, c * 0.35f ), new Vector3( c * 0.04f, c * 0.2f, c * 0.16f ), GlassBlue );
				if ( Lv( 5 ) )
					Accent( cyl, new Vector3( 0, 0, c * 0.82f ), new Vector3( c * 0.08f, c * 0.08f, c * 0.06f ), RankGold );
				break;
			}

			case BuildableId.University:
			{
				// Wide plaster campus hall with twin columns and a peaked slate roof.
				Plinth();
				RankMarks();
				Part( box, plaster, new Vector3( 0, 0, c * 0.3f ), new Vector3( c * 0.9f, c * 0.78f, c * 0.5f ), white, PlasterFallback );
				Part( cyl, plaster, new Vector3( c * 0.38f, c * 0.22f, c * 0.28f ), new Vector3( c * 0.1f, c * 0.1f, c * 0.46f ), white, PlasterFallback );
				Part( cyl, plaster, new Vector3( c * 0.38f, -c * 0.22f, c * 0.28f ), new Vector3( c * 0.1f, c * 0.1f, c * 0.46f ), white, PlasterFallback );
				Part( pyr, slate, new Vector3( 0, 0, c * 0.68f ), new Vector3( c * 0.98f, c * 0.88f, c * 0.28f ), white, SlateFallback );
				Accent( box, new Vector3( c * 0.4f, 0, c * 0.22f ), new Vector3( c * 0.03f, c * 0.28f, c * 0.34f ), DarkTimber );
				var uniBanner = Lv( 5 ) ? RankGold : BannerBlue;
				Accent( box, new Vector3( 0, c * 0.4f, c * 0.42f ), new Vector3( c * 0.28f, c * 0.02f, c * 0.12f ), uniBanner );
				if ( Lv( 2 ) )
				{
					Accent( box, new Vector3( -c * 0.4f, c * 0.18f, c * 0.34f ), new Vector3( c * 0.02f, c * 0.14f, c * 0.14f ), GlassBlue );
					Accent( box, new Vector3( -c * 0.4f, -c * 0.18f, c * 0.34f ), new Vector3( c * 0.02f, c * 0.14f, c * 0.14f ), GlassBlue );
				}
				if ( Lv( 4 ) )
					Part( box, stone, new Vector3( 0, 0, c * 0.08f ), new Vector3( c * 0.5f, c * 0.55f, c * 0.06f ), new Color( 0.82f, 0.82f, 0.88f ), StoneFallback );
				break;
			}
		}

		if ( includeRubble )
		{
			result.Rubble = Part(
				box,
				stone,
				new Vector3( 0, 0, GameConstants.H( 8f ) ),
				new Vector3( c, c, GameConstants.H( 16f ) ),
				new Color( 0.32f, 0.28f, 0.24f ),
				new Color( 0.25f, 0.2f, 0.18f ),
				track: false );
			if ( result.Rubble.IsValid() )
				result.Rubble.GameObject.Enabled = false;
		}

		return result;
	}

	/// <summary>Local-space wall footprint (along +X). Rotate the building root for facing.</summary>
	public static Vector3 WallPieceFootprint( float cell, float wallH ) =>
		new( cell, cell * 0.55f, wallH );
}
