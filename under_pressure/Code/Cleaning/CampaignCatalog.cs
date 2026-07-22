namespace UnderPressure;

/// <summary>The 25-level story campaign. Levels 1–3 live here; the remaining acts are
/// authored in the CampaignSitesAct* partial files.</summary>
public static partial class CampaignCatalog
{
	private static readonly Color CleanConcrete = new( 0.84f, 0.70f, 0.46f );
	private static readonly Color Grime = new( 0.52f, 0.36f, 0.10f );
	private static readonly Color HouseWall = new( 1f, 0.82f, 0.32f );
	private static readonly Color Grass = new( 0.42f, 0.84f, 0.10f );
	private static readonly Color TrailDark = new( 0.22f, 0.10f, 0.08f );
	private static readonly Color Blood = new( 0.58f, 0.06f, 0.08f );
	private static readonly Color AlleyAsphalt = new( 0.42f, 0.42f, 0.44f );

	// --- Shared site palette (Acts I–IV) ---
	private static readonly Color OldBlood = new( 0.34f, 0.05f, 0.06f );
	private static readonly Color NeonBlue = new( 0.16f, 0.72f, 0.95f );
	private static readonly Color ToxicGreen = new( 0.32f, 0.62f, 0.12f );
	private static readonly Color Soot = new( 0.14f, 0.13f, 0.13f );
	private static readonly Color OilBlack = new( 0.10f, 0.10f, 0.12f );
	private static readonly Color RustBrown = new( 0.46f, 0.26f, 0.14f );
	private static readonly Color PaleConcrete = new( 0.72f, 0.70f, 0.66f );
	private static readonly Color DarkFloor = new( 0.24f, 0.24f, 0.27f );
	private static readonly Color WhiteTile = new( 0.90f, 0.90f, 0.87f );
	private static readonly Color WhiteMarble = new( 0.92f, 0.90f, 0.86f );
	private static readonly Color TeakWood = new( 0.62f, 0.42f, 0.22f );
	private static readonly Color GlassDark = new( 0.10f, 0.12f, 0.16f );
	private static readonly Color SteelClean = new( 0.68f, 0.70f, 0.73f );
	private static readonly Color BollardYellow = new( 0.95f, 0.82f, 0.12f );
	private static readonly Color SafetyOrange = new( 0.95f, 0.42f, 0.08f );

	/// <summary>Live campaign list. Prefer <see cref="Rebuild"/> after hot-reload authoring so edits apply without a full domain restart.</summary>
	private static IReadOnlyList<JobDef> _all;

	public static IReadOnlyList<JobDef> All
	{
		get
		{
			_all ??= Build();
			return _all;
		}
	}

	/// <summary>Drop the cached job list so the next access rebuilds from code.</summary>
	public static void Rebuild() => _all = null;

	static float Pay( int level ) => 1f + (level - 1) * 0.32f;

	static EnemySpawnDef E( EnemyKind kind, float x, float y ) =>
		new() { Kind = kind, Position = new Vector3( x, y, 0f ) };

	static PanelDef FlatGround( float w, float h, Color dirt, Color clean,
		PanelShape shape = PanelShape.Full, GrimePattern pattern = GrimePattern.Organic,
		Vector3? position = null, Angles? rotation = null,
		List<SurfaceSecret> secrets = null, List<GraffitiLine> graffiti = null, float cellSize = 13f,
		CleanSurface surface = CleanSurface.Pavement,
		ToolType? followUp = null, Color? followUpColor = null, bool followUpWet = true ) =>
		new()
		{
			Position = position ?? new Vector3( 0f, 0f, 1f ),
			Rotation = rotation ?? default,
			Width = w,
			Height = h,
			CellSize = cellSize,
			Dirt = dirt,
			Clean = clean,
			Surface = surface,
			Shape = shape,
			GrimePattern = pattern,
			Secrets = secrets,
			Graffiti = graffiti,
			FollowUp = followUp,
			FollowUpColor = followUpColor ?? new Color( 0.55f, 0.8f, 1f ),
			FollowUpWet = followUpWet,
		};

	static PanelDef FlatWall( float w, float h, Color dirt, Color clean,
		PanelShape shape = PanelShape.Full, GrimePattern pattern = GrimePattern.Organic,
		Vector3? position = null, float yaw = 0f,
		CleanSurface surface = CleanSurface.Pavement,
		List<SurfaceSecret> secrets = null, List<GraffitiLine> graffiti = null, float cellSize = 13f,
		ToolType? followUp = null, Color? followUpColor = null, bool followUpWet = true ) =>
		new()
		{
			Position = position ?? new Vector3( 0f, 0f, 135f ),
			Rotation = new Angles( 0, yaw, 90 ),
			Width = w,
			Height = h,
			CellSize = cellSize,
			Dirt = dirt,
			Clean = clean,
			Surface = surface,
			Shape = shape,
			GrimePattern = pattern,
			Secrets = secrets,
			Graffiti = graffiti,
			FollowUp = followUp,
			FollowUpColor = followUpColor ?? new Color( 0.55f, 0.8f, 1f ),
			FollowUpWet = followUpWet,
		};

