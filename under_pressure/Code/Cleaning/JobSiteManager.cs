namespace UnderPressure;

/// <summary>
/// Builds the current job's geometry, tracks overall cleanliness, and pays cash as
/// cells are cleaned. Owns nothing persistent itself — it reads the wallet/upgrade/
/// prestige systems so payouts always reflect the latest multipliers.
/// </summary>
public sealed class JobSiteManager
{
	private readonly PlayerWallet _wallet;
	private readonly UpgradeSystem _upgrades;
	private readonly PrestigeSystem _prestige;
	private readonly SaveData _save;

	private GameObject _root;
	private readonly List<CleanableSurface> _surfaces = new();

	public JobDef Current { get; private set; }
	public int Index { get; private set; }
	public int TotalCells { get; private set; }
	public int CleanedCells { get; private set; }

	/// <summary>Sum of every cell's area factor — the job's total payable "value" units,
	/// independent of how large or small the dirt tiles are rendered.</summary>
	private double _jobCellValue;

	/// <summary>Enough of the job is clean to leave (return to the van). Reached at 99%.</summary>
	public bool IsComplete => TotalCells > 0 && Progress >= GameConstants.JobCompleteThreshold;

	/// <summary>Every last speck is gone — qualifies for the perfectionist bonus.</summary>
	public bool IsSpotless => TotalCells > 0 && CleanedCells >= TotalCells;

	public float Progress => TotalCells == 0 ? 0f : (float)CleanedCells / TotalCells;

	public Vector3 SpawnPosition { get; private set; }
	public float SpawnYaw { get; private set; }

	/// <summary>Bumped whenever a new job loads so the player knows to re-spawn.</summary>
	public int LoadGeneration { get; private set; }

	public JobSiteManager( SaveData save, PlayerWallet wallet, UpgradeSystem upgrades, PrestigeSystem prestige )
	{
		_save = save;
		_wallet = wallet;
		_upgrades = upgrades;
		_prestige = prestige;
	}

	public double CashPerCell => GameConstants.BaseCellValue
		* _upgrades.CashMultiplier
		* _upgrades.VanMultiplier
		* _prestige.Multiplier
		* (Current?.ValueMultiplier ?? 1.0);

	public void LoadJob( int index )
	{
		Index = ((index % JobCatalog.Jobs.Count) + JobCatalog.Jobs.Count) % JobCatalog.Jobs.Count;
		_save.JobIndex = Index;
		Current = JobCatalog.Get( Index );

		_root?.Destroy();
		_surfaces.Clear();
		CleanedCells = 0;
		TotalCells = 0;
		_jobCellValue = 0;

		_root = new GameObject( true, $"Job_{Current.Name}" );

		BuildWorld();
		BuildProps();
		BuildDecor();
		BuildPanels();

		SpawnPosition = Current.SpawnPosition;
		SpawnYaw = Current.SpawnYaw;
		LoadGeneration++;
	}

	public void AdvanceToNext() => LoadJob( Index + 1 );

	/// <summary>Dev/cheat: visually and mechanically finish every panel on this job.</summary>
	public void InstantComplete()
	{
		foreach ( var surface in _surfaces )
			surface.InstantClean();
	}

	/// <summary>Distinct tools the current job's panels require across all stages (for hints).</summary>
	public IEnumerable<ToolType> RequiredTools =>
		(Current?.Panels ?? new List<PanelDef>()).SelectMany( p => p.Tools ).Distinct();

	/// <summary>True if the equipped tool can clean at least one unfinished spot here.</summary>
	public bool ToolMatchesJob( ToolType tool ) => _surfaces.Any( s => s.HasWorkFor( tool ) );

	/// <summary>Award the completion bonus for the finished job. Call once.</summary>
	public double AwardCompletionBonus()
	{
		var bonus = _jobCellValue * CashPerCell * GameConstants.CompletionBonusFactor;
		_wallet.Earn( bonus );
		return bonus;
	}

	/// <summary>Award the one-time perfectionist bonus for a spotless 100% job. Call once.</summary>
	public double AwardPerfectBonus()
	{
		var bonus = _jobCellValue * CashPerCell * GameConstants.PerfectBonusFactor;
		_wallet.Earn( bonus );
		return bonus;
	}

	private void BuildWorld() => WorldMapBuilder.Build( _root, Current, Index );

	private void BuildProps()
	{
		foreach ( var prop in Current.Props )
		{
			var go = new GameObject( _root, true, "Prop" );
			go.WorldPosition = DepthLayers.LiftProp( prop.Position, prop.Rotation, prop.Size );
			go.WorldRotation = prop.Rotation.ToRotation();
			go.LocalScale = MeshPrimitives.BoxScale( prop.Size );
			var mr = go.Components.Create<ModelRenderer>();
			mr.Model = MeshPrimitives.Box;
			mr.MaterialOverride = GameMaterials.Concrete;
			mr.Tint = prop.Color;
		}
	}

	private void BuildDecor()
	{
		foreach ( var decor in Current.Decor )
			Scenery.Build( _root, decor );
	}

	private void BuildPanels()
	{
		foreach ( var panel in Current.Panels )
		{
			var go = new GameObject( _root, true, "Panel" );
			go.WorldPosition = DepthLayers.LiftPanel( panel.Position, panel.Rotation );
			go.WorldRotation = panel.Rotation.ToRotation();

			var cleanMat = panel.Surface switch
			{
				CleanSurface.Wood => GameMaterials.Wood,
				CleanSurface.Glass => GameMaterials.Metal,
				_ => GameMaterials.Concrete,
			};

			var surface = go.Components.Create<CleanableSurface>();
			surface.Setup( panel.Width, panel.Height, panel.CellSize, panel.Clean, cleanMat, panel.Stages(),
				panel.Graffiti, panel.Secrets, panel.Shape, panel.GrimePattern );

			// Price each cell by its area so smaller tiles pay proportionally less per cell,
			// keeping a job's total payout the same regardless of tile size.
			var areaFactor = surface.CellArea / GameConstants.ReferenceCellArea;

			surface.CellsCleaned += count =>
			{
				CleanedCells += count;
				_wallet.Earn( count * CashPerCell * areaFactor );
			};

			// Pests smear grime back on: progress drops, but no cash is clawed back.
			surface.CellsResoiled += count =>
			{
				CleanedCells = Math.Max( 0, CleanedCells - count );
			};

			TotalCells += surface.TotalCells;
			_jobCellValue += surface.TotalCells * areaFactor;
			_surfaces.Add( surface );
		}
	}
}
