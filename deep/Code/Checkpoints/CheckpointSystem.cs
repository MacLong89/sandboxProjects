namespace Deep;

public sealed class CheckpointSystem : Component
{
	private readonly List<GameObject> _spawned = new();

	public IEnumerable<DivingBell> ActiveBells
	{
		get
		{
			foreach ( var go in _spawned )
			{
				if ( go is null || !go.IsValid() ) continue;
				var b = go.Components.Get<DivingBell>();
				if ( b is not null && b.IsValid() )
					yield return b;
			}
		}
	}

	public void RespawnAll( BalanceConfig balance )
	{
		Clear();
		var bed = SeabedTerrain.Instance;
		SpawnBell( bed, balance, "bell_mid", MathF.Min( 90f, balance.MaxOceanDepthMeters * 0.35f ), -14f );
		SpawnBell( bed, balance, "bell_deep", MathF.Min( 180f, balance.MaxOceanDepthMeters * 0.55f ), 16f );
		if ( balance.MaxOceanDepthMeters >= 280f )
			SpawnBell( bed, balance, "bell_abyss", MathF.Min( 280f, balance.MaxOceanDepthMeters - 40f ), -6f );
	}

	private void SpawnBell( SeabedTerrain bed, BalanceConfig balance, string id, float depth, float xBias )
	{
		var x = Math.Clamp( xBias, -balance.HorizontalHalfWidth + 6f, balance.HorizontalHalfWidth - 6f );
		Vector3 pos;
		if ( bed is not null )
		{
			var z = balance.WorldZFromDepth( depth );
			var floor = bed.FloorZAtX( x ) + bed.SwimClearance + 3f;
			if ( z < floor ) z = floor;
			pos = new Vector3( x, 0.2f, z );
		}
		else
		{
			pos = new Vector3( x, 0.2f, balance.WorldZFromDepth( depth ) );
		}

		var go = new GameObject( true, $"Checkpoint_{id}" );
		go.WorldPosition = pos;
		var bell = go.Components.Create<DivingBell>();
		bell.Setup( id, depth );
		_spawned.Add( go );
	}

	public void Clear()
	{
		foreach ( var go in _spawned )
		{
			if ( go.IsValid() )
				go.Destroy();
		}
		_spawned.Clear();
	}
}
