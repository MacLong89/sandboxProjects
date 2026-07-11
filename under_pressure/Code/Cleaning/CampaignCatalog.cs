namespace UnderPressure;

/// <summary>The 25-level story campaign. Each job plants or pays off narrative clues
/// revealed as grime is washed away.</summary>
public static class CampaignCatalog
{
	private static readonly Color Concrete = new( 0.78f, 0.64f, 0.42f );
	private static readonly Color CleanConcrete = new( 0.84f, 0.70f, 0.46f );
	private static readonly Color Grime = new( 0.52f, 0.36f, 0.10f );
	/// <summary>Algae / bio-sludge only — normal dirt uses <see cref="Grime"/> (brown).</summary>
	private static readonly Color Moss = new( 0.22f, 0.48f, 0.12f );
	private static readonly Color HouseWall = new( 1f, 0.82f, 0.32f );
	private static readonly Color Grass = new( 0.42f, 0.84f, 0.10f );
	private static readonly Color WoodClean = new( 0.96f, 0.70f, 0.26f );
	private static readonly Color GlassClean = new( 0.58f, 0.82f, 0.94f );
	private static readonly Color Paint = new( 0.92f, 0.12f, 0.72f );
	private static readonly Color Brick = new( 0.94f, 0.34f, 0.18f );
	private static readonly Color WallConcrete = new( 0.76f, 0.66f, 0.50f );
	private static readonly Color WetFilm = new( 0.42f, 0.78f, 0.96f );
	private static readonly Color FoamFilm = new( 0.82f, 0.88f, 0.58f );
	private static readonly Color Blood = new( 0.58f, 0.04f, 0.04f );
	private static readonly Color NeonBlue = new( 0.22f, 0.88f, 1f );
	private static readonly Color GasRed = new( 0.90f, 0.20f, 0.20f );
	private static readonly Color StoreBeige = new( 0.88f, 0.82f, 0.66f );

	public static readonly IReadOnlyList<JobDef> All = Build();

	static float Pay( int level ) => 1f + (level - 1) * 0.32f;

	static EnemySpawnDef E( EnemyKind kind, float x, float y ) =>
		new() { Kind = kind, Position = new Vector3( x, y, 0f ) };

	static PanelDef FlatGround( float w, float h, Color dirt, Color clean,
		PanelShape shape = PanelShape.Full, GrimePattern pattern = GrimePattern.Organic,
		Vector3? position = null, Angles? rotation = null,
		List<SurfaceSecret> secrets = null, List<GraffitiLine> graffiti = null, float cellSize = 13f ) =>
		new()
		{
			Position = position ?? new Vector3( 0f, 0f, 1f ),
			Rotation = rotation ?? default,
			Width = w,
			Height = h,
			CellSize = cellSize,
			Dirt = dirt,
			Clean = clean,
			Shape = shape,
			GrimePattern = pattern,
			Secrets = secrets,
			Graffiti = graffiti,
		};

	static PanelDef FlatWall( float w, float h, Color dirt, Color clean,
		PanelShape shape = PanelShape.Full, GrimePattern pattern = GrimePattern.Organic,
		Vector3? position = null,
		List<SurfaceSecret> secrets = null, List<GraffitiLine> graffiti = null, float cellSize = 13f ) =>
		new()
		{
			Position = position ?? new Vector3( 0f, 0f, 135f ),
			Rotation = new Angles( 0, 0, 90 ),
			Width = w,
			Height = h,
			CellSize = cellSize,
			Dirt = dirt,
			Clean = clean,
			Surface = CleanSurface.Pavement,
			Shape = shape,
			GrimePattern = pattern,
			Secrets = secrets,
			Graffiti = graffiti,
		};

