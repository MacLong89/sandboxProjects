namespace UnderPressure;

/// <summary>Act I closers — the jobs that stop being innocent (levels 4–5).</summary>
public static partial class CampaignCatalog
{
	/// <summary>Level 4 — Aegis subsidiary loading dock: skids, soot, and a primered-over wall.</summary>
	static JobDef Level04_LoadingDock() => new()
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
		GroundColor = new Color( 0.40f, 0.40f, 0.42f ),
		GroundSize = new Vector2( 1800f, 1600f ),
		SpawnPosition = new Vector3( 0f, -520f, 0f ),
		SpawnYaw = 90f,
		Panels = new List<PanelDef>
		{
			// Dock platform top — soot and drag scars (the job you were paid for).
			FlatGround( 600f, 170f, Soot, PaleConcrete, PanelShape.Platform, GrimePattern.Streaks,
				position: new Vector3( 0f, 128f, 49f ), secrets: new List<SurfaceSecret>
				{
					new()
					{
						Text = "AEGIS LOGISTICS", X = 0.5f, Y = 0.5f, Scale = 1.9f,
						DiscoveryId = "L04_aegis_stencil",
						Monologue = "Aegis again. Same logo as the car wash floor. Two jobs, two sites, one company signing both checks.",
					},
				} ),
			// Approach apron — panic skids arcing toward the ramp.
			FlatGround( 720f, 420f, OilBlack, AlleyAsphalt, PanelShape.Full, GrimePattern.Streaks,
				position: new Vector3( 0f, -180f, 1f ) ),
			// The wall they told me to ignore: fresh primer over pockmarks.
			FlatWall( 300f, 170f, new Color( 0.62f, 0.60f, 0.58f ), new Color( 0.48f, 0.50f, 0.53f ),
				PanelShape.Rounded, GrimePattern.Splatter,
				position: new Vector3( 700f, 204f, 130f ), secrets: new List<SurfaceSecret>
				{
					new()
					{
						Symbol = SecretSymbol.Scratches, X = 0.38f, Y = 0.55f, Scale = 2.2f,
						DiscoveryId = "L04_bullet_holes",
						Monologue = "That's not damage from a forklift. Nine-millimeter holes in a tight group, primered over the same morning. They paid me extra NOT to look at this.",
					},
					new() { Symbol = SecretSymbol.Scratches, X = 0.68f, Y = 0.42f, Scale = 1.6f },
				} ),
		},
		Decor = new List<DecorDef>
		{
			D( DecorKind.Warehouse, 0f, 430f, size: new Vector3( 900f, 400f, 260f ) ),
			D( DecorKind.Platform, 0f, 130f, size: new Vector3( 700f, 200f, 48f ) ),
			D( DecorKind.TiledWall, 700f, 220f, size: new Vector3( 360f, 24f, 220f ), color: new Color( 0.48f, 0.50f, 0.53f ) ),
			D( DecorKind.Container, -640f, 160f, yaw: 90f, size: new Vector3( 0.9f, 1f, 1f ), color: new Color( 0.24f, 0.42f, 0.56f ) ),
			D( DecorKind.BarrelCluster, 480f, -60f, yaw: 20f ),
			D( DecorKind.CrateStack, -420f, -120f, yaw: -15f ),
			D( DecorKind.Floodlight, -260f, -420f, yaw: 200f ),
			D( DecorKind.LampPost, 620f, -380f ),
			D( DecorKind.LampPost, -720f, -380f ),
			D( DecorKind.GuardRail, 0f, -740f, size: new Vector3( 1500f, 1f, 1f ) ),
			D( DecorKind.ConcreteBarrier, -620f, -600f, yaw: 25f, size: new Vector3( 240f, 1f, 1f ) ),
		},
		Props = new List<PropDef>
		{
			// Dock bumpers + a stray pallet.
			P( -180f, 232f, 30f, 60f, 14f, 24f, new Color( 0.12f, 0.12f, 0.14f ) ),
			P( 180f, 232f, 30f, 60f, 14f, 24f, new Color( 0.12f, 0.12f, 0.14f ) ),
			P( 360f, 60f, 6f, 90f, 70f, 12f, new Color( 0.58f, 0.44f, 0.26f ) ),
			// Dragging scar decals leading off the apron.
			P( -80f, -420f, 3f, 34f, 220f, 1f, TrailDark ),
			P( 40f, -480f, 3f, 26f, 180f, 1f, TrailDark ),
		},
		Enemies = new List<EnemySpawnDef>
		{
			E( EnemyKind.Rat, 420f, 40f ),
			E( EnemyKind.Rat, -380f, -60f ),
			E( EnemyKind.OilLeech, 80f, -260f ),
			E( EnemyKind.Pigeon, -140f, 300f ),
		},
	};

	/// <summary>Level 5 — estate garage: oil blowout under a bleach-wiped luxury SUV.</summary>
	static JobDef Level05_PoliticiansGarage() => new()
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
		Theme = MapTheme.Suburban,
		GroundColor = new Color( 0.34f, 0.68f, 0.14f ),
		GroundSize = new Vector2( 1600f, 1600f ),
		SpawnPosition = new Vector3( 60f, -520f, 0f ),
		SpawnYaw = 90f,
		Panels = new List<PanelDef>
		{
			// The blowout — and the first thing water can't touch.
			FlatGround( 300f, 260f, OilBlack, PaleConcrete, PanelShape.OilSpill, GrimePattern.Splatter,
				position: new Vector3( -40f, -20f, 11f ), followUp: ToolType.ScrubBrush,
				followUpColor: NeonBlue, followUpWet: false, secrets: new List<SurfaceSecret>
				{
					new()
					{
						Symbol = SecretSymbol.Biohazard, X = 0.5f, Y = 0.5f, Scale = 2.2f, Color = NeonBlue,
						DiscoveryId = "L05_neon_residue",
						Monologue = "Under the oil there's a blue sheen that laughs at the jet. It only lifts with the brush — and it glows faintly where the trunk was parked. What leaks out of a person and glows?",
					},
				} ),
			// Herringbone driveway down to the gate.
			FlatGround( 240f, 460f, Grime, CleanConcrete, PanelShape.Driveway, GrimePattern.Organic,
				position: new Vector3( 180f, -420f, 1f ) ),
		},
		Decor = new List<DecorDef>
		{
			D( DecorKind.House, 0f, 470f, size: new Vector3( 780f, 320f, 210f ), color: new Color( 0.92f, 0.88f, 0.80f ) ),
			D( DecorKind.Platform, 0f, 30f, size: new Vector3( 640f, 420f, 10f ), color: new Color( 0.66f, 0.64f, 0.60f ) ),
			D( DecorKind.LuxuryCar, -170f, 120f, yaw: -95f, size: new Vector3( 1.1f, 1f, 1f ), color: new Color( 0.06f, 0.07f, 0.09f ), z: 11f ),
			D( DecorKind.LuxuryCar, 210f, 130f, yaw: -85f, size: new Vector3( 1.05f, 1f, 1f ), color: new Color( 0.55f, 0.56f, 0.60f ), z: 11f ),
			D( DecorKind.LampPost, 420f, -180f, size: new Vector3( 0.9f, 1f, 1f ) ),
			D( DecorKind.LampPost, -420f, -180f, size: new Vector3( 0.9f, 1f, 1f ) ),
			D( DecorKind.Fence, 0f, -770f, size: new Vector3( 1500f, 1f, 1f ), color: new Color( 0.24f, 0.26f, 0.30f ) ),
			D( DecorKind.Fence, 740f, -200f, yaw: 90f, size: new Vector3( 1080f, 1f, 1f ), color: new Color( 0.24f, 0.26f, 0.30f ) ),
			D( DecorKind.Fence, -740f, -200f, yaw: 90f, size: new Vector3( 1080f, 1f, 1f ), color: new Color( 0.24f, 0.26f, 0.30f ) ),
			D( DecorKind.Tree, 480f, 300f, size: new Vector3( 1.4f, 1f, 1f ) ),
			D( DecorKind.Tree, -520f, 260f, size: new Vector3( 1.25f, 1f, 1f ) ),
			D( DecorKind.Tree, -560f, -420f, size: new Vector3( 1.1f, 1f, 1f ) ),
			D( DecorKind.Bush, 340f, 260f ),
			D( DecorKind.Bush, -340f, 270f, size: new Vector3( 1.2f, 1f, 1f ) ),
			D( DecorKind.Statue, 480f, -560f, yaw: 15f, size: new Vector3( 0.8f, 1f, 1f ) ),
			D( DecorKind.Mailbox, 320f, -660f, yaw: 10f ),
		},
		Props = new List<PropDef>
		{
			// Cut-out trunk lining dumped beside the SUV, bleach jugs, shop rags.
			P( -330f, 40f, 12f, 90f, 60f, 8f, new Color( 0.30f, 0.28f, 0.26f ) ),
			P( -300f, -90f, 12f, 24f, 24f, 34f, new Color( 0.92f, 0.92f, 0.94f ) ),
			P( -260f, -110f, 12f, 22f, 22f, 30f, new Color( 0.92f, 0.92f, 0.94f ) ),
			P( -240f, -60f, 12f, 30f, 22f, 6f, new Color( 0.85f, 0.30f, 0.25f ) ),
			// Garage doorway trim shadow on the house face.
			P( 0f, 308f, 100f, 620f, 4f, 12f, new Color( 0.30f, 0.28f, 0.26f ) ),
		},
		Enemies = new List<EnemySpawnDef>
		{
			E( EnemyKind.Raccoon, 420f, -80f ),
			E( EnemyKind.Rat, -420f, -220f ),
			E( EnemyKind.Wasp, 200f, 300f ),
		},
	};
}
