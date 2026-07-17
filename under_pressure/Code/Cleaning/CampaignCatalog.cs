namespace UnderPressure;

/// <summary>The 25-level story campaign. Level 1 is fully authored; levels 2–25 keep
/// narrative metadata only — physical sites are rebuilt from scratch one job at a time.</summary>
public static class CampaignCatalog
{
	private static readonly Color CleanConcrete = new( 0.84f, 0.70f, 0.46f );
	private static readonly Color Grime = new( 0.52f, 0.36f, 0.10f );
	private static readonly Color HouseWall = new( 1f, 0.82f, 0.32f );
	private static readonly Color Grass = new( 0.42f, 0.84f, 0.10f );
	private static readonly Color TrailDark = new( 0.22f, 0.10f, 0.08f );
	private static readonly Color Blood = new( 0.58f, 0.06f, 0.08f );
	private static readonly Color AlleyAsphalt = new( 0.42f, 0.42f, 0.44f );

	/// <summary>Blank play-pad color for unauthored stub sites.</summary>
	private static readonly Color StubPad = new( 0.50f, 0.48f, 0.44f );

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
		Vector3? position = null, float yaw = 0f,
		CleanSurface surface = CleanSurface.Pavement,
		List<SurfaceSecret> secrets = null, float cellSize = 13f ) =>
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
		};

	/// <summary>Narrative-only stub — empty pad, no panels/props/decor/enemies. Rebuilds go here.</summary>
	static JobDef StubSite(
		int level,
		string name,
		string blurb,
		string briefing,
		string briefingTag,
		string actTitle,
		string location,
		string crimeScene,
		string revealHook,
		bool combat = false,
		float? timeLimit = null ) =>
		new()
		{
			Name = name,
			Blurb = blurb,
			Briefing = briefing,
			BriefingTag = briefingTag,
			ActTitle = actTitle,
			Location = location,
			CrimeScene = crimeScene,
			RevealHook = revealHook,
			TimeLimitSeconds = timeLimit,
			IsCombatLevel = combat,
			ValueMultiplier = Pay( level ),
			Theme = MapTheme.UrbanPlaza,
			GroundColor = StubPad,
			GroundSize = new Vector2( 1400f, 1400f ),
			SpawnPosition = new Vector3( 0f, -260f, 0f ),
			SpawnYaw = 90f,
			Panels = new List<PanelDef>(),
			Props = new List<PropDef>(),
			Decor = new List<DecorDef>(),
			Enemies = new List<EnemySpawnDef>(),
		};

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
				FlatWall( 120f, 140f, Blood, new Color( 0.90f, 0.84f, 0.72f ), PanelShape.OilSpill, GrimePattern.Splatter,
					position: new Vector3( -95f, 99f, 110f ), yaw: 0f, cellSize: 12f ),
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

		// ── LEVELS 4–25: story kept, physical site wiped (rebuild from scratch) ─
		jobs.Add( StubSite( 4,
			"Corporate Loading Dock",
			"Wash tire skids and chemical residue off a loading platform.",
			"The underground delivery bay of an Aegis Tech subsidiary — heavy tire skids, soot, and chemical residue on the concrete platform. "
				+ "Something massive was dragged across the floor. My phone buzzes: \"Just clean the soot. Ignore the walls. Bonus attached.\" That's hush money. I take it.",
			"ACT I", "ACT I — BUSINESS AS USUAL",
			"Aegis Tech Subsidiary Loading Dock",
			"Panic-acceleration tire marks and dragging scars where something massive was pulled across the floor.",
			"Bullet holes riddled through brick beneath hastily applied industrial primer." ) );

		jobs.Add( StubSite( 5,
			"Politician's Garage",
			"Clean a luxury SUV oil blowout at a secluded estate.",
			"Private multi-car garage at a secluded estate — a massive oil blowout under an expensive luxury SUV. "
				+ "The interior's been bleach-wiped and the trunk lining cut out. This belongs to Senator Vance's top aide. Politics, corporations, and whatever happened in that trunk just shook hands.",
			"ACT I", "ACT I — BUSINESS AS USUAL",
			"Secluded Estate Luxury Garage",
			"Bleach-wiped SUV interior; trunk lining completely cut out and removed.",
			"Neon-blue residue on the garage floor that water alone cannot wash away." ) );

		jobs.Add( StubSite( 6,
			"Subway Station 4",
			"Scrub rust and graffiti off a closed transit platform.",
			"Abandoned section of a subterranean platform — ordered to clean massive rust and graffiti stains off tracks and tiled walls. "
				+ "Bloody handprints trail along pristine white tile where someone dragged themselves before collapsing. Chemical masking spray tried to hide their last act.",
			"ACT II", "ACT II — DECODING THE GRIME",
			"Abandoned Subway Platform",
			"Bloody handprints trailing along white tiled walls where a victim tried to drag themselves.",
			"A schematic map of hidden service tunnels left as a clean silhouette on the tile." ) );

		jobs.Add( StubSite( 7,
			"Shipping Container Yard",
			"Wash three unmarked containers in a foggy dockyard.",
			"Foggy labyrinthine dockyard at 3 AM — wash the interior and exterior of three specific unmarked steel containers. "
				+ "The center container opens on a pool of blood mixed with broken zip-ties and discarded tactical gloves. I should walk away. I don't.",
			"ACT II", "ACT II — DECODING THE GRIME",
			"Foggy Shipping Container Yard",
			"Pool of blood sloshing across container floorboards with broken zip-ties and tactical gloves.",
			"Laser-etched Aegis military serial number and a hidden USB compartment." ) );

		jobs.Add( StubSite( 8,
			"Luxury Yacht",
			"Power-wash teak deck and cabin after a \"corporate party.\"",
			"Private marina, onboard a silent superyacht — clean teak deck and plush cabin after a supposedly wild corporate party. "
				+ "Furniture stripped, bleach overwhelming, blood deep in the wood grain. Whatever happened here, someone paid to make it smell like cleaning products.",
			"ACT II", "ACT II — DECODING THE GRIME",
			"Private Marina Superyacht",
			"Stripped cabin, overwhelming bleach smell, blood seeped deep into teak deck grain.",
			"Deck engraving: Property of the Department of Energy — Experimental Division." ) );

		jobs.Add( StubSite( 9,
			"Water Treatment",
			"Contain a toxic sludge spill near main intake valves.",
			"Industrial filtration room at the city's edge — clean a massive toxic chemical sludge spill near the main water intake valves. "
				+ "Hazmat suits in the trash, a security booth smashed and sprayed with familiar neon-blue chemical. This isn't one body anymore. This is the whole city.",
			"ACT II", "ACT II — DECODING THE GRIME",
			"City Water Treatment Facility",
			"Discarded hazmat suits; smashed security booth sprayed with neon-blue chemical.",
			"Diagnostic screen beneath sludge: WARNING — UNKNOWN BIOTOXIN INJECTED INTO URBAN SUPPLY." ) );

		jobs.Add( StubSite( 10,
			"Safehouse",
			"Scrub a \"vandalized\" cabin before it hits the market.",
			"Remote snow-dusted cabin — sent to clean a heavily vandalized property before it goes on the market. "
				+ "Walls shredded by bullets, multiple blood pools on hardwood. As I wash the living room floor, a burner phone on the counter rings. Distorted voice: \"We know you took the drive, Leo.\"",
			"ACT II", "ACT II — DECODING THE GRIME",
			"Remote Woodland Safehouse",
			"High-caliber shootout — bullet-shredded walls and multiple blood pools on hardwood.",
			"Chalk outline of a federal agent and an FBI badge inside a hidden floor safe." ) );

		jobs.Add( StubSite( 11,
			"Dark Highway",
			"Erase a crash scene before police arrive — clock is ticking.",
			"Secluded highway underpass in a downpour — clean a fiery car crash before local police or emergency services arrive. "
				+ "Shattered windshield coated in blood, scorch mark from a localized explosion. Flashlights move in the woods. They're watching me work.",
			"ACT III", "ACT III — THE HUNTED",
			"Highway Underpass in Downpour",
			"Blood-coated shattered windshield and a localized explosion scorch mark on asphalt.",
			"GPS coordinates scratched into the guardrail with a key.",
			timeLimit: 420f ) );

		jobs.Add( StubSite( 12,
			"Server Farm",
			"Clear coolant leak and fire soot from destroyed server racks.",
			"Windowless data center — catastrophic coolant leak and heavy fire soot across rows of destroyed server racks. "
				+ "Servers bashed with axes, blood splattered across flashing LED panels. I'm erasing the evidence of a free-press execution.",
			"ACT III", "ACT III — THE HUNTED",
			"High-Tech Data Center",
			"Servers bashed with axes; blood splattered across flashing LED control panels.",
			"Journalism outlet logo beneath charred soot: FREE PRESS." ) );

		jobs.Add( StubSite( 13,
			"Penthouse Suite",
			"High-pressure cleanup at the coordinates from the highway.",
			"Breathtaking skyscraper penthouse — multi-room crime scene at the GPS coordinates from the highway job. "
				+ "Furniture shattered, mirrors smashed, blood trail to a broken panoramic window. The CEO was thrown out. I'm cleaning his office.",
			"ACT III", "ACT III — THE HUNTED",
			"Luxury Skyscraper Penthouse",
			"Shattered furniture and mirrors; blood trail to a broken panoramic window.",
			"Laser-etched blueprint under desk varnish: PROJECT CLEAN SLATE." ) );

		jobs.Add( StubSite( 14,
			"Meatpacking Plant",
			"Scrub a scene hidden among hooks and animal waste.",
			"Freezing industrial slaughterhouse — clean a gruesome scene hidden among animal waste and meat hooks. "
				+ "Human blood pools differently than animal blood on freezing metal. I can tell the difference now. That's not a skill I wanted.",
			"ACT III", "ACT III — THE HUNTED",
			"Industrial Meatpacking Plant",
			"Human blood pooling in distinct patterns separate from animal blood on freezing floors.",
			"Hit list etched into stainless steel — VANCE PRESSURE WASHING at the bottom." ) );

		jobs.Add( StubSite( 15,
			"Ambush at the Shop",
			"Your garage is trashed — hitmen wait in the shadows.",
			"I come home to pack my things and find my shop trashed, walls spray-painted with targets. Hitmen waited in the shadows. "
				+ "I don't have a gun — I have my custom rig. Acid-wash strips their cloaking armor and I blast my way out. I'm not the cleaner anymore. I'm a combatant.",
			"ACT III", "ACT III — THE HUNTED",
			"Vance Pressure Washing HQ",
			"Overturned equipment and target symbols spray-painted across your own walls.",
			"Industrial acid-wash melts cloaking armor off an elite assassin.",
			combat: true ) );

		jobs.Add( StubSite( 16,
			"Abandoned Underground",
			"Fight mercenaries while clearing bio-sludge to open drainage doors.",
			"Crumbling city maintenance tunnels — fight corporate mercenaries while blasting bio-sludge blocks to open automated drainage doors. "
				+ "Remains of previous cleaning crews litter the tunnels. I'm not the first washer they hired. I'm the first one still breathing.",
			"ACT IV", "ACT IV — TACTICAL FORENSICS",
			"Old City Maintenance Tunnels",
			"Remains of previous cleaning crews who failed litter the tunnels.",
			"Hidden graffiti map showing a secret flank path past a mercenary turret.",
			combat: true ) );

		jobs.Add( StubSite( 17,
			"Aegis Bio-Lab",
			"Infiltrate Sector C for the antidote — melt locks with acid-wash.",
			"Sterile hidden corporate laboratory — infiltrate to steal the physical antidote and evidence. Bodies of scientists and guards line the hallways. "
				+ "Blast doors locked down. The keypad code is written in blood on the observation glass.",
			"ACT IV", "ACT IV — TACTICAL FORENSICS",
			"Aegis Bio-Lab Sector C",
			"Aftermath of an internal breach — bodies of scientists and guards in the hallways.",
			"Door code 7734 written in blood on the observation window.",
			combat: true ) );

		jobs.Add( StubSite( 18,
			"Chemical Warehouse",
			"Fight snipers on catwalks; chain-react chemical barrels.",
			"Massive industrial distribution center — mercenaries sniping from upper catwalks, glowing pools of raw neon-blue bioweapon on the floor. "
				+ "Blast the shipping manifest clean and read the delivery schedule: the compound ships to the city reservoir tonight.",
			"ACT IV", "ACT IV — TACTICAL FORENSICS",
			"Industrial Chemical Warehouse",
			"Massive glowing pools of raw neon-blue bioweapon compound on warehouse floors.",
			"Shipping manifest: bioweapon deployed to the city reservoir tonight.",
			combat: true ) );

		jobs.Add( StubSite( 19,
			"Rail Yard Siege",
			"Hijack a moving train of chemical shipments in a thunderstorm.",
			"Open-air train yard at night in a violent thunderstorm — hijack a moving train carrying chemical shipments. "
				+ "Train cars leak bio-chemicals; railway workers who saw too much didn't make it off the tracks.",
			"ACT IV", "ACT IV — TACTICAL FORENSICS",
			"Open-Air Rail Yard",
			"Train cars leaking bio-chemicals; bodies of railway workers who saw too much.",
			"Tracking frequency on the locomotive intercepts black-ops radio chatter.",
			combat: true ) );

		jobs.Add( StubSite( 20,
			"Black Site",
			"Breach an unregistered detention facility; rescue whistleblowers.",
			"Unregistered underground government detention facility — breach to rescue surviving whistleblowers. "
				+ "A literal torture chamber; concrete permanently stained with layers of old and fresh blood. Steam vents blind whole rooms of guards.",
			"ACT IV", "ACT IV — TACTICAL FORENSICS",
			"Underground Black Site",
			"Torture chamber — concrete permanently stained with layers of old and fresh blood.",
			"Structural blueprint on a pillar reveals a secret ventilation shaft bypass.",
			combat: true ) );

		jobs.Add( StubSite( 21,
			"Broadcast Tower Exterior",
			"Fight up scaffolding; blast jamming gear off satellite dishes.",
			"Skyscraper roof and broadcasting antenna during a lightning storm — fight up exterior scaffolding and blast jamming equipment welded to satellite dishes. "
				+ "Original security force piled on the roof. I need the backup generator online.",
			"ACT IV", "ACT IV — TACTICAL FORENSICS",
			"Broadcast Tower Rooftop",
			"Bodies of the building's original security force piled on the roof.",
			"Hidden override switch on the backup generator restores antenna power.",
			combat: true ) );

		jobs.Add( StubSite( 22,
			"Broadcast Tower Interior",
			"Hold the control room while evidence uploads to the public.",
			"Television studios and control rooms — hold the line against a massive assault while data files upload. "
				+ "Pristine modern studio turned warzone: plaster dust, bullet holes, blood. Upload completes. The conspiracy leaks globally.",
			"ACT IV", "ACT IV — TACTICAL FORENSICS",
			"Broadcast Tower Control Room",
			"Studio warzone — plaster dust, bullet holes, and blood across the set.",
			"Live security feed: Senator Vance fleeing toward the dam reservoir.",
			combat: true ) );

		jobs.Add( StubSite( 23,
			"Senator Vance Gala",
			"Infiltrate a museum fundraiser to corner the senator.",
			"Grand historic museum hosting a high-society political fundraiser — infiltrate to corner Senator Vance. "
				+ "Marble floors and priceless statues shattered, blood sprayed as her elite security turns weapons on me. She escapes to a helicopter. Pumps at the reservoir are already running.",
			"ACT IV", "ACT IV — TACTICAL FORENSICS",
			"Historic Museum Political Gala",
			"Shattered marble floors and statues; blood sprayed as private security engages you.",
			"Security key card beneath catering wine and blood on the exhibition plaque.",
			combat: true ) );

		jobs.Add( StubSite( 24,
			"Reservoir Infiltration",
			"Stop automated pumps dumping lethal compound into the water.",
			"Massive multi-tiered concrete dam — fight through mercenaries protecting automated pumps dumping the final lethal dose. "
				+ "Pristine water channels turning terrifying neon-blue. Clear bio-crust off emergency valves to reach manual override levers.",
			"ACT IV", "ACT IV — TACTICAL FORENSICS",
			"City Dam & Water Reservoir",
			"Pristine water channels turning a terrifying glowing neon-blue.",
			"Manual override levers beneath toxic bio-crust on emergency release valves.",
			combat: true ) );

		jobs.Add( StubSite( 25,
			"Ultimate Clean Slate",
			"Final boss — strip the Commander's exosuit plating with your rig.",
			"Core pump room of the dam — Aegis command center covered in blood, burning fuel, and neon-blue toxin. "
				+ "The Commander of the black-ops cleanup division fights in an armored exosuit with chemical flamethrowers. Blast the ballistic plating off and hit the glowing core. Last job. Biggest stain.",
			"ACT IV", "ACT IV — TACTICAL FORENSICS",
			"Dam Command Center / Aegis HQ",
			"Command center covered in blood, burning fuel, and neon-blue toxin.",
			"Blast ballistic plating off the Commander's suit to expose the vulnerable core.",
			combat: true ) );

		return jobs;
	}
}