	private static List<JobDef> Build()
	{
		var jobs = new List<JobDef>();

		// ── ACT I: BUSINESS AS USUAL (1–5) ──────────────────────────────────

		jobs.Add( new()
		{
			Name = "The Daily Grind",
			Blurb = "Blast mud and moss off a suburban driveway.",
			Briefing = "Your first paying gig — a neighbor's referral, nothing fancy. Deep tire tracks and lawn-care grime on a quiet suburban driveway. "
				+ "Be done before the HOA meeting this afternoon. Nobody's watching. Nobody cares. That's the whole job.",
			BriefingTag = "ACT I",
			ActTitle = "ACT I — BUSINESS AS USUAL",
			Location = "Suburban Residential Driveway & Sidewalk",
			CrimeScene = "None. It looks completely innocent.",
			RevealHook = "A campaign stencil beneath the moss: Vote Evelyn Vance for Senate.",
			ValueMultiplier = Pay( 1 ),
			Theme = MapTheme.Suburban,
			GroundColor = Grass,
			GroundSize = new Vector2( 1600f, 1600f ),
			SpawnPosition = new Vector3( 0f, -260f, 0f ),
			SpawnYaw = 90f,
			Panels = new List<PanelDef>
			{
				FlatGround( 320f, 520f, Grime, CleanConcrete, PanelShape.Driveway, GrimePattern.Organic, secrets: new List<SurfaceSecret>
				{
					new()
					{
						Text = "VOTE VANCE", X = 0.5f, Y = 0.62f, Scale = 2.8f,
						DiscoveryId = "L01_vance_stencil",
						Monologue = "Senator Vance's face stenciled into the sidewalk like she owns the pavement. I don't even know who she is yet — but her name's already under my boots.",
					},
					new() { Text = "CLEANER FUTURE", X = 0.5f, Y = 0.38f, Scale = 1.8f },
				} ),
				FlatGround( 84f, 360f, Grime, CleanConcrete, PanelShape.Strip, GrimePattern.Speckled,
					position: new Vector3( -208f, 36f, 1f ) ),
			},
			Props = new List<PropDef>
			{
				// Concrete apron — ties the cleanable pad to the garage mouth.
				new() { Position = new Vector3( 0f, 258f, 1f ), Rotation = default, Size = new Vector3( 320f, 36f, 2f ), Color = CleanConcrete },
			},
			Decor = new List<DecorDef>
			{
				new() { Kind = DecorKind.House, Position = new Vector3( 0f, 398f, 0f ), Yaw = 0f, Size = new Vector3( 560f, 280f, 170f ), Color = HouseWall },
				new() { Kind = DecorKind.Tree, Position = new Vector3( 340f, 260f, 0f ), Size = new Vector3( 1.2f, 1f, 1f ) },
				new() { Kind = DecorKind.Tree, Position = new Vector3( -360f, 180f, 0f ), Size = new Vector3( 1.05f, 1f, 1f ) },
				new() { Kind = DecorKind.Bush, Position = new Vector3( -170f, 250f, 0f ) },
				new() { Kind = DecorKind.Bush, Position = new Vector3( 175f, 255f, 0f ), Size = new Vector3( 1.15f, 1f, 1f ) },
				new() { Kind = DecorKind.Fence, Position = new Vector3( 520f, 300f, 0f ), Yaw = 90f, Size = new Vector3( 760f, 1f, 1f ) },
				new() { Kind = DecorKind.Fence, Position = new Vector3( -520f, 300f, 0f ), Yaw = 90f, Size = new Vector3( 760f, 1f, 1f ) },
				new() { Kind = DecorKind.Mailbox, Position = new Vector3( 250f, -220f, 0f ), Yaw = 20f },
			},
		} );

		jobs.Add( new()
		{
			Name = "Late Night at the Car Wash",
			Blurb = "Strip oil and grime from a commercial bay after hours.",
			Briefing = "Double shift at a local car wash — 1 AM, clogged mud, motor oil slicks, and gray grime caked on the bay walls. "
				+ "The pay is inexplicably high for a car wash. A trash can overflows with shredded bond paper and faint dark trails lead out of the bay.",
			BriefingTag = "ACT I",
			ActTitle = "ACT I — BUSINESS AS USUAL",
			Location = "Commercial Car Wash Bay",
			CrimeScene = "Shredded high-grade bond paper overflowing a trash can; faint dark trails leading out of the bay.",
			RevealHook = "A stenciled Aegis Tech Solutions logo beneath the grease on the plastic curtains.",
			ValueMultiplier = Pay( 2 ),
			Theme = MapTheme.GasStation,
			GroundColor = new Color( 0.54f, 0.50f, 0.42f ),
			GroundSize = new Vector2( 1700f, 1700f ),
			SpawnPosition = new Vector3( 0f, -300f, 0f ),
			SpawnYaw = 90f,
			Panels = new List<PanelDef>
			{
				FlatGround( 560f, 200f, Grime, CleanConcrete, PanelShape.CarBay, GrimePattern.Streaks, cellSize: 15f, secrets: new List<SurfaceSecret>
				{
					new()
					{
						Text = "AEGIS", X = 0.5f, Y = 0.58f, Scale = 3.2f,
						DiscoveryId = "L02_aegis_logo",
						Monologue = "Aegis Tech Solutions — laser-etched under a layer of grease like they've been here forever. Who pays triple rate to wash a logo back into view?",
					},
					new() { Text = "TECH", X = 0.5f, Y = 0.36f, Scale = 2.4f },
				} ),
			},
			Decor = new List<DecorDef>
			{
				new() { Kind = DecorKind.CarWashBay, Position = new Vector3( 0f, 20f, 0f ), Size = new Vector3( 600f, 220f, 130f ) },
				new() { Kind = DecorKind.GasCanopy, Position = new Vector3( -300f, 280f, 0f ), Size = new Vector3( 340f, 200f, 110f ), Color = GasRed },
				new() { Kind = DecorKind.Building, Position = new Vector3( 260f, 340f, 0f ), Size = new Vector3( 260f, 180f, 100f ), Color = StoreBeige },
			},
			Props = new List<PropDef>
			{
				new() { Position = new Vector3( 200f, -140f, 1f ), Size = new Vector3( 42f, 42f, 72f ), Color = new Color( 0.18f, 0.18f, 0.20f ) },
			},
			Enemies = new List<EnemySpawnDef>
			{
				E( EnemyKind.Rat, -90f, 30f ),
				E( EnemyKind.Rat, 110f, -50f ),
			},
		} );

		jobs.Add( new()
		{
			Name = "The Red Flag",
			Blurb = "Hose down a \"wine spill\" before the health inspection.",
			Briefing = "Anonymous burner-app job behind an upscale restaurant — a massive dried \"wine spill\" and kitchen grease blowout before morning inspection. "
				+ "Up close it's unmistakable: fan-shaped crimson blood six feet up the brick and pooling around the drain. I tell myself it's a prank. The alley doesn't laugh.",
			BriefingTag = "ACT I",
			ActTitle = "ACT I — BUSINESS AS USUAL",
			Location = "Restaurant Back Alley & Dumpster Pad",
			CrimeScene = "Fan-shaped blood splatter six feet up a brick wall, pooling heavily around a drainage grate.",
			RevealHook = "Permanent marker beneath the blood on the pavement: THEY KNOW.",
			ValueMultiplier = Pay( 3 ),
			Theme = MapTheme.Alley,
			GroundColor = new Color( 0.38f, 0.74f, 0.28f ),
			GroundSize = new Vector2( 1600f, 1600f ),
			SpawnPosition = new Vector3( 0f, -280f, 0f ),
			SpawnYaw = 90f,
			Panels = new List<PanelDef>
			{
				FlatWall( 480f, 260f, Blood, Brick, PanelShape.Banner, GrimePattern.Splatter, secrets: new List<SurfaceSecret>
				{
					new() { Text = "WINE?", X = 0.5f, Y = 0.72f, Scale = 2.2f },
				} ),
				FlatGround( 380f, 380f, Blood, CleanConcrete, PanelShape.OilSpill, GrimePattern.Splatter, secrets: new List<SurfaceSecret>
				{
					new()
					{
						Text = "THEY KNOW", X = 0.5f, Y = 0.52f, Scale = 3f,
						DiscoveryId = "L03_they_know",
						Monologue = "THEY KNOW — scrawled in marker under a lake of blood. I fish a shattered pair of expensive eyeglasses out of the drain. Pranks don't wear prescription lenses.",
					},
				} ),
			},
			Decor = new List<DecorDef>
			{
				new() { Kind = DecorKind.Restaurant, Position = new Vector3( 0f, 220f, 0f ), Size = new Vector3( 500f, 200f, 140f ), Color = Brick },
			},
			Props = new List<PropDef>
			{
				new() { Position = new Vector3( 80f, 100f, 1f ), Size = new Vector3( 64f, 64f, 8f ), Color = new Color( 0.34f, 0.36f, 0.38f ) },
			},
			Enemies = new List<EnemySpawnDef>
			{
				E( EnemyKind.Rat, -120f, -40f ),
				E( EnemyKind.Rat, 130f, 20f ),
				E( EnemyKind.Pigeon, 0f, 80f ),
			},
		} );

		jobs.Add( new()
		{
			Name = "Corporate Loading Dock",
			Blurb = "Wash tire skids and chemical residue off a loading platform.",
			Briefing = "The underground delivery bay of an Aegis Tech subsidiary — heavy tire skids, soot, and chemical residue on the concrete platform. "
				+ "Something massive was dragged across the floor. My phone buzzes: \"Just clean the soot. Ignore the walls. Bonus attached.\" That's hush money. I take it.",
			BriefingTag = "ACT I",
			ActTitle = "ACT I — BUSINESS AS USUAL",
			Location = "Aegis Tech Subsidiary Loading Dock",
			CrimeScene = "Panic-acceleration tire marks and dragging scars where something massive was pulled across the floor.",
			RevealHook = "Bullet holes riddled through brick beneath hastily applied industrial primer.",
			ValueMultiplier = Pay( 4 ),
			Theme = MapTheme.Industrial,
			GroundColor = new Color( 0.56f, 0.52f, 0.44f ),
			GroundSize = new Vector2( 1700f, 1700f ),
			SpawnPosition = new Vector3( 0f, -320f, 0f ),
			SpawnYaw = 90f,
			Panels = new List<PanelDef>
			{
				FlatGround( 580f, 220f, Grime, CleanConcrete, PanelShape.Platform, GrimePattern.Streaks,
					position: new Vector3( 0f, -30f, 1f ) ),
				FlatWall( 620f, 200f, new Color( 0.62f, 0.62f, 0.64f ), WallConcrete, PanelShape.Banner, GrimePattern.Rust, cellSize: 14f, secrets: new List<SurfaceSecret>
				{
					new()
					{
						Symbol = SecretSymbol.Scratches, X = 0.5f, Y = 0.52f, Scale = 1.4f,
						DiscoveryId = "L04_bullet_holes",
						Monologue = "Primer peels away and the wall underneath is Swiss cheese — tight clusters of bullet holes punched through brick. Soot is one thing. This is a cover-up.",
					},
					new() { Text = "IGNORE WALLS", X = 0.5f, Y = 0.24f, Scale = 1.6f },
				} ),
			},
			Decor = new List<DecorDef>
			{
				new() { Kind = DecorKind.Warehouse, Position = new Vector3( 0f, 300f, 0f ), Size = new Vector3( 620f, 240f, 150f ), Color = WallConcrete },
			},
			Enemies = new List<EnemySpawnDef>
			{
				E( EnemyKind.Pigeon, -80f, 50f ),
				E( EnemyKind.Rat, 100f, -60f ),
				E( EnemyKind.Rat, -140f, -90f ),
			},
		} );

		jobs.Add( new()
		{
			Name = "Politician's Garage",
			Blurb = "Clean a luxury SUV oil blowout at a secluded estate.",
			Briefing = "Private multi-car garage at a secluded estate — a massive oil blowout under an expensive luxury SUV. "
				+ "The interior's been bleach-wiped and the trunk lining cut out. This belongs to Senator Vance's top aide. Politics, corporations, and whatever happened in that trunk just shook hands.",
			BriefingTag = "ACT I",
			ActTitle = "ACT I — BUSINESS AS USUAL",
			Location = "Secluded Estate Luxury Garage",
			CrimeScene = "Bleach-wiped SUV interior; trunk lining completely cut out and removed.",
			RevealHook = "Neon-blue residue on the garage floor that water alone cannot wash away.",
			ValueMultiplier = Pay( 5 ),
			Theme = MapTheme.ParkingGarage,
			GroundColor = new Color( 0.38f, 0.38f, 0.40f ),
			GroundSize = new Vector2( 1800f, 1800f ),
			SpawnPosition = new Vector3( 0f, -340f, 0f ),
			SpawnYaw = 90f,
			Panels = new List<PanelDef>
			{
				new()
				{
					Position = new Vector3( 0f, 0f, 1f ), Rotation = default,
					Width = 420f, Height = 420f, CellSize = 13f,
					Dirt = Grime, Clean = CleanConcrete,
					Shape = PanelShape.Ellipse, GrimePattern = GrimePattern.Speckled,
					FollowUp = ToolType.ScrubBrush, FollowUpColor = NeonBlue, FollowUpWet = true,
					Secrets = new List<SurfaceSecret>
					{
						new()
						{
							Symbol = SecretSymbol.Biohazard, X = 0.5f, Y = 0.56f, Scale = 1.3f, Color = NeonBlue,
							DiscoveryId = "L05_neon_residue",
							Monologue = "Neon-blue film that laughs at my hose — needs solvent to break. AEGIS's fingerprint on a senator's garage floor. I'm not washing driveways anymore.",
						},
						new() { Text = "AEGIS", X = 0.5f, Y = 0.30f, Scale = 2.2f, Color = NeonBlue },
					},
				},
			},
			Decor = new List<DecorDef>
			{
				new() { Kind = DecorKind.EstateGarage, Position = new Vector3( 0f, 320f, 0f ), Size = new Vector3( 540f, 260f, 160f ), Color = HouseWall },
				new() { Kind = DecorKind.Tree, Position = new Vector3( 380f, 200f, 0f ), Size = new Vector3( 1.1f, 1f, 1f ) },
				new() { Kind = DecorKind.Tree, Position = new Vector3( -400f, 160f, 0f ), Size = new Vector3( 1f, 1f, 1f ) },
			},
			Enemies = new List<EnemySpawnDef>
			{
				E( EnemyKind.Pigeon, -100f, 40f ),
				E( EnemyKind.Pigeon, 90f, -70f ),
				E( EnemyKind.Rat, 0f, -100f ),
				E( EnemyKind.Wasp, 160f, 60f ),
			},
		} );

		// ── ACT II: DECODING THE GRIME (6–10) ───────────────────────────────

		jobs.Add( new()
		{
			Name = "Subway Station 4",
			Blurb = "Scrub rust and graffiti off a closed transit platform.",
			Briefing = "Abandoned section of a subterranean platform — ordered to clean massive rust and graffiti stains off tracks and tiled walls. "
				+ "Bloody handprints trail along pristine white tile where someone dragged themselves before collapsing. Chemical masking spray tried to hide their last act.",
			BriefingTag = "ACT II",
			ActTitle = "ACT II — DECODING THE GRIME",
			Location = "Abandoned Subway Platform",
			CrimeScene = "Bloody handprints trailing along white tiled walls where a victim tried to drag themselves.",
			RevealHook = "A schematic map of hidden service tunnels left as a clean silhouette on the tile.",
			ValueMultiplier = Pay( 6 ),
			Theme = MapTheme.ParkingGarage,
			GroundColor = new Color( 0.40f, 0.40f, 0.42f ),
			GroundSize = new Vector2( 1700f, 1700f ),
			SpawnPosition = new Vector3( 0f, -300f, 0f ),
			SpawnYaw = 90f,
			Panels = new List<PanelDef>
			{
				FlatWall( 520f, 240f, Blood, new Color( 0.92f, 0.90f, 0.86f ), PanelShape.Rounded, GrimePattern.Speckled, secrets: new List<SurfaceSecret>
				{
					new()
					{
						Symbol = SecretSymbol.Compass, X = 0.5f, Y = 0.54f, Scale = 1.2f,
						DiscoveryId = "L06_tunnel_map",
						Monologue = "A compass rose and tunnel schematic burned into the tile — a whistleblower's last message. I wash it away for money and memorize every line.",
					},
					new() { Text = "SVC TUNNELS", X = 0.5f, Y = 0.26f, Scale = 1.6f },
				} ),
			},
			Decor = new List<DecorDef>
			{
				new() { Kind = DecorKind.SubwayStation, Position = new Vector3( 0f, 260f, 0f ), Size = new Vector3( 560f, 280f, 180f ) },
			},
			Enemies = new List<EnemySpawnDef>
			{
				E( EnemyKind.Rat, -110f, -50f ),
				E( EnemyKind.Rat, 120f, 30f ),
				E( EnemyKind.Pigeon, -60f, 80f ),
				E( EnemyKind.Wasp, 140f, -80f ),
			},
		} );

		jobs.Add( new()
		{
			Name = "Shipping Container Yard",
			Blurb = "Wash three unmarked containers in a foggy dockyard.",
			Briefing = "Foggy labyrinthine dockyard at 3 AM — wash the interior and exterior of three specific unmarked steel containers. "
				+ "The center container opens on a pool of blood mixed with broken zip-ties and discarded tactical gloves. I should walk away. I don't.",
			BriefingTag = "ACT II",
			ActTitle = "ACT II — DECODING THE GRIME",
			Location = "Foggy Shipping Container Yard",
			CrimeScene = "Pool of blood sloshing across container floorboards with broken zip-ties and tactical gloves.",
			RevealHook = "Laser-etched Aegis military serial number and a hidden USB compartment.",
			ValueMultiplier = Pay( 7 ),
			Theme = MapTheme.Industrial,
			GroundColor = new Color( 0.50f, 0.46f, 0.42f ),
			GroundSize = new Vector2( 1800f, 1800f ),
			SpawnPosition = new Vector3( 0f, -340f, 0f ),
			SpawnYaw = 90f,
			Panels = new List<PanelDef>
			{
				FlatWall( 280f, 340f, new Color( 0.48f, 0.30f, 0.22f ), new Color( 0.62f, 0.64f, 0.68f ), PanelShape.Strip, GrimePattern.Rust, secrets: new List<SurfaceSecret>
				{
					new()
					{
						Text = "AEGIS-7741", X = 0.5f, Y = 0.58f, Scale = 2.2f,
						DiscoveryId = "L07_aegis_serial",
						Monologue = "Military-grade serial number laser-etched into steel. A hidden compartment clicks open — encrypted USB drive, still warm. I'm not a cleaner anymore. I'm a thief.",
					},
					new() { Symbol = SecretSymbol.Interlock, X = 0.5f, Y = 0.32f, Scale = 0.9f },
				} ),
			},
			Decor = new List<DecorDef>
			{
				new() { Kind = DecorKind.ContainerYard, Position = new Vector3( 0f, 200f, 0f ), Size = new Vector3( 220f, 1f, 1f ) },
			},
			Enemies = new List<EnemySpawnDef>
			{
				E( EnemyKind.Rat, -130f, -40f ),
				E( EnemyKind.Pigeon, 100f, 60f ),
				E( EnemyKind.OilLeech, 0f, -80f ),
				E( EnemyKind.Raccoon, -80f, 90f ),
			},
		} );

		jobs.Add( new()
		{
			Name = "Luxury Yacht",
			Blurb = "Power-wash teak deck and cabin after a \"corporate party.\"",
			Briefing = "Private marina, onboard a silent superyacht — clean teak deck and plush cabin after a supposedly wild corporate party. "
				+ "Furniture stripped, bleach overwhelming, blood deep in the wood grain. Whatever happened here, someone paid to make it smell like cleaning products.",
			BriefingTag = "ACT II",
			ActTitle = "ACT II — DECODING THE GRIME",
			Location = "Private Marina Superyacht",
			CrimeScene = "Stripped cabin, overwhelming bleach smell, blood seeped deep into teak deck grain.",
			RevealHook = "Deck engraving: Property of the Department of Energy — Experimental Division.",
			ValueMultiplier = Pay( 8 ),
			Theme = MapTheme.Storefront,
			GroundColor = new Color( 0.56f, 0.52f, 0.44f ),
			GroundSize = new Vector2( 1600f, 1600f ),
			SpawnPosition = new Vector3( 0f, -270f, 0f ),
			SpawnYaw = 90f,
			Panels = new List<PanelDef>
			{
				new()
				{
					Position = new Vector3( 0f, 20f, 1f ), Rotation = default,
					Width = 500f, Height = 340f, CellSize = 12f,
					Dirt = Blood, Clean = WoodClean, Surface = CleanSurface.Wood,
					Shape = PanelShape.Deck, GrimePattern = GrimePattern.Organic,
					FollowUp = ToolType.ScrubBrush, FollowUpColor = FoamFilm,
					Secrets = new List<SurfaceSecret>
					{
						new()
						{
							Text = "DEPT ENERGY", X = 0.5f, Y = 0.58f, Scale = 2f,
							DiscoveryId = "L08_doe_engraving",
							Monologue = "Department of Energy — Experimental Division — carved around the deck like a government property tag. They're using my van as an untraceable civilian eraser.",
						},
						new() { Text = "EXPERIMENTAL", X = 0.5f, Y = 0.34f, Scale = 1.5f },
					},
				},
			},
			Decor = new List<DecorDef>
			{
				new() { Kind = DecorKind.Yacht, Position = new Vector3( 0f, 240f, 0f ), Size = new Vector3( 520f, 300f, 120f ), Color = WoodClean },
			},
			Enemies = new List<EnemySpawnDef>
			{
				E( EnemyKind.Pigeon, -90f, 50f ),
				E( EnemyKind.Wasp, 110f, -40f ),
				E( EnemyKind.Raccoon, -150f, -70f ),
				E( EnemyKind.Rat, 70f, 90f ),
			},
		} );

		jobs.Add( new()
		{
			Name = "Water Treatment",
			Blurb = "Contain a toxic sludge spill near main intake valves.",
			Briefing = "Industrial filtration room at the city's edge — clean a massive toxic chemical sludge spill near the main water intake valves. "
				+ "Hazmat suits in the trash, a security booth smashed and sprayed with familiar neon-blue chemical. This isn't one body anymore. This is the whole city.",
			BriefingTag = "ACT II",
			ActTitle = "ACT II — DECODING THE GRIME",
			Location = "City Water Treatment Facility",
			CrimeScene = "Discarded hazmat suits; smashed security booth sprayed with neon-blue chemical.",
			RevealHook = "Diagnostic screen beneath sludge: WARNING — UNKNOWN BIOTOXIN INJECTED INTO URBAN SUPPLY.",
			ValueMultiplier = Pay( 9 ),
			Theme = MapTheme.Industrial,
			GroundColor = new Color( 0.50f, 0.46f, 0.42f ),
			GroundSize = new Vector2( 1700f, 1700f ),
			SpawnPosition = new Vector3( 0f, -310f, 0f ),
			SpawnYaw = 90f,
			Panels = new List<PanelDef>
			{
				FlatWall( 560f, 200f, Moss, WallConcrete, PanelShape.Banner, GrimePattern.Speckled, cellSize: 14f, secrets: new List<SurfaceSecret>
				{
					new()
					{
						Text = "BIOTOXIN", X = 0.5f, Y = 0.60f, Scale = 2.6f, Color = NeonBlue,
						DiscoveryId = "L09_biotoxin_warning",
						Monologue = "BIOTOXIN — flashing on a diagnostic screen I had to blast clean to read. Unknown compound injected into the urban supply. They're testing something on us.",
					},
					new() { Text = "URBAN SUPPLY", X = 0.5f, Y = 0.32f, Scale = 1.6f },
				} ),
			},
			Decor = new List<DecorDef>
			{
				new() { Kind = DecorKind.WaterPlant, Position = new Vector3( 0f, 280f, 0f ), Size = new Vector3( 480f, 1f, 160f ), Color = WallConcrete },
			},
			Enemies = new List<EnemySpawnDef>
			{
				E( EnemyKind.OilLeech, -100f, 30f ),
				E( EnemyKind.OilLeech, 90f, -50f ),
				E( EnemyKind.Rat, -140f, -80f ),
				E( EnemyKind.Pigeon, 130f, 70f ),
				E( EnemyKind.Wasp, 0f, 100f ),
			},
		} );

		jobs.Add( new()
		{
			Name = "Safehouse",
			Blurb = "Scrub a \"vandalized\" cabin before it hits the market.",
			Briefing = "Remote snow-dusted cabin — sent to clean a heavily vandalized property before it goes on the market. "
				+ "Walls shredded by bullets, multiple blood pools on hardwood. As I wash the living room floor, a burner phone on the counter rings. Distorted voice: \"We know you took the drive, Leo.\"",
			BriefingTag = "ACT II",
			ActTitle = "ACT II — DECODING THE GRIME",
			Location = "Remote Woodland Safehouse",
			CrimeScene = "High-caliber shootout — bullet-shredded walls and multiple blood pools on hardwood.",
			RevealHook = "Chalk outline of a federal agent and an FBI badge inside a hidden floor safe.",
			ValueMultiplier = Pay( 10 ),
			Theme = MapTheme.Backyard,
			GroundColor = Grass,
			GroundSize = new Vector2( 1600f, 1600f ),
			SpawnPosition = new Vector3( 0f, -260f, 0f ),
			SpawnYaw = 90f,
			Panels = new List<PanelDef>
			{
				FlatGround( 520f, 280f, Blood, WoodClean, PanelShape.Banner, GrimePattern.Splatter, secrets: new List<SurfaceSecret>
				{
					new()
					{
						Symbol = SecretSymbol.Star, X = 0.5f, Y = 0.54f, Scale = 1.25f,
						DiscoveryId = "L10_fbi_badge",
						Monologue = "FBI badge outline under dried blood — an investigator who vanished. The phone rings while I'm still holding the nozzle. They know my name. The trap just sprung.",
					},
					new() { Text = "FEDERAL", X = 0.5f, Y = 0.28f, Scale = 2f },
				} ),
			},
			Decor = new List<DecorDef>
			{
				new() { Kind = DecorKind.Cabin, Position = new Vector3( 0f, 300f, 0f ), Size = new Vector3( 460f, 240f, 130f ) },
				new() { Kind = DecorKind.Tree, Position = new Vector3( 320f, 180f, 0f ), Size = new Vector3( 1.15f, 1f, 1f ) },
				new() { Kind = DecorKind.Tree, Position = new Vector3( -340f, 140f, 0f ), Size = new Vector3( 1.05f, 1f, 1f ) },
			},
			Enemies = new List<EnemySpawnDef>
			{
				E( EnemyKind.Raccoon, -100f, 40f ),
				E( EnemyKind.Raccoon, 110f, -30f ),
				E( EnemyKind.Rat, -150f, -90f ),
				E( EnemyKind.Pigeon, 80f, 90f ),
				E( EnemyKind.StickerBandit, 0f, -110f ),
			},
		} );

		// ── ACT III: THE HUNTED (11–15) ─────────────────────────────────────

		jobs.Add( new()
		{
			Name = "Dark Highway",
			Blurb = "Erase a crash scene before police arrive — clock is ticking.",
			Briefing = "Secluded highway underpass in a downpour — clean a fiery car crash before local police or emergency services arrive. "
				+ "Shattered windshield coated in blood, scorch mark from a localized explosion. Flashlights move in the woods. They're watching me work.",
			BriefingTag = "ACT III",
			ActTitle = "ACT III — THE HUNTED",
			Location = "Highway Underpass in Downpour",
			CrimeScene = "Blood-coated shattered windshield and a localized explosion scorch mark on asphalt.",
			RevealHook = "GPS coordinates scratched into the guardrail with a key.",
			TimeLimitSeconds = 420f,
			ValueMultiplier = Pay( 11 ),
			Theme = MapTheme.Alley,
			GroundColor = new Color( 0.38f, 0.74f, 0.28f ),
			GroundSize = new Vector2( 1700f, 1700f ),
			SpawnPosition = new Vector3( 0f, -320f, 0f ),
			SpawnYaw = 90f,
			Panels = new List<PanelDef>
			{
				FlatWall( 680f, 120f, Grime, WallConcrete, PanelShape.Banner, GrimePattern.Streaks, secrets: new List<SurfaceSecret>
				{
					new()
					{
						Text = "40.7128 N", X = 0.5f, Y = 0.60f, Scale = 2f,
						DiscoveryId = "L11_gps_coords",
						Monologue = "Coordinates scratched into the guardrail with a car key — the dead agent's partner tried to leave a breadcrumb. Penthouse downtown. Flashlights in the treeline. Hurry.",
					},
					new() { Text = "74.0060 W", X = 0.5f, Y = 0.36f, Scale = 2f },
				} ),
			},
			Decor = new List<DecorDef>
			{
				new() { Kind = DecorKind.HighwayUnderpass, Position = new Vector3( 0f, 200f, 0f ), Size = new Vector3( 700f, 320f, 160f ) },
			},
			Enemies = new List<EnemySpawnDef>
			{
				E( EnemyKind.Rat, -120f, -50f ),
				E( EnemyKind.Pigeon, 100f, 60f ),
				E( EnemyKind.Wasp, -70f, 90f ),
				E( EnemyKind.StickerBandit, 0f, -100f ),
			},
		} );

		jobs.Add( new()
		{
			Name = "Server Farm",
			Blurb = "Clear coolant leak and fire soot from destroyed server racks.",
			Briefing = "Windowless data center — catastrophic coolant leak and heavy fire soot across rows of destroyed server racks. "
				+ "Servers bashed with axes, blood splattered across flashing LED panels. I'm erasing the evidence of a free-press execution.",
			BriefingTag = "ACT III",
			ActTitle = "ACT III — THE HUNTED",
			Location = "High-Tech Data Center",
			CrimeScene = "Servers bashed with axes; blood splattered across flashing LED control panels.",
			RevealHook = "Journalism outlet logo beneath charred soot: FREE PRESS.",
			ValueMultiplier = Pay( 12 ),
			Theme = MapTheme.Industrial,
			GroundColor = new Color( 0.50f, 0.46f, 0.42f ),
			GroundSize = new Vector2( 1700f, 1700f ),
			SpawnPosition = new Vector3( 0f, -300f, 0f ),
			SpawnYaw = 90f,
			Panels = new List<PanelDef>
			{
				FlatWall( 380f, 260f, new Color( 0.18f, 0.16f, 0.14f ), GlassClean, PanelShape.Window, GrimePattern.Speckled, secrets: new List<SurfaceSecret>
				{
					new()
					{
						Text = "FREE PRESS", X = 0.5f, Y = 0.54f, Scale = 2.8f,
						DiscoveryId = "L12_free_press",
						Monologue = "FREE PRESS — logo burned into the glass enclosure under charred soot. Independent journalism wiped out hours ago. I'm hosing down their tombstone.",
					},
				} ),
			},
			Decor = new List<DecorDef>
			{
				new() { Kind = DecorKind.DataCenter, Position = new Vector3( 0f, 260f, 0f ), Size = new Vector3( 540f, 280f, 150f ) },
			},
			Enemies = new List<EnemySpawnDef>
			{
				E( EnemyKind.Pigeon, -90f, 50f ),
				E( EnemyKind.Wasp, 120f, -40f ),
				E( EnemyKind.Rat, -140f, -70f ),
				E( EnemyKind.StickerBandit, 60f, 80f ),
				E( EnemyKind.RivalWasher, 0f, -120f ),
			},
		} );

		jobs.Add( new()
		{
			Name = "Penthouse Suite",
			Blurb = "High-pressure cleanup at the coordinates from the highway.",
			Briefing = "Breathtaking skyscraper penthouse — multi-room crime scene at the GPS coordinates from the highway job. "
				+ "Furniture shattered, mirrors smashed, blood trail to a broken panoramic window. The CEO was thrown out. I'm cleaning his office.",
			BriefingTag = "ACT III",
			ActTitle = "ACT III — THE HUNTED",
			Location = "Luxury Skyscraper Penthouse",
			CrimeScene = "Shattered furniture and mirrors; blood trail to a broken panoramic window.",
			RevealHook = "Laser-etched blueprint under desk varnish: PROJECT CLEAN SLATE.",
			ValueMultiplier = Pay( 13 ),
			Theme = MapTheme.UrbanPlaza,
			GroundColor = new Color( 0.58f, 0.52f, 0.40f ),
			GroundSize = new Vector2( 1800f, 1800f ),
			SpawnPosition = new Vector3( 0f, -350f, 0f ),
			SpawnYaw = 90f,
			Panels = new List<PanelDef>
			{
				new()
				{
					Position = new Vector3( 0f, 40f, 1f ), Rotation = default,
					Width = 540f, Height = 240f, CellSize = 12f,
					Dirt = Blood, Clean = WoodClean, Surface = CleanSurface.Wood,
					Shape = PanelShape.Banner, GrimePattern = GrimePattern.Splatter,
					FollowUp = ToolType.ScrubBrush, FollowUpColor = FoamFilm,
					Secrets = new List<SurfaceSecret>
					{
						new()
						{
							Text = "CLEAN SLATE", X = 0.5f, Y = 0.58f, Scale = 2.4f,
							DiscoveryId = "L13_clean_slate",
							Monologue = "PROJECT CLEAN SLATE — bioweapon disguised as eco-friendly water treatment. Vance funds it, Aegis builds it, DOE blesses it. The full plot, etched into an executive desk.",
						},
						new() { Text = "PROJECT", X = 0.5f, Y = 0.32f, Scale = 2f },
					},
				},
			},
			Decor = new List<DecorDef>
			{
				new() { Kind = DecorKind.Penthouse, Position = new Vector3( 0f, 320f, 0f ), Size = new Vector3( 520f, 300f, 200f ) },
			},
			Enemies = new List<EnemySpawnDef>
			{
				E( EnemyKind.RivalWasher, -90f, 40f ),
				E( EnemyKind.StickerBandit, 100f, -50f ),
				E( EnemyKind.Wasp, -130f, -80f ),
				E( EnemyKind.Pigeon, 70f, 90f ),
			},
		} );

		jobs.Add( new()
		{
			Name = "Meatpacking Plant",
			Blurb = "Scrub a scene hidden among hooks and animal waste.",
			Briefing = "Freezing industrial slaughterhouse — clean a gruesome scene hidden among animal waste and meat hooks. "
				+ "Human blood pools differently than animal blood on freezing metal. I can tell the difference now. That's not a skill I wanted.",
			BriefingTag = "ACT III",
			ActTitle = "ACT III — THE HUNTED",
			Location = "Industrial Meatpacking Plant",
			CrimeScene = "Human blood pooling in distinct patterns separate from animal blood on freezing floors.",
			RevealHook = "Hit list etched into stainless steel — VANCE PRESSURE WASHING at the bottom.",
			ValueMultiplier = Pay( 14 ),
			Theme = MapTheme.Industrial,
			GroundColor = new Color( 0.50f, 0.46f, 0.42f ),
			GroundSize = new Vector2( 1700f, 1700f ),
			SpawnPosition = new Vector3( 0f, -310f, 0f ),
			SpawnYaw = 90f,
			Panels = new List<PanelDef>
			{
				FlatWall( 220f, 300f, Grime, new Color( 0.78f, 0.80f, 0.82f ), PanelShape.Strip, GrimePattern.Streaks, cellSize: 12f, secrets: new List<SurfaceSecret>
				{
					new()
					{
						Text = "VANCE PRESSURE", X = 0.5f, Y = 0.22f, Scale = 1.8f,
						DiscoveryId = "L14_hit_list",
						Monologue = "Hit list etched in grease — scientists at the top, my business name freshly scratched at the bottom. Vance Pressure Washing. Same surname as the senator. I'm the loose end.",
					},
					new() { Text = "WASHING", X = 0.5f, Y = 0.12f, Scale = 1.6f },
				} ),
			},
			Decor = new List<DecorDef>
			{
				new() { Kind = DecorKind.MeatPlant, Position = new Vector3( 0f, 240f, 0f ), Size = new Vector3( 480f, 260f, 150f ) },
			},
			Enemies = new List<EnemySpawnDef>
			{
				E( EnemyKind.RivalWasher, -100f, 50f ),
				E( EnemyKind.RivalWasher, 90f, -40f ),
				E( EnemyKind.StickerBandit, 0f, -110f ),
				E( EnemyKind.Rat, -150f, -70f ),
				E( EnemyKind.Wasp, 130f, 80f ),
			},
		} );

		jobs.Add( new()
		{
			Name = "Ambush at the Shop",
			Blurb = "Your garage is trashed — hitmen wait in the shadows.",
			Briefing = "I come home to pack my things and find my shop trashed, walls spray-painted with targets. Hitmen waited in the shadows. "
				+ "I don't have a gun — I have my custom rig. Acid-wash strips their cloaking armor and I blast my way out. I'm not the cleaner anymore. I'm a combatant.",
			BriefingTag = "ACT III",
			ActTitle = "ACT III — THE HUNTED",
			Location = "Vance Pressure Washing HQ",
			CrimeScene = "Overturned equipment and target symbols spray-painted across your own walls.",
			RevealHook = "Industrial acid-wash melts cloaking armor off an elite assassin.",
			IsCombatLevel = true,
			ValueMultiplier = Pay( 15 ),
			Theme = MapTheme.GasStation,
			GroundColor = new Color( 0.54f, 0.50f, 0.42f ),
			GroundSize = new Vector2( 1800f, 1800f ),
			SpawnPosition = new Vector3( 0f, -360f, 0f ),
			SpawnYaw = 90f,
			Panels = new List<PanelDef>
			{
				FlatGround( 400f, 400f, Grime, CleanConcrete, PanelShape.OilSpill, GrimePattern.Splatter, graffiti: new List<GraffitiLine>
				{
					new()
					{
						Text = "TARGET", X = 0.5f, Y = 0.62f, Scale = 4f, Color = Blood,
						DiscoveryId = "L15_acid_reveal",
						Monologue = "Acid-wash across the floor and an assassin's cloaking armor melts off like wax. My rig was built to erase stains — turns out it erases people too.",
					},
				} ),
			},
			Decor = new List<DecorDef>
			{
				new() { Kind = DecorKind.PlayerShop, Position = new Vector3( 0f, 300f, 0f ), Size = new Vector3( 500f, 260f, 140f ) },
			},
			Enemies = new List<EnemySpawnDef>
			{
				E( EnemyKind.RivalWasher, -100f, 50f ),
				E( EnemyKind.RivalWasher, 100f, -40f ),
				E( EnemyKind.RivalWasher, 0f, 120f ),
				E( EnemyKind.StickerBandit, -140f, -80f ),
				E( EnemyKind.StickerBandit, 140f, -70f ),
				E( EnemyKind.Wasp, -60f, -120f ),
				E( EnemyKind.Wasp, 70f, -110f ),
			},
		} );

		// ── ACT IV: TACTICAL FORENSICS (16–25) ──────────────────────────────

		jobs.Add( new()
		{
			Name = "Abandoned Underground",
			Blurb = "Fight mercenaries while clearing bio-sludge to open drainage doors.",
			Briefing = "Crumbling city maintenance tunnels — fight corporate mercenaries while blasting bio-sludge blocks to open automated drainage doors. "
				+ "Remains of previous cleaning crews litter the tunnels. I'm not the first washer they hired. I'm the first one still breathing.",
			BriefingTag = "ACT IV",
			ActTitle = "ACT IV — TACTICAL FORENSICS",
			Location = "Old City Maintenance Tunnels",
			CrimeScene = "Remains of previous cleaning crews who failed litter the tunnels.",
			RevealHook = "Hidden graffiti map showing a secret flank path past a mercenary turret.",
			IsCombatLevel = true,
			ValueMultiplier = Pay( 16 ),
			Theme = MapTheme.ParkingGarage,
			GroundColor = new Color( 0.40f, 0.40f, 0.42f ),
			GroundSize = new Vector2( 1700f, 1700f ),
			SpawnPosition = new Vector3( 0f, -300f, 0f ),
			SpawnYaw = 90f,
			Panels = new List<PanelDef>
			{
				FlatWall( 480f, 480f, Moss, WallConcrete, PanelShape.LCorner, GrimePattern.Organic, secrets: new List<SurfaceSecret>
				{
					new()
					{
						Symbol = SecretSymbol.Compass, X = 0.5f, Y = 0.52f, Scale = 1.3f,
						DiscoveryId = "L16_tunnel_map",
						Monologue = "Old tunnel workers drew a flank route in grime — bypass the turret, hit them from the service shaft. My washer stuns. Their armor doesn't.",
					},
					new() { Text = "FLANK LEFT", X = 0.5f, Y = 0.26f, Scale = 1.8f },
				} ),
			},
			Decor = new List<DecorDef>
			{
				new() { Kind = DecorKind.SubwayStation, Position = new Vector3( 0f, 280f, 0f ), Size = new Vector3( 520f, 300f, 170f ) },
			},
			Enemies = new List<EnemySpawnDef>
			{
				E( EnemyKind.RivalWasher, -90f, 50f ),
				E( EnemyKind.RivalWasher, 90f, -40f ),
				E( EnemyKind.StickerBandit, -130f, -70f ),
				E( EnemyKind.StickerBandit, 130f, 60f ),
				E( EnemyKind.Rat, 0f, -100f ),
			},
		} );

		jobs.Add( new()
		{
			Name = "Aegis Bio-Lab",
			Blurb = "Infiltrate Sector C for the antidote — melt locks with acid-wash.",
			Briefing = "Sterile hidden corporate laboratory — infiltrate to steal the physical antidote and evidence. Bodies of scientists and guards line the hallways. "
				+ "Blast doors locked down. The keypad code is written in blood on the observation glass.",
			BriefingTag = "ACT IV",
			ActTitle = "ACT IV — TACTICAL FORENSICS",
			Location = "Aegis Bio-Lab Sector C",
			CrimeScene = "Aftermath of an internal breach — bodies of scientists and guards in the hallways.",
			RevealHook = "Door code 7734 written in blood on the observation window.",
			IsCombatLevel = true,
			ValueMultiplier = Pay( 17 ),
			Theme = MapTheme.Industrial,
			GroundColor = new Color( 0.50f, 0.46f, 0.42f ),
			GroundSize = new Vector2( 1700f, 1700f ),
			SpawnPosition = new Vector3( 0f, -310f, 0f ),
			SpawnYaw = 90f,
			Panels = new List<PanelDef>
			{
				FlatWall( 300f, 200f, FoamFilm, GlassClean, PanelShape.Window, GrimePattern.Organic, secrets: new List<SurfaceSecret>
				{
					new()
					{
						Text = "7734", X = 0.5f, Y = 0.52f, Scale = 3.5f, Color = Blood,
						DiscoveryId = "L17_door_code",
						Monologue = "7734 — smeared in blood from inside the lab by a dying scientist. Digital logs say they picked me because my small business had no automated logging. Perfect ghost.",
					},
				} ),
			},
			Decor = new List<DecorDef>
			{
				new() { Kind = DecorKind.BioLab, Position = new Vector3( 0f, 260f, 0f ), Size = new Vector3( 460f, 240f, 140f ) },
			},
			Enemies = new List<EnemySpawnDef>
			{
				E( EnemyKind.RivalWasher, -100f, 50f ),
				E( EnemyKind.RivalWasher, 100f, -50f ),
				E( EnemyKind.StickerBandit, -140f, -60f ),
				E( EnemyKind.ContractBodyguard, 0f, 110f ),
				E( EnemyKind.Wasp, 150f, 70f ),
			},
		} );

		jobs.Add( new()
		{
			Name = "Chemical Warehouse",
			Blurb = "Fight snipers on catwalks; chain-react chemical barrels.",
			Briefing = "Massive industrial distribution center — mercenaries sniping from upper catwalks, glowing pools of raw neon-blue bioweapon on the floor. "
				+ "Blast the shipping manifest clean and read the delivery schedule: the compound ships to the city reservoir tonight.",
			BriefingTag = "ACT IV",
			ActTitle = "ACT IV — TACTICAL FORENSICS",
			Location = "Industrial Chemical Warehouse",
			CrimeScene = "Massive glowing pools of raw neon-blue bioweapon compound on warehouse floors.",
			RevealHook = "Shipping manifest: bioweapon deployed to the city reservoir tonight.",
			IsCombatLevel = true,
			ValueMultiplier = Pay( 18 ),
			Theme = MapTheme.Industrial,
			GroundColor = new Color( 0.50f, 0.46f, 0.42f ),
			GroundSize = new Vector2( 1800f, 1800f ),
			SpawnPosition = new Vector3( 0f, -340f, 0f ),
			SpawnYaw = 90f,
			Panels = new List<PanelDef>
			{
				FlatWall( 560f, 160f, Grime, WallConcrete, PanelShape.Banner, GrimePattern.Streaks, secrets: new List<SurfaceSecret>
				{
					new()
					{
						Text = "RESERVOIR TONIGHT", X = 0.5f, Y = 0.54f, Scale = 1.8f, Color = NeonBlue,
						DiscoveryId = "L18_reservoir_tonight",
						Monologue = "RESERVOIR TONIGHT — delivery schedule on the manifest clipboard. I disrupt the supply chain and Vance panics into a rushed deployment.",
					},
				} ),
			},
			Decor = new List<DecorDef>
			{
				new() { Kind = DecorKind.Warehouse, Position = new Vector3( 0f, 280f, 0f ), Size = new Vector3( 580f, 260f, 160f ), Color = WallConcrete },
			},
			Enemies = new List<EnemySpawnDef>
			{
				E( EnemyKind.RivalWasher, -110f, 60f ),
				E( EnemyKind.RivalWasher, 110f, -50f ),
				E( EnemyKind.RivalWasher, 0f, 130f ),
				E( EnemyKind.StickerBandit, -150f, -80f ),
				E( EnemyKind.StickerBandit, 150f, -70f ),
				E( EnemyKind.ContractBodyguard, -70f, -120f ),
			},
		} );

		jobs.Add( new()
		{
			Name = "Rail Yard Siege",
			Blurb = "Hijack a moving train of chemical shipments in a thunderstorm.",
			Briefing = "Open-air train yard at night in a violent thunderstorm — hijack a moving train carrying chemical shipments. "
				+ "Train cars leak bio-chemicals; railway workers who saw too much didn't make it off the tracks.",
			BriefingTag = "ACT IV",
			ActTitle = "ACT IV — TACTICAL FORENSICS",
			Location = "Open-Air Rail Yard",
			CrimeScene = "Train cars leaking bio-chemicals; bodies of railway workers who saw too much.",
			RevealHook = "Tracking frequency on the locomotive intercepts black-ops radio chatter.",
			IsCombatLevel = true,
			ValueMultiplier = Pay( 19 ),
			Theme = MapTheme.Industrial,
			GroundColor = new Color( 0.50f, 0.46f, 0.42f ),
			GroundSize = new Vector2( 1800f, 1800f ),
			SpawnPosition = new Vector3( 0f, -350f, 0f ),
			SpawnYaw = 90f,
			Panels = new List<PanelDef>
			{
				FlatWall( 240f, 360f, new Color( 0.48f, 0.30f, 0.22f ), new Color( 0.62f, 0.64f, 0.68f ), PanelShape.Strip, GrimePattern.Rust, secrets: new List<SurfaceSecret>
				{
					new()
					{
						Text = "FREQ 447.12", X = 0.5f, Y = 0.52f, Scale = 2f,
						DiscoveryId = "L19_tracking_freq",
						Monologue = "Tracking frequency etched into the locomotive — 447.12 MHz. My phone intercepts black-ops chatter. I know their defensive plan for the broadcast tower.",
					},
				} ),
			},
			Decor = new List<DecorDef>
			{
				new() { Kind = DecorKind.Locomotive, Position = new Vector3( 0f, 260f, 0f ), Size = new Vector3( 480f, 1f, 140f ) },
			},
			Enemies = new List<EnemySpawnDef>
			{
				E( EnemyKind.RivalWasher, -100f, 50f ),
				E( EnemyKind.RivalWasher, 100f, -40f ),
				E( EnemyKind.RivalWasher, -60f, -110f ),
				E( EnemyKind.ContractBodyguard, 80f, 100f ),
				E( EnemyKind.ContractBodyguard, 0f, -130f ),
				E( EnemyKind.StickerBandit, 150f, -80f ),
			},
		} );

		jobs.Add( new()
		{
			Name = "Black Site",
			Blurb = "Breach an unregistered detention facility; rescue whistleblowers.",
			Briefing = "Unregistered underground government detention facility — breach to rescue surviving whistleblowers. "
				+ "A literal torture chamber; concrete permanently stained with layers of old and fresh blood. Steam vents blind whole rooms of guards.",
			BriefingTag = "ACT IV",
			ActTitle = "ACT IV — TACTICAL FORENSICS",
			Location = "Underground Black Site",
			CrimeScene = "Torture chamber — concrete permanently stained with layers of old and fresh blood.",
			RevealHook = "Structural blueprint on a pillar reveals a secret ventilation shaft bypass.",
			IsCombatLevel = true,
			ValueMultiplier = Pay( 20 ),
			Theme = MapTheme.ParkingGarage,
			GroundColor = new Color( 0.40f, 0.40f, 0.42f ),
			GroundSize = new Vector2( 1700f, 1700f ),
			SpawnPosition = new Vector3( 0f, -320f, 0f ),
			SpawnYaw = 90f,
			Panels = new List<PanelDef>
			{
				FlatWall( 200f, 340f, Grime, WallConcrete, PanelShape.Strip, GrimePattern.Rust, secrets: new List<SurfaceSecret>
				{
					new()
					{
						Symbol = SecretSymbol.Crosshair, X = 0.5f, Y = 0.52f, Scale = 1.2f,
						DiscoveryId = "L20_vent_blueprint",
						Monologue = "Vent shaft blueprint on the pillar — bypass the ambush squad waiting around the corner. Lead scientist agrees to testify if I get him to a camera.",
					},
					new() { Text = "VENT SHAFT", X = 0.5f, Y = 0.26f, Scale = 1.8f },
				} ),
			},
			Decor = new List<DecorDef>
			{
				new() { Kind = DecorKind.BioLab, Position = new Vector3( 0f, 300f, 0f ), Size = new Vector3( 500f, 260f, 180f ) },
			},
			Enemies = new List<EnemySpawnDef>
			{
				E( EnemyKind.RivalWasher, -110f, 60f ),
				E( EnemyKind.RivalWasher, 110f, -50f ),
				E( EnemyKind.ContractBodyguard, -80f, -100f ),
				E( EnemyKind.ContractBodyguard, 90f, 110f ),
				E( EnemyKind.ContractBodyguard, 0f, -140f ),
				E( EnemyKind.StickerBandit, 150f, 40f ),
			},
		} );

		jobs.Add( new()
		{
			Name = "Broadcast Tower Exterior",
			Blurb = "Fight up scaffolding; blast jamming gear off satellite dishes.",
			Briefing = "Skyscraper roof and broadcasting antenna during a lightning storm — fight up exterior scaffolding and blast jamming equipment welded to satellite dishes. "
				+ "Original security force piled on the roof. I need the backup generator online.",
			BriefingTag = "ACT IV",
			ActTitle = "ACT IV — TACTICAL FORENSICS",
			Location = "Broadcast Tower Rooftop",
			CrimeScene = "Bodies of the building's original security force piled on the roof.",
			RevealHook = "Hidden override switch on the backup generator restores antenna power.",
			IsCombatLevel = true,
			ValueMultiplier = Pay( 21 ),
			Theme = MapTheme.UrbanPlaza,
			GroundColor = new Color( 0.58f, 0.52f, 0.40f ),
			GroundSize = new Vector2( 1700f, 1700f ),
			SpawnPosition = new Vector3( 0f, -300f, 0f ),
			SpawnYaw = 90f,
			Panels = new List<PanelDef>
			{
				FlatGround( 280f, 280f, Grime, CleanConcrete, PanelShape.Rounded, GrimePattern.Rust, secrets: new List<SurfaceSecret>
				{
					new()
					{
						Text = "OVERRIDE", X = 0.5f, Y = 0.54f, Scale = 2.6f,
						DiscoveryId = "L21_generator_override",
						Monologue = "Override switch under industrial grease on the backup generator — antenna power restored. Grid's ready to broadcast the stolen files to the whole world.",
					},
				} ),
			},
			Decor = new List<DecorDef>
			{
				new() { Kind = DecorKind.Building, Position = new Vector3( 0f, 300f, 0f ), Size = new Vector3( 420f, 220f, 180f ), Color = WallConcrete },
				new() { Kind = DecorKind.Sign, Position = new Vector3( 0f, 220f, 0f ), Yaw = 0f },
			},
			Enemies = new List<EnemySpawnDef>
			{
				E( EnemyKind.RivalWasher, -100f, 50f ),
				E( EnemyKind.RivalWasher, 100f, -40f ),
				E( EnemyKind.RivalWasher, 0f, 120f ),
				E( EnemyKind.ContractBodyguard, -140f, -70f ),
				E( EnemyKind.ContractBodyguard, 140f, -60f ),
				E( EnemyKind.Wasp, -60f, -120f ),
			},
		} );

		jobs.Add( new()
		{
			Name = "Broadcast Tower Interior",
			Blurb = "Hold the control room while evidence uploads to the public.",
			Briefing = "Television studios and control rooms — hold the line against a massive assault while data files upload. "
				+ "Pristine modern studio turned warzone: plaster dust, bullet holes, blood. Upload completes. The conspiracy leaks globally.",
			BriefingTag = "ACT IV",
			ActTitle = "ACT IV — TACTICAL FORENSICS",
			Location = "Broadcast Tower Control Room",
			CrimeScene = "Studio warzone — plaster dust, bullet holes, and blood across the set.",
			RevealHook = "Live security feed: Senator Vance fleeing toward the dam reservoir.",
			IsCombatLevel = true,
			ValueMultiplier = Pay( 22 ),
			Theme = MapTheme.Storefront,
			GroundColor = new Color( 0.56f, 0.52f, 0.44f ),
			GroundSize = new Vector2( 1700f, 1700f ),
			SpawnPosition = new Vector3( 0f, -310f, 0f ),
			SpawnYaw = 90f,
			Panels = new List<PanelDef>
			{
				FlatWall( 560f, 180f, new Color( 0.18f, 0.16f, 0.14f ), GlassClean, PanelShape.Banner, GrimePattern.Speckled, secrets: new List<SurfaceSecret>
				{
					new()
					{
						Text = "VANCE FLEEING", X = 0.5f, Y = 0.54f, Scale = 2f, Color = Blood,
						DiscoveryId = "L22_vance_fleeing",
						Monologue = "Live feed under the soot — VANCE FLEEING toward the dam reservoir. Upload's done. The world knows about Clean Slate. She's running her backup plan to poison the city.",
					},
				} ),
			},
			Decor = new List<DecorDef>
			{
				new() { Kind = DecorKind.DataCenter, Position = new Vector3( 0f, 280f, 0f ), Size = new Vector3( 560f, 300f, 160f ) },
			},
			Enemies = new List<EnemySpawnDef>
			{
				E( EnemyKind.RivalWasher, -110f, 60f ),
				E( EnemyKind.RivalWasher, 110f, -50f ),
				E( EnemyKind.RivalWasher, -50f, -120f ),
				E( EnemyKind.ContractBodyguard, 80f, 100f ),
				E( EnemyKind.ContractBodyguard, 0f, -140f ),
				E( EnemyKind.StickerBandit, 150f, -80f ),
				E( EnemyKind.StickerBandit, -150f, 50f ),
			},
		} );

		jobs.Add( new()
		{
			Name = "Senator Vance Gala",
			Blurb = "Infiltrate a museum fundraiser to corner the senator.",
			Briefing = "Grand historic museum hosting a high-society political fundraiser — infiltrate to corner Senator Vance. "
				+ "Marble floors and priceless statues shattered, blood sprayed as her elite security turns weapons on me. She escapes to a helicopter. Pumps at the reservoir are already running.",
			BriefingTag = "ACT IV",
			ActTitle = "ACT IV — TACTICAL FORENSICS",
			Location = "Historic Museum Political Gala",
			CrimeScene = "Shattered marble floors and statues; blood sprayed as private security engages you.",
			RevealHook = "Security key card beneath catering wine and blood on the exhibition plaque.",
			IsCombatLevel = true,
			ValueMultiplier = Pay( 23 ),
			Theme = MapTheme.UrbanPlaza,
			GroundColor = new Color( 0.58f, 0.52f, 0.40f ),
			GroundSize = new Vector2( 1800f, 1800f ),
			SpawnPosition = new Vector3( 0f, -340f, 0f ),
			SpawnYaw = 90f,
			Panels = new List<PanelDef>
			{
				FlatGround( 380f, 380f, Blood, CleanConcrete, PanelShape.Circle, GrimePattern.Splatter, secrets: new List<SurfaceSecret>
				{
					new()
					{
						Symbol = SecretSymbol.Star, X = 0.5f, Y = 0.52f, Scale = 1.1f,
						DiscoveryId = "L23_security_key",
						Monologue = "Security key card under wine and blood on the exhibition plaque — dropped by Vance on her way to the helicopter. Reservoir pumps are already running.",
					},
				} ),
			},
			Decor = new List<DecorDef>
			{
				new() { Kind = DecorKind.Museum, Position = new Vector3( 0f, 320f, 0f ), Size = new Vector3( 540f, 280f, 170f ) },
			},
			Enemies = new List<EnemySpawnDef>
			{
				E( EnemyKind.RivalWasher, -100f, 50f ),
				E( EnemyKind.RivalWasher, 100f, -40f ),
				E( EnemyKind.RivalWasher, 0f, 130f ),
				E( EnemyKind.ContractBodyguard, -140f, -80f ),
				E( EnemyKind.ContractBodyguard, 140f, -70f ),
				E( EnemyKind.ContractBodyguard, -70f, -120f ),
				E( EnemyKind.StickerBandit, 160f, 60f ),
			},
		} );

		jobs.Add( new()
		{
			Name = "Reservoir Infiltration",
			Blurb = "Stop automated pumps dumping lethal compound into the water.",
			Briefing = "Massive multi-tiered concrete dam — fight through mercenaries protecting automated pumps dumping the final lethal dose. "
				+ "Pristine water channels turning terrifying neon-blue. Clear bio-crust off emergency valves to reach manual override levers.",
			BriefingTag = "ACT IV",
			ActTitle = "ACT IV — TACTICAL FORENSICS",
			Location = "City Dam & Water Reservoir",
			CrimeScene = "Pristine water channels turning a terrifying glowing neon-blue.",
			RevealHook = "Manual override levers beneath toxic bio-crust on emergency release valves.",
			IsCombatLevel = true,
			ValueMultiplier = Pay( 24 ),
			Theme = MapTheme.Industrial,
			GroundColor = new Color( 0.50f, 0.46f, 0.42f ),
			GroundSize = new Vector2( 1800f, 1800f ),
			SpawnPosition = new Vector3( 0f, -360f, 0f ),
			SpawnYaw = 90f,
			Panels = new List<PanelDef>
			{
				FlatWall( 460f, 460f, Moss, WallConcrete, PanelShape.Cross, GrimePattern.Speckled, secrets: new List<SurfaceSecret>
				{
					new()
					{
						Text = "OVERRIDE", X = 0.5f, Y = 0.54f, Scale = 2.8f, Color = NeonBlue,
						DiscoveryId = "L24_valve_override",
						Monologue = "OVERRIDE levers under neon-blue bio-crust on the emergency valves. Neutralizing agents in my tanks — reverse the spread before the timer hits zero.",
					},
					new() { Text = "VALVES", X = 0.5f, Y = 0.28f, Scale = 2f },
				} ),
			},
			Decor = new List<DecorDef>
			{
				new() { Kind = DecorKind.Dam, Position = new Vector3( 0f, 280f, 0f ), Size = new Vector3( 520f, 1f, 180f ), Color = WallConcrete },
			},
			Enemies = new List<EnemySpawnDef>
			{
				E( EnemyKind.RivalWasher, -110f, 60f ),
				E( EnemyKind.RivalWasher, 110f, -50f ),
				E( EnemyKind.RivalWasher, -60f, -120f ),
				E( EnemyKind.RivalWasher, 70f, 110f ),
				E( EnemyKind.ContractBodyguard, -140f, -80f ),
				E( EnemyKind.ContractBodyguard, 140f, -70f ),
				E( EnemyKind.ContractBodyguard, 0f, -150f ),
				E( EnemyKind.StickerBandit, 160f, 50f ),
			},
		} );

		jobs.Add( new()
		{
			Name = "Ultimate Clean Slate",
			Blurb = "Final boss — strip the Commander's exosuit plating with your rig.",
			Briefing = "Core pump room of the dam — Aegis command center covered in blood, burning fuel, and neon-blue toxin. "
				+ "The Commander of the black-ops cleanup division fights in an armored exosuit with chemical flamethrowers. Blast the ballistic plating off and hit the glowing core. Last job. Biggest stain.",
			BriefingTag = "ACT IV",
			ActTitle = "ACT IV — TACTICAL FORENSICS",
			Location = "Dam Command Center / Aegis HQ",
			CrimeScene = "Command center covered in blood, burning fuel, and neon-blue toxin.",
			RevealHook = "Blast ballistic plating off the Commander's suit to expose the vulnerable core.",
			IsCombatLevel = true,
			ValueMultiplier = Pay( 25 ),
			Theme = MapTheme.Industrial,
			GroundColor = new Color( 0.50f, 0.46f, 0.42f ),
			GroundSize = new Vector2( 1800f, 1800f ),
			SpawnPosition = new Vector3( 0f, -360f, 0f ),
			SpawnYaw = 90f,
			Panels = new List<PanelDef>
			{
				FlatGround( 500f, 500f, Blood, CleanConcrete, PanelShape.Ring, GrimePattern.Splatter, cellSize: 12f, secrets: new List<SurfaceSecret>
				{
					new()
					{
						Symbol = SecretSymbol.Crosshair, X = 0.5f, Y = 0.54f, Scale = 1.5f, Color = NeonBlue,
						DiscoveryId = "L25_commander_core",
						Monologue = "Crosshair on the Commander's chest core — strip the plating, hit the glow, purge the last toxic pool. City saved. Conspiracy shattered. I pack up the washer and walk into sunrise.",
					},
					new() { Text = "COMMANDER", X = 0.5f, Y = 0.28f, Scale = 2.4f },
				} ),
			},
			Decor = new List<DecorDef>
			{
				new() { Kind = DecorKind.Dam, Position = new Vector3( 0f, 300f, 0f ), Size = new Vector3( 560f, 1f, 200f ), Color = NeonBlue },
				new() { Kind = DecorKind.WaterPlant, Position = new Vector3( -320f, 200f, 0f ), Size = new Vector3( 360f, 1f, 140f ) },
			},
			Enemies = new List<EnemySpawnDef>
			{
				E( EnemyKind.StrayDog, 0f, 140f ),
				E( EnemyKind.RivalWasher, -110f, 60f ),
				E( EnemyKind.RivalWasher, 110f, -50f ),
				E( EnemyKind.RivalWasher, -70f, -120f ),
				E( EnemyKind.RivalWasher, 80f, 100f ),
				E( EnemyKind.ContractBodyguard, -150f, -80f ),
				E( EnemyKind.ContractBodyguard, 150f, -70f ),
				E( EnemyKind.ContractBodyguard, 0f, -160f ),
				E( EnemyKind.StickerBandit, -160f, 50f ),
				E( EnemyKind.StickerBandit, 160f, 40f ),
				E( EnemyKind.Wasp, -50f, -140f ),
				E( EnemyKind.Wasp, 60f, -130f ),
			},
		} );

		return jobs;
	}
}