	/// <summary>Shorthand for a decorative box prop.</summary>
	static PropDef P( float x, float y, float z, float sx, float sy, float sz, Color color, Angles rotation = default ) =>
		new() { Position = new Vector3( x, y, z ), Rotation = rotation, Size = new Vector3( sx, sy, sz ), Color = color };

	/// <summary>Shorthand for a placed decoration.</summary>
	static DecorDef D( DecorKind kind, float x, float y, float yaw = 0f, Vector3? size = null, Color? color = null, float z = 0f ) =>
		new() { Kind = kind, Position = new Vector3( x, y, z ), Yaw = yaw, Size = size ?? Vector3.One, Color = color ?? Color.White };

	private static List<JobDef> Build()
	{
		var jobs = new List<JobDef>();

		// ── LEVEL 1 (authored — keep) ────────────────────────────────────────

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

		// ── LEVEL 2 (Foothills Gas & Go — cream store + canopy) ───────────────
		jobs.Add( new()
		{
			Name = "Late Night at the Car Wash",
			Blurb = "Hose the bay floor and scrub the filthy side windows after hours.",
			Briefing = "Double shift at a local car wash — 1 AM. Motor oil and gray grime cake the tunnel floor; the side windows are fogged with soap film and road spray. "
				+ "The pay is inexplicably high for a car wash. A trash can overflows with shredded bond paper and faint dark trails lead out of the bay.",
			BriefingTag = "ACT I",
			ActTitle = "ACT I — BUSINESS AS USUAL",
			Location = "After-Hours Commercial Car Wash",
			CrimeScene = "Shredded high-grade bond paper overflowing a trash can; faint dark trails leading out of the bay.",
			RevealHook = "A stenciled Aegis Tech Solutions logo beneath the grease on the bay floor.",
			ValueMultiplier = Pay( 2 ),
			Theme = MapTheme.GasStation,
			GroundColor = new Color( 0.36f, 0.36f, 0.38f ),
			GroundSize = new Vector2( 1800f, 1800f ),
			SpawnPosition = new Vector3( 0f, -420f, 0f ),
			SpawnYaw = 90f,
			Panels = new List<PanelDef>
			{
				// One under-canopy wash pad (Aegis). Keep a gap from the approach so spots don't stack.
				FlatGround( 340f, 260f, Grime, CleanConcrete, PanelShape.Full, GrimePattern.Streaks,
					position: new Vector3( 0f, -80f, 0f ), cellSize: 13f, secrets: new List<SurfaceSecret>
					{
						new()
						{
							Text = "AEGIS", X = 0.5f, Y = 0.56f, Scale = 3.0f,
							DiscoveryId = "L02_aegis_logo",
							Monologue = "Aegis Tech Solutions — laser-etched under a layer of grease like they've been here forever. Who pays triple rate to wash a logo back into view?",
						},
						new() { Text = "TECH", X = 0.5f, Y = 0.34f, Scale = 2.2f },
					} ),
			},
			Decor = new List<DecorDef>
			{
				// Cream store + pump canopy (Size = store footprint).
				new() { Kind = DecorKind.FoothillsStation, Position = new Vector3( 0f, 160f, 0f ), Size = new Vector3( 480f, 200f, 130f ) },
				new() { Kind = DecorKind.Dumpster, Position = new Vector3( 340f, 120f, 0f ), Yaw = -15f, Size = new Vector3( 1.15f, 1f, 1f ) },
				new() { Kind = DecorKind.Tree, Position = new Vector3( 520f, 260f, 0f ), Size = new Vector3( 1.15f, 1f, 1f ) },
				new() { Kind = DecorKind.Tree, Position = new Vector3( -520f, 200f, 0f ), Size = new Vector3( 1.1f, 1f, 1f ) },
				new() { Kind = DecorKind.Bush, Position = new Vector3( -300f, -200f, 0f ) },
				new() { Kind = DecorKind.Bush, Position = new Vector3( 300f, -220f, 0f ), Size = new Vector3( 1.1f, 1f, 1f ) },
			},
			Props = new List<PropDef>
			{
				// ICE chest + boxes at store front.
				new() { Position = new Vector3( -120f, 50f, 1f ), Size = new Vector3( 80f, 40f, 46f ), Color = new Color( 0.92f, 0.92f, 0.94f ) },
				new() { Position = new Vector3( -60f, 48f, 1f ), Size = new Vector3( 36f, 28f, 24f ), Color = new Color( 0.62f, 0.48f, 0.28f ) },
				new() { Position = new Vector3( -60f, 48f, 26f ), Size = new Vector3( 32f, 24f, 20f ), Color = new Color( 0.55f, 0.42f, 0.24f ) },
				// Cone near door + tire stack on store right.
				new() { Position = new Vector3( 80f, 48f, 1f ), Size = new Vector3( 20f, 20f, 44f ), Color = new Color( 0.95f, 0.42f, 0.08f ) },
				new() { Position = new Vector3( 260f, 100f, 1f ), Size = new Vector3( 52f, 52f, 14f ), Color = new Color( 0.12f, 0.12f, 0.14f ) },
				new() { Position = new Vector3( 260f, 100f, 16f ), Size = new Vector3( 52f, 52f, 14f ), Color = new Color( 0.12f, 0.12f, 0.14f ) },
				new() { Position = new Vector3( 260f, 100f, 31f ), Size = new Vector3( 52f, 52f, 14f ), Color = new Color( 0.12f, 0.12f, 0.14f ) },
				new() { Position = new Vector3( 340f, 120f, 98f ), Size = new Vector3( 64f, 40f, 16f ), Color = new Color( 0.86f, 0.84f, 0.76f ) },
				new() { Position = new Vector3( 200f, 40f, 3f ), Size = new Vector3( 70f, 24f, 2f ), Color = TrailDark },
				new() { Position = new Vector3( 270f, 80f, 3f ), Size = new Vector3( 60f, 22f, 2f ), Color = TrailDark },
				new() { Position = new Vector3( -160f, -280f, 3f ), Size = new Vector3( 40f, 90f, 2f ), Color = new Color( 0.95f, 0.82f, 0.12f ) },
			},
			Enemies = new List<EnemySpawnDef>
			{
				E( EnemyKind.Rat, 320f, 100f ),
				E( EnemyKind.Rat, 280f, 60f ),
				E( EnemyKind.Rat, 40f, -40f ),
			},
		} );

		// ── LEVEL 3 (restaurant back alley — Laurent steakhouse rear) ─────────
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
			GroundColor = new Color( 0.34f, 0.34f, 0.36f ),
			GroundSize = new Vector2( 1600f, 1600f ),
			SpawnPosition = new Vector3( 0f, -380f, 0f ),
			SpawnYaw = 90f,
			Panels = new List<PanelDef>
			{
				// Blood pool in open asphalt beside the dumpster pad — keep clear of props.
				FlatGround( 170f, 150f, Blood, AlleyAsphalt, PanelShape.OilSpill, GrimePattern.Splatter,
					position: new Vector3( 155f, -110f, 1f ), cellSize: 12f, secrets: new List<SurfaceSecret>
					{
						new()
						{
							Text = "THEY KNOW", X = 0.5f, Y = 0.48f, Scale = 2.4f,
							DiscoveryId = "L03_they_know",
							Monologue = "Scrawled under the 'wine' like a dare. They know. Who's they — and how do they know me?",
						},
					} ),
				// Fan of blood on the wall beside the dumpster (left façade, natural height).
				// 4 units off the steakhouse face (y=100) so the panel's base layer clears it.
				FlatWall( 120f, 140f, Blood, new Color( 0.90f, 0.84f, 0.72f ), PanelShape.OilSpill, GrimePattern.Splatter,
					position: new Vector3( -95f, 96f, 110f ), yaw: 0f, cellSize: 12f ),
			},
			Decor = new List<DecorDef>
			{
				new() { Kind = DecorKind.SteakhouseAlley, Position = new Vector3( 0f, 180f, 0f ), Size = new Vector3( 720f, 160f, 150f ) },
				new() { Kind = DecorKind.Dumpster, Position = new Vector3( 20f, 40f, 0f ), Yaw = 0f, Size = new Vector3( 1.35f, 1f, 1f ) },
				new() { Kind = DecorKind.Fence, Position = new Vector3( -420f, 40f, 0f ), Yaw = 90f, Size = new Vector3( 520f, 1f, 1f ), Color = new Color( 0.55f, 0.38f, 0.22f ) },
				new() { Kind = DecorKind.Fence, Position = new Vector3( -280f, -200f, 0f ), Yaw = 0f, Size = new Vector3( 280f, 1f, 1f ), Color = new Color( 0.55f, 0.38f, 0.22f ) },
			},
			Props = new List<PropDef>
			{
				// Dumpster concrete pad + bollards.
				new() { Position = new Vector3( 20f, 40f, 1f ), Size = new Vector3( 160f, 120f, 4f ), Color = new Color( 0.70f, 0.70f, 0.68f ) },
				new() { Position = new Vector3( -50f, -5f, 1f ), Size = new Vector3( 18f, 18f, 48f ), Color = new Color( 0.95f, 0.82f, 0.12f ) },
				new() { Position = new Vector3( 90f, -5f, 1f ), Size = new Vector3( 18f, 18f, 48f ), Color = new Color( 0.95f, 0.82f, 0.12f ) },
				// Left clutter — kept low/forward so it doesn't cover the wall splash at X≈-95.
				new() { Position = new Vector3( -150f, 15f, 1f ), Size = new Vector3( 70f, 55f, 12f ), Color = new Color( 0.62f, 0.44f, 0.22f ) },
				new() { Position = new Vector3( -150f, 15f, 14f ), Size = new Vector3( 70f, 55f, 12f ), Color = new Color( 0.58f, 0.40f, 0.20f ) },
				new() { Position = new Vector3( -190f, -10f, 1f ), Size = new Vector3( 36f, 36f, 36f ), Color = new Color( 0.82f, 0.18f, 0.16f ) },
				new() { Position = new Vector3( -185f, 25f, 1f ), Size = new Vector3( 36f, 36f, 36f ), Color = new Color( 0.22f, 0.42f, 0.78f ) },
				new() { Position = new Vector3( -220f, 10f, 1f ), Size = new Vector3( 36f, 36f, 36f ), Color = new Color( 0.28f, 0.62f, 0.34f ) },
				new() { Position = new Vector3( -175f, -25f, 1f ), Size = new Vector3( 32f, 32f, 70f ), Color = new Color( 0.12f, 0.12f, 0.14f ) },
				// Bags/box on the right — past dumpster & away from wall splash.
				new() { Position = new Vector3( 230f, 85f, 1f ), Size = new Vector3( 40f, 28f, 28f ), Color = new Color( 0.12f, 0.12f, 0.14f ) },
				new() { Position = new Vector3( 265f, 70f, 1f ), Size = new Vector3( 36f, 26f, 24f ), Color = new Color( 0.14f, 0.14f, 0.16f ) },
				new() { Position = new Vector3( 245f, 100f, 1f ), Size = new Vector3( 30f, 24f, 20f ), Color = new Color( 0.62f, 0.48f, 0.28f ) },
				// Drain just south of the blood rim (pool reads as draining toward it).
				new() { Position = new Vector3( 155f, -195f, 2f ), Size = new Vector3( 48f, 48f, 3f ), Color = new Color( 0.28f, 0.28f, 0.30f ) },
				new() { Position = new Vector3( -340f, -80f, 1f ), Size = new Vector3( 40f, 40f, 60f ), Color = new Color( 0.22f, 0.48f, 0.78f ) },
				new() { Position = new Vector3( -300f, -100f, 1f ), Size = new Vector3( 36f, 36f, 55f ), Color = new Color( 0.12f, 0.12f, 0.14f ) },
				// Litter kept off the stain footprint.
				new() { Position = new Vector3( -80f, -200f, 2f ), Size = new Vector3( 18f, 12f, 2f ), Color = new Color( 0.90f, 0.90f, 0.88f ) },
				new() { Position = new Vector3( 20f, -230f, 2f ), Size = new Vector3( 14f, 10f, 2f ), Color = new Color( 0.88f, 0.88f, 0.86f ) },
			},
			Enemies = new List<EnemySpawnDef>
			{
				E( EnemyKind.Rat, 100f, 40f ),
				E( EnemyKind.Rat, -140f, 30f ),
				E( EnemyKind.Rat, -40f, -220f ),
			},
		} );

		// ── LEVELS 4–25 (authored in the CampaignSitesAct* partial files) ─────
		jobs.Add( Level04_LoadingDock() );
		jobs.Add( Level05_PoliticiansGarage() );
		jobs.Add( Level06_SubwayStation() );
		jobs.Add( Level07_ContainerYard() );
		jobs.Add( Level08_LuxuryYacht() );
		jobs.Add( Level09_WaterTreatment() );
		jobs.Add( Level10_Safehouse() );
		jobs.Add( Level11_DarkHighway() );
		jobs.Add( Level12_ServerFarm() );
		jobs.Add( Level13_Penthouse() );
		jobs.Add( Level14_MeatpackingPlant() );
		jobs.Add( Level15_AmbushAtTheShop() );
		jobs.Add( Level16_AbandonedUnderground() );
		jobs.Add( Level17_BioLab() );
		jobs.Add( Level18_ChemicalWarehouse() );
		jobs.Add( Level19_RailYardSiege() );
		jobs.Add( Level20_BlackSite() );
		jobs.Add( Level21_TowerRooftop() );
		jobs.Add( Level22_TowerStudio() );
		jobs.Add( Level23_MuseumGala() );
		jobs.Add( Level24_Reservoir() );
		jobs.Add( Level25_CleanSlate() );

		return jobs;
	}
}
