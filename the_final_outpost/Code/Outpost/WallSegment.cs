namespace FinalOutpost;

/// <summary>One perimeter wall chunk with its own HP pool.</summary>
public sealed class WallSegment
{
	public GameObject Go;
	/// <summary>Tintable visual pieces (posts, rails, pickets). Damage feedback lerps each base tint.</summary>
	public List<(ModelRenderer Renderer, Color Base)> VisualParts { get; } = new();
	public Vector3 Center;
	/// <summary>Placement / visual box (full cell thickness × segment length).</summary>
	public Vector3 FootprintSize;
	/// <summary>Thin pathing / melee box — scaffold depth, not empty ground outside the bars.</summary>
	public Vector3 PathFootprintSize
	{
		get
		{
			var alongX = WallScaffoldVisual.RunsAlongX( Center, FootprintSize );
			var length = alongX ? FootprintSize.x : FootprintSize.y;
			var depth = GameConstants.WallPathDepth;
			return alongX
				? new Vector3( length, depth, 0f )
				: new Vector3( depth, length, 0f );
		}
	}
	/// <summary>Melee approach footprint — matches path occupancy / bar depth.</summary>
	public Vector3 ZombieCollisionFootprint => PathFootprintSize;
	public float Health;
	public float MaxHealth;
	/// <summary>Stable id ("x,y" of the segment centre) for persisting player-removed walls.</summary>
	public string Key;

	public bool IsBroken => Health <= 0f;
	public float HealthFraction => MaxHealth <= 0f ? 0f : Health / MaxHealth;

	public void SetMaxHealth( float max, bool healToFull )
	{
		if ( healToFull )
		{
			MaxHealth = max;
			Health = max;
		}
		else
		{
			var frac = HealthFraction;
			MaxHealth = max;
			Health = max * frac;
		}

		RefreshVisual();
	}

	public void Damage( float amount )
	{
		if ( IsBroken || amount <= 0f ) return;

		Health = MathF.Max( 0f, Health - amount );
		if ( IsBroken )
			DestructionFx.Burst( Center.WithZ( 0f ), 1.15f );

		RefreshVisual();
	}

	public void Repair( float amount )
	{
		Health = MathF.Min( MaxHealth, Health + amount );
		RefreshVisual();
	}

	public void SetHealth( float hp )
	{
		Health = MathF.Min( MaxHealth, MathF.Max( 0f, hp ) );
		RefreshVisual();
	}

	public void RepairToFull()
	{
		Health = MaxHealth;
		RefreshVisual();
	}

	public void RefreshVisual()
	{
		if ( Go is null ) return;

		if ( IsBroken )
		{
			Go.Enabled = false;
			SyncTileOccupancy();
			return;
		}

		Go.Enabled = true;
		var t = HealthFraction;
		var damaged = new Color( 0.5f, 0.15f, 0.12f );
		foreach ( var (mr, baseTint) in VisualParts )
		{
			if ( mr is null || !mr.IsValid() ) continue;
			mr.Tint = Color.Lerp( damaged, baseTint, t );
		}

		SyncTileOccupancy();
	}

	void SyncTileOccupancy()
	{
		var shouldBlock = !IsBroken && FootprintSize.Length > 0f;
		if ( shouldBlock == _blocksTiles )
			return;

		if ( shouldBlock )
			TileOccupancy.MarkWall( this );
		else
			TileOccupancy.UnmarkWall( this );

		_blocksTiles = shouldBlock;
		BuildManager.Instance?.RefreshWallMountHeights();
	}

	bool _blocksTiles;
}
