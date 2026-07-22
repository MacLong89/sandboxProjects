namespace UnderPressure;

/// <summary>Act II — decoding the grime (levels 6–10).</summary>
public static partial class CampaignCatalog
{
	/// <summary>Level 6 — abandoned subway platform: handprints on white tile, rust on the boards.</summary>
	static JobDef Level06_SubwayStation() => new()
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
		Theme = MapTheme.Underground,
		GroundColor = new Color( 0.46f, 0.45f, 0.44f ),
		GroundSize = new Vector2( 1900f, 1100f ),
		SpawnPosition = new Vector3( -700f, -320f, 0f ),
		SpawnYaw = 45f,
		Panels = new List<PanelDef>
		{
			// Drag trail along the tile — someone's last crawl.
			// 4 units off the tile face (y=498) so the panel's base layer clears the wall.
			FlatWall( 480f, 190f, Blood, WhiteTile, PanelShape.Full, GrimePattern.Splatter,
				position: new Vector3( -280f, 494f, 130f ), secrets: new List<SurfaceSecret>
				{
					new()
					{
						Symbol = SecretSymbol.Compass, X = 0.30f, Y = 0.55f, Scale = 2.2f,
						DiscoveryId = "L06_tunnel_map",
						Monologue = "The masking spray missed it: a service-tunnel schematic scratched into the tile, arrows pointing east. Whoever bled here was trying to leave a map for the next person. That's me.",
					},
					new() { Text = "EAST 7", X = 0.72f, Y = 0.40f, Scale = 1.6f },
				} ),
			// Rusted tag wall further down the platform.
			FlatWall( 480f, 190f, RustBrown, WhiteTile, PanelShape.Full, GrimePattern.Rust,
				position: new Vector3( 320f, 494f, 130f ), graffiti: new List<GraffitiLine>
				{
					new() { Text = "GHOST LINE", X = 0.5f, Y = 0.62f, Scale = 2.0f, Color = new Color( 0.85f, 0.20f, 0.45f ) },
					new() { Text = "NO TRAINS NO WITNESSES", X = 0.5f, Y = 0.34f, Scale = 1.1f, Color = new Color( 0.20f, 0.70f, 0.80f ) },
				} ),
			// Platform boards — decades of rust bleed and soot.
			FlatGround( 760f, 320f, RustBrown, PaleConcrete, PanelShape.Rounded, GrimePattern.Rust,
				position: new Vector3( 0f, 60f, 1f ) ),
		},
		Decor = new List<DecorDef>
		{
			D( DecorKind.TrackTrench, 0f, 350f, size: new Vector3( 1700f, 1f, 1f ) ),
			D( DecorKind.TiledWall, -280f, 510f, size: new Vector3( 560f, 24f, 260f ), color: WhiteTile ),
			D( DecorKind.TiledWall, 320f, 510f, size: new Vector3( 560f, 24f, 260f ), color: WhiteTile ),
			D( DecorKind.Column, -620f, 60f, size: new Vector3( 60f, 0f, 330f ), color: new Color( 0.62f, 0.62f, 0.60f ) ),
			D( DecorKind.Column, -160f, -180f, size: new Vector3( 60f, 0f, 330f ), color: new Color( 0.62f, 0.62f, 0.60f ) ),
			D( DecorKind.Column, 300f, -180f, size: new Vector3( 60f, 0f, 330f ), color: new Color( 0.62f, 0.62f, 0.60f ) ),
			D( DecorKind.Column, 700f, 60f, size: new Vector3( 60f, 0f, 330f ), color: new Color( 0.62f, 0.62f, 0.60f ) ),
			D( DecorKind.Floodlight, -520f, -220f, yaw: 40f ),
			D( DecorKind.Floodlight, 560f, -240f, yaw: -50f ),
			D( DecorKind.Rubble, 780f, 320f, yaw: 70f, size: new Vector3( 1.2f, 1f, 1f ) ),
			D( DecorKind.CrateStack, -800f, 200f, yaw: 30f, size: new Vector3( 0.9f, 1f, 1f ) ),
		},
		Props = new List<PropDef>
		{
			// Wooden benches and a dead vending machine.
			P( -420f, -60f, 1f, 150f, 44f, 46f, new Color( 0.34f, 0.26f, 0.18f ) ),
			P( 480f, -60f, 1f, 150f, 44f, 46f, new Color( 0.34f, 0.26f, 0.18f ) ),
			P( 820f, -100f, 1f, 70f, 50f, 170f, new Color( 0.55f, 0.12f, 0.14f ) ),
			// Discarded masking-spray canisters near the handprint wall.
			P( -160f, 400f, 1f, 16f, 16f, 30f, new Color( 0.75f, 0.75f, 0.78f ) ),
			P( -120f, 420f, 1f, 16f, 16f, 30f, new Color( 0.75f, 0.75f, 0.78f ), new Angles( 0, 30, 85 ) ),
		},
		Enemies = new List<EnemySpawnDef>
		{
			E( EnemyKind.Rat, -300f, 180f ),
			E( EnemyKind.Rat, 200f, 100f ),
			E( EnemyKind.Rat, 620f, -120f ),
			E( EnemyKind.OilLeech, -60f, 260f ),
		},
	};

	/// <summary>Level 7 — foggy dockside container yard, three unmarked boxes.</summary>
	static JobDef Level07_ContainerYard() => new()
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
		Theme = MapTheme.Waterfront,
		GroundColor = new Color( 0.38f, 0.40f, 0.42f ),
		GroundSize = new Vector2( 2000f, 1700f ),
		SpawnPosition = new Vector3( 0f, -620f, 0f ),
		SpawnYaw = 90f,
		Panels = new List<PanelDef>
		{
			// Blood sheet spilling out of the open center container.
			FlatGround( 220f, 260f, Blood, AlleyAsphalt, PanelShape.OilSpill, GrimePattern.Splatter,
				position: new Vector3( 0f, 30f, 1f ), cellSize: 12f, secrets: new List<SurfaceSecret>
				{
					new()
					{
						Text = "AEGIS-7734", X = 0.5f, Y = 0.55f, Scale = 1.7f,
						DiscoveryId = "L07_serial",
						Monologue = "A military serial number, laser-etched under the floorboards — and a machined compartment the size of a USB stick. Empty. Somebody got here first, or somebody left with it in their teeth.",
					},
					new() { Symbol = SecretSymbol.Interlock, X = 0.5f, Y = 0.28f, Scale = 1.5f },
				} ),
			// Rust bleeding down the west unmarked container. The container face is at y=152
			// with corrugation ribs reaching 149.5 — sit the panel clear in front of both.
			FlatWall( 210f, 90f, RustBrown, new Color( 0.42f, 0.44f, 0.46f ), PanelShape.Strip, GrimePattern.Rust,
				position: new Vector3( -330f, 145f, 52f ), cellSize: 11f ),
			// Salt grime on the east unmarked container.
			FlatWall( 210f, 90f, Grime, new Color( 0.42f, 0.44f, 0.46f ), PanelShape.Strip, GrimePattern.Streaks,
				position: new Vector3( 330f, 145f, 52f ), cellSize: 11f ),
			// Crane lane sludge running toward the quay.
			FlatGround( 900f, 240f, OilBlack, AlleyAsphalt, PanelShape.Banner, GrimePattern.Streaks,
				position: new Vector3( -100f, -380f, 1f ) ),
		},
		Decor = new List<DecorDef>
		{
			D( DecorKind.ContainerOpen, 0f, 200f, size: new Vector3( 1f, 1f, 1f ), color: new Color( 0.42f, 0.44f, 0.46f ) ),
			D( DecorKind.Container, -330f, 200f, size: new Vector3( 1f, 1f, 1f ), color: new Color( 0.42f, 0.44f, 0.46f ) ),
			D( DecorKind.Container, 330f, 200f, size: new Vector3( 1f, 1f, 1f ), color: new Color( 0.42f, 0.44f, 0.46f ) ),
			// Background stacks — the labyrinth.
			D( DecorKind.Container, -720f, 520f, yaw: 4f, color: new Color( 0.62f, 0.28f, 0.20f ) ),
			D( DecorKind.Container, -720f, 520f, yaw: 4f, color: new Color( 0.20f, 0.45f, 0.35f ), z: 101f ),
			D( DecorKind.Container, 700f, 540f, yaw: -6f, color: new Color( 0.75f, 0.55f, 0.15f ) ),
			D( DecorKind.Container, 260f, 620f, yaw: 90f, color: new Color( 0.30f, 0.32f, 0.55f ) ),
			D( DecorKind.Container, -820f, -200f, yaw: 90f, color: new Color( 0.55f, 0.20f, 0.22f ) ),
			D( DecorKind.Catwalk, 0f, 420f, size: new Vector3( 900f, 90f, 170f ) ),
			D( DecorKind.CrateStack, 560f, -100f, yaw: 20f ),
			D( DecorKind.BarrelCluster, -560f, -80f, yaw: -30f, color: new Color( 0.20f, 0.45f, 0.72f ) ),
			D( DecorKind.Floodlight, -180f, -180f, yaw: 25f ),
			D( DecorKind.Floodlight, 220f, -160f, yaw: -35f ),
			D( DecorKind.LampPost, -680f, -520f ),
			D( DecorKind.LampPost, 680f, -520f ),
		},
		Props = new List<PropDef>
		{
			// Zip-ties and tactical gloves at the container mouth.
			P( -60f, 110f, 1f, 20f, 14f, 3f, new Color( 0.90f, 0.90f, 0.88f ) ),
			P( 60f, 130f, 1f, 22f, 16f, 6f, new Color( 0.12f, 0.12f, 0.14f ) ),
			P( 90f, 90f, 1f, 22f, 16f, 6f, new Color( 0.12f, 0.12f, 0.14f ) ),
			// Fog-lamp glow pools (thin warm decals under lamps).
			P( -680f, -520f, 3f, 120f, 120f, 1f, new Color( 0.92f, 0.86f, 0.62f ) ),
			P( 680f, -520f, 3f, 120f, 120f, 1f, new Color( 0.92f, 0.86f, 0.62f ) ),
		},
		Enemies = new List<EnemySpawnDef>
		{
			E( EnemyKind.Rat, -260f, 60f ),
			E( EnemyKind.Rat, 300f, -60f ),
			E( EnemyKind.OilLeech, -100f, -320f ),
			E( EnemyKind.StickerBandit, 480f, 160f ),
		},
	};

	/// <summary>Level 8 — superyacht at a private marina: blood in the teak, film on the glass.</summary>
	static JobDef Level08_LuxuryYacht() => new()
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
		Theme = MapTheme.Waterfront,
		GroundColor = new Color( 0.52f, 0.50f, 0.46f ),
		GroundSize = new Vector2( 1500f, 1300f ),
		SpawnPosition = new Vector3( 0f, -480f, 0f ),
		SpawnYaw = 90f,
		Panels = new List<PanelDef>
		{
			// Teak deck — the grain drank the blood; brush it back out.
			FlatGround( 700f, 250f, OldBlood, TeakWood, PanelShape.Deck, GrimePattern.Splatter,
				position: new Vector3( 0f, 400f, 26f ), surface: CleanSurface.Wood,
				followUp: ToolType.ScrubBrush, followUpColor: new Color( 0.44f, 0.14f, 0.12f ), followUpWet: false,
				secrets: new List<SurfaceSecret>
				{
					new()
					{
						Text = "PROPERTY OF D.O.E.", X = 0.5f, Y = 0.58f, Scale = 1.6f,
						DiscoveryId = "L08_doe_engraving",
						Monologue = "Department of Energy — Experimental Division, engraved into a party yacht's deck. Since when does the DOE throw parties? Since when do their parties bleed?",
					},
					new() { Text = "EXPERIMENTAL DIV", X = 0.5f, Y = 0.36f, Scale = 1.2f },
				} ),
			// Cabin window band — soap film and salt spray, squeegee finish. The deckhouse
			// front face is at y=460 (window-band trim reaching 457.5); y=517 was inside it.
			FlatWall( 360f, 90f, new Color( 0.70f, 0.76f, 0.80f ), GlassDark, PanelShape.Banner, GrimePattern.Streaks,
				position: new Vector3( 0f, 453f, 106f ), surface: CleanSurface.Glass,
				followUp: ToolType.Squeegee, cellSize: 10f ),
			// Marina quay slick where the "catering" vans idled.
			FlatGround( 520f, 280f, Grime, AlleyAsphalt, PanelShape.Ellipse, GrimePattern.Organic,
				position: new Vector3( -380f, -140f, 1f ) ),
		},
		Decor = new List<DecorDef>
		{
			// Hull + deckhouse. Gangway steps lead up from the quay.
			D( DecorKind.Platform, 0f, 450f, size: new Vector3( 1000f, 380f, 24f ), color: new Color( 0.94f, 0.94f, 0.92f ) ),
			D( DecorKind.YachtCabin, 0f, 560f, size: new Vector3( 460f, 200f, 120f ), z: 24f ),
			D( DecorKind.Railing, 0f, 268f, size: new Vector3( 960f, 1f, 1f ), z: 24f ),
			D( DecorKind.Railing, -490f, 450f, yaw: 90f, size: new Vector3( 360f, 1f, 1f ), z: 24f ),
			D( DecorKind.Railing, 490f, 450f, yaw: 90f, size: new Vector3( 360f, 1f, 1f ), z: 24f ),
			D( DecorKind.Platform, 0f, 230f, size: new Vector3( 220f, 90f, 16f ), color: new Color( 0.80f, 0.80f, 0.78f ) ),
			D( DecorKind.Platform, 0f, 160f, size: new Vector3( 220f, 60f, 8f ), color: new Color( 0.74f, 0.74f, 0.72f ) ),
			// Quay dressing.
			D( DecorKind.LuxuryCar, 460f, -220f, yaw: 170f, color: new Color( 0.85f, 0.86f, 0.88f ) ),
			D( DecorKind.CrateStack, -620f, 60f, yaw: 15f, size: new Vector3( 0.85f, 1f, 1f ) ),
			D( DecorKind.Dumpster, 620f, 40f, yaw: -20f ),
			D( DecorKind.LampPost, -540f, -420f ),
			D( DecorKind.LampPost, 540f, -420f ),
			D( DecorKind.BarrelCluster, -640f, -380f, yaw: 45f, size: new Vector3( 0.8f, 1f, 1f ), color: new Color( 0.90f, 0.90f, 0.92f ) ),
		},
		Props = new List<PropDef>
		{
			// Stripped furniture piled on the quay under tarps.
			P( 300f, 20f, 1f, 160f, 90f, 60f, new Color( 0.36f, 0.40f, 0.46f ) ),
			P( 300f, 20f, 62f, 150f, 84f, 8f, new Color( 0.55f, 0.58f, 0.62f ) ),
			// Bleach jugs at the gangway.
			P( -140f, 180f, 1f, 22f, 22f, 32f, new Color( 0.92f, 0.92f, 0.94f ) ),
			P( -110f, 200f, 1f, 22f, 22f, 32f, new Color( 0.92f, 0.92f, 0.94f ) ),
		},
		Enemies = new List<EnemySpawnDef>
		{
			E( EnemyKind.Pigeon, -200f, 300f ),
			E( EnemyKind.Pigeon, 260f, 420f ),
			E( EnemyKind.Pigeon, 40f, -200f ),
			E( EnemyKind.Wasp, -420f, -60f ),
		},
	};

	/// <summary>Level 9 — water treatment plant: sludge at the intakes, neon warning beneath.</summary>
	static JobDef Level09_WaterTreatment() => new()
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
		GroundColor = new Color( 0.48f, 0.48f, 0.48f ),
		GroundSize = new Vector2( 1800f, 1500f ),
		SpawnPosition = new Vector3( 0f, -560f, 0f ),
		SpawnYaw = 90f,
		Panels = new List<PanelDef>
		{
			// The main sludge sheet, crusted where it dried.
			FlatGround( 540f, 440f, ToxicGreen, PaleConcrete, PanelShape.OilSpill, GrimePattern.Organic,
				position: new Vector3( -280f, 160f, 1f ), followUp: ToolType.ScrubBrush,
				followUpColor: new Color( 0.20f, 0.38f, 0.10f ), followUpWet: false,
				secrets: new List<SurfaceSecret>
				{
					new()
					{
						Symbol = SecretSymbol.Biohazard, X = 0.38f, Y = 0.60f, Scale = 2.4f,
						DiscoveryId = "L09_biotoxin",
						Monologue = "The floor screen under the sludge is still powered: WARNING — UNKNOWN BIOTOXIN INJECTED INTO URBAN SUPPLY. Injected. Present tense. This stopped being about bodies an hour ago.",
					},
					new() { Text = "BIOTOXIN ALERT", X = 0.55f, Y = 0.30f, Scale = 1.5f },
				} ),
			// Valve cluster — crusted in a cross around the intake wheels.
			FlatGround( 320f, 320f, new Color( 0.24f, 0.44f, 0.10f ), SteelClean, PanelShape.Cross, GrimePattern.Speckled,
				position: new Vector3( 380f, 160f, 1f ) ),
			// Smashed security booth face, sprayed neon.
			FlatWall( 240f, 150f, NeonBlue, new Color( 0.48f, 0.50f, 0.52f ), PanelShape.Rounded, GrimePattern.Splatter,
				position: new Vector3( 640f, -96f, 110f ), cellSize: 11f ),
		},
		Decor = new List<DecorDef>
		{
			D( DecorKind.MachineCore, 0f, 560f, size: new Vector3( 1.15f, 1f, 1f ), color: NeonBlue ),
			D( DecorKind.WaterChannel, 0f, -60f, size: new Vector3( 1300f, 150f, 1f ), color: new Color( 0.14f, 0.34f, 0.30f ) ),
			D( DecorKind.Catwalk, 320f, -60f, yaw: 90f, size: new Vector3( 420f, 80f, 150f ) ),
			D( DecorKind.PipeRun, -420f, 420f, size: new Vector3( 800f, 0f, 110f ), color: new Color( 0.30f, 0.55f, 0.60f ) ),
			D( DecorKind.PipeRun, 480f, 420f, size: new Vector3( 500f, 0f, 90f ), color: new Color( 0.55f, 0.58f, 0.62f ) ),
			D( DecorKind.ValveStation, 320f, 220f, size: new Vector3( 1.1f, 1f, 1f ) ),
			D( DecorKind.ValveStation, 440f, 160f, size: new Vector3( 1.0f, 1f, 1f ) ),
			D( DecorKind.ValveStation, 340f, 80f, size: new Vector3( 0.9f, 1f, 1f ) ),
			D( DecorKind.TiledWall, 640f, -80f, size: new Vector3( 260f, 24f, 200f ), color: new Color( 0.48f, 0.50f, 0.52f ) ),
			D( DecorKind.Generator, -640f, -220f, yaw: 10f ),
			D( DecorKind.BarrelCluster, -520f, -400f, yaw: 60f, color: new Color( 0.80f, 0.66f, 0.16f ) ),
			D( DecorKind.SteamVent, -80f, 300f ),
			D( DecorKind.Floodlight, 160f, -320f, yaw: -20f ),
			D( DecorKind.LampPost, -780f, -520f ),
		},
		Props = new List<PropDef>
		{
			// Dumped hazmat suits by the booth.
			P( 540f, -220f, 1f, 70f, 50f, 14f, new Color( 0.95f, 0.88f, 0.30f ) ),
			P( 600f, -260f, 1f, 60f, 44f, 12f, new Color( 0.95f, 0.88f, 0.30f ) ),
			P( 500f, -280f, 1f, 30f, 30f, 26f, new Color( 0.20f, 0.20f, 0.22f ) ),
			// Warning stripe decals at the channel crossings.
			P( 320f, 70f, 3f, 140f, 24f, 1f, BollardYellow ),
			P( 320f, -190f, 3f, 140f, 24f, 1f, BollardYellow ),
		},
		Enemies = new List<EnemySpawnDef>
		{
			E( EnemyKind.OilLeech, -260f, 120f ),
			E( EnemyKind.OilLeech, -420f, 280f ),
			E( EnemyKind.OilLeech, 320f, 40f ),
			E( EnemyKind.Rat, 560f, -320f ),
			E( EnemyKind.Rat, -600f, -120f ),
		},
	};

	/// <summary>Level 10 — snowbound safehouse: blood on hardwood, a phone that knows your name.</summary>
	static JobDef Level10_Safehouse() => new()
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
		Theme = MapTheme.Snowfield,
		GroundColor = new Color( 0.88f, 0.91f, 0.96f ),
		GroundSize = new Vector2( 1500f, 1500f ),
		SpawnPosition = new Vector3( 0f, -560f, 0f ),
		SpawnYaw = 90f,
		Panels = new List<PanelDef>
		{
			// Torn-up hardwood terrace — the "living room" they dragged outside.
			FlatGround( 420f, 190f, Blood, new Color( 0.50f, 0.34f, 0.18f ), PanelShape.Deck, GrimePattern.Splatter,
				position: new Vector3( 0f, 60f, 12f ), surface: CleanSurface.Wood,
				secrets: new List<SurfaceSecret>
				{
					new()
					{
						Symbol = SecretSymbol.Crosshair, X = 0.34f, Y = 0.52f, Scale = 2.2f,
						DiscoveryId = "L10_chalk_outline",
						Monologue = "Chalk outline under the blood — arms spread, badge-shaped shadow by the hip. Federal. Someone processed this scene, then hired me to erase their own paperwork.",
					},
					new() { Text = "FBI", X = 0.70f, Y = 0.38f, Scale = 1.8f },
				} ),
			// Bullet-shredded cabin face.
			FlatWall( 380f, 140f, new Color( 0.28f, 0.22f, 0.16f ), new Color( 0.48f, 0.33f, 0.20f ),
				PanelShape.Full, GrimePattern.Splatter,
				position: new Vector3( 0f, 142f, 120f ), surface: CleanSurface.Wood, cellSize: 12f ),
			// Muddy drag path through the snow to the tree line.
			FlatGround( 190f, 500f, TrailDark, new Color( 0.88f, 0.91f, 0.96f ), PanelShape.Strip, GrimePattern.Streaks,
				position: new Vector3( -80f, -380f, 1f ) ),
		},
		Decor = new List<DecorDef>
		{
			D( DecorKind.Cabin, 0f, 330f, size: new Vector3( 540f, 350f, 150f ) ),
			D( DecorKind.Platform, 0f, 70f, size: new Vector3( 460f, 220f, 10f ), color: new Color( 0.42f, 0.29f, 0.16f ) ),
			D( DecorKind.Fence, -380f, 100f, yaw: 90f, size: new Vector3( 620f, 1f, 1f ), color: new Color( 0.52f, 0.40f, 0.28f ) ),
			D( DecorKind.Fence, 380f, 100f, yaw: 90f, size: new Vector3( 620f, 1f, 1f ), color: new Color( 0.52f, 0.40f, 0.28f ) ),
			D( DecorKind.CrateStack, 460f, 260f, yaw: 12f, size: new Vector3( 0.9f, 1f, 1f ), color: new Color( 0.48f, 0.36f, 0.22f ) ),
			D( DecorKind.Tree, -480f, 380f, size: new Vector3( 1.5f, 1f, 1f ), color: new Color( 0.78f, 0.86f, 0.90f ) ),
			D( DecorKind.Tree, 560f, 420f, size: new Vector3( 1.3f, 1f, 1f ), color: new Color( 0.78f, 0.86f, 0.90f ) ),
			D( DecorKind.Tree, -560f, -160f, size: new Vector3( 1.2f, 1f, 1f ), color: new Color( 0.78f, 0.86f, 0.90f ) ),
			D( DecorKind.Tree, 520f, -300f, size: new Vector3( 1.35f, 1f, 1f ), color: new Color( 0.78f, 0.86f, 0.90f ) ),
			D( DecorKind.Floodlight, 260f, -140f, yaw: -30f ),
			D( DecorKind.WreckedCar, -520f, -480f, yaw: 40f, size: new Vector3( 0.95f, 1f, 1f ) ),
		},
		Props = new List<PropDef>
		{
			// The counter with the burner phone.
			P( 300f, 60f, 12f, 90f, 44f, 60f, new Color( 0.40f, 0.30f, 0.20f ) ),
			P( 300f, 50f, 74f, 14f, 8f, 3f, new Color( 0.08f, 0.08f, 0.10f ) ),
			// Shell casings glinting in the snow.
			P( 150f, -60f, 1f, 5f, 3f, 2f, new Color( 0.85f, 0.72f, 0.30f ) ),
			P( 190f, -30f, 1f, 5f, 3f, 2f, new Color( 0.85f, 0.72f, 0.30f ) ),
			P( 120f, -110f, 1f, 5f, 3f, 2f, new Color( 0.85f, 0.72f, 0.30f ) ),
			// Ripped-out floor safe beside the deck.
			P( -280f, 40f, 1f, 60f, 60f, 40f, new Color( 0.25f, 0.26f, 0.30f ) ),
		},
		Enemies = new List<EnemySpawnDef>
		{
			E( EnemyKind.Raccoon, 380f, -220f ),
			E( EnemyKind.Raccoon, -420f, 200f ),
			E( EnemyKind.Rat, -160f, -200f ),
		},
	};
}
