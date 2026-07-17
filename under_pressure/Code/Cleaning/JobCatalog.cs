namespace UnderPressure;

/// <summary>Material style for the clean surface revealed under the grime.</summary>
public enum CleanSurface
{
	Pavement,
	Wood,
	Glass,
}

/// <summary>A single dirty panel within a job (floor slab, wall, window, etc.).</summary>
public sealed class PanelDef
{
	public Vector3 Position { get; init; }
	public Angles Rotation { get; init; }
	public float Width { get; init; }
	public float Height { get; init; }
	public float CellSize { get; init; } = 22f;
	public Color Dirt { get; init; }
	public Color Clean { get; init; }

	/// <summary>What the cleaned surface should look like (drives its material).</summary>
	public CleanSurface Surface { get; init; } = CleanSurface.Pavement;

	/// <summary>Silhouette within the panel bounds — keeps jobs from all reading as the same rectangle.</summary>
	public PanelShape Shape { get; init; } = PanelShape.Full;

	/// <summary>How grime is shaded when the layer is baked.</summary>
	public GrimePattern GrimePattern { get; init; } = GrimePattern.Organic;

	// Every panel is first pressure-washed (that strips the Dirt layer). Some surfaces then
	// leave a residue that a hand tool must clear: FollowUp names that tool, FollowUpColor its
	// tint (default a wet blue film), and FollowUpWet whether it renders as a thin translucent
	// film (true, e.g. squeegee water) or an opaque second layer.
	public ToolType? FollowUp { get; init; }
	public Color FollowUpColor { get; init; } = new( 0.55f, 0.8f, 1f );
	public bool FollowUpWet { get; init; } = true;

	/// <summary>Ordered cleaning layers for this panel: pressure wash first, then any follow-up.</summary>
	public IReadOnlyList<CleanStage> Stages()
	{
		var stages = new List<CleanStage>
		{
			new() { Tool = ToolType.PressureWasher, Color = Dirt, Wet = false },
		};

		if ( FollowUp is { } tool && tool != ToolType.PressureWasher )
			stages.Add( new CleanStage { Tool = tool, Color = FollowUpColor, Wet = FollowUpWet } );

		return stages;
	}

	/// <summary>Distinct tools needed to fully clean this panel.</summary>
	public IEnumerable<ToolType> Tools => Stages().Select( s => s.Tool ).Distinct();

	/// <summary>Optional spray-painted lettering on the surface beneath grime (uncovered while cleaning).</summary>
	public IReadOnlyList<GraffitiLine> Graffiti { get; init; }

	/// <summary>Carved/stenciled marks on the clean surface beneath the grime (revealed as you wash).</summary>
	public IReadOnlyList<SurfaceSecret> Secrets { get; init; }
}

/// <summary>A decorative, non-cleanable block to give the scene a sense of place.</summary>
public sealed class PropDef
{
	public Vector3 Position { get; init; }
	public Angles Rotation { get; init; }
	public Vector3 Size { get; init; }
	public Color Color { get; init; }
}

public sealed class JobDef
{
	public string Name { get; init; }
	public string Blurb { get; init; }
	/// <summary>Story copy shown on the pre-job briefing card after driving to the next site.</summary>
	public string Briefing { get; init; }
	/// <summary>Short story-phase label on the briefing card (e.g. ACT I).</summary>
	public string BriefingTag { get; init; }
	/// <summary>Full act heading shown on the briefing card.</summary>
	public string ActTitle { get; init; }
	/// <summary>Where this job takes place.</summary>
	public string Location { get; init; }
	/// <summary>What the player can see before washing — the obvious crime scene.</summary>
	public string CrimeScene { get; init; }
	/// <summary>What pressure-washing will uncover beneath the grime.</summary>
	public string RevealHook { get; init; }
	/// <summary>Optional countdown in seconds (Act III+ timed jobs).</summary>
	public float? TimeLimitSeconds { get; init; }
	/// <summary>Heavy combat expected — more pests, higher pressure.</summary>
	public bool IsCombatLevel { get; init; }
	public double ValueMultiplier { get; init; } = 1.0;
	public MapTheme Theme { get; init; } = MapTheme.Suburban;
	public Color GroundColor { get; init; }
	public Vector2 GroundSize { get; init; } = new( 1200f, 1200f );
	public Vector2 MapSize { get; init; } = new( GameConstants.DefaultMapSize, GameConstants.DefaultMapSize );
	public Vector3 WorkCenter { get; init; } = Vector3.Zero;
	public Vector3 SpawnPosition { get; init; }
	public float SpawnYaw { get; init; }
	public IReadOnlyList<PanelDef> Panels { get; init; } = new List<PanelDef>();
	public IReadOnlyList<PropDef> Props { get; init; } = new List<PropDef>();
	public IReadOnlyList<DecorDef> Decor { get; init; } = new List<DecorDef>();
	public IReadOnlyList<EnemySpawnDef> Enemies { get; init; } = new List<EnemySpawnDef>();
}

/// <summary>The 25-level story campaign jobs.</summary>
public static class JobCatalog
{
	public static IReadOnlyList<JobDef> Jobs => CampaignCatalog.All;

	/// <summary>Force catalog rebuild (call when entering / cycling the level viewer).</summary>
	public static void Reload() => CampaignCatalog.Rebuild();

	public static JobDef Get( int index )
	{
		var jobs = Jobs;
		return jobs[((index % jobs.Count) + jobs.Count) % jobs.Count];
	}
}
