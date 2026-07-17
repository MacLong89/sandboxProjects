namespace Deep;

public sealed class StorySpawnSystem : Component
{
	private readonly List<GameObject> _spawned = new();

	public IEnumerable<StoryPickup> ActiveStories
	{
		get
		{
			foreach ( var go in _spawned )
			{
				if ( go is null || !go.IsValid() ) continue;
				var s = go.Components.Get<StoryPickup>();
				if ( s is not null && s.IsValid() && !s.Collected )
					yield return s;
			}
		}
	}

	public void RespawnAll( BalanceConfig balance )
	{
		Clear();
		var bed = SeabedTerrain.Instance;

		foreach ( var def in StoryCatalog.All )
		{
			if ( def.MinDepth >= balance.MaxOceanDepthMeters - 2f )
				continue;

			var depth = (def.MinDepth + MathF.Min( def.MaxDepth, balance.MaxOceanDepthMeters - 2f )) * 0.5f;
			var x = Math.Clamp( def.WorldX, -balance.HorizontalHalfWidth + 4f, balance.HorizontalHalfWidth - 4f );
			Vector3 pos;
			if ( bed is not null )
				pos = bed.GroundSprite( x, 1.6f, 0.08f );
			else
				pos = new Vector3( x, 0f, balance.WorldZFromDepth( depth ) );

			var go = new GameObject( true, $"Story_{def.Id}" );
			go.WorldPosition = pos;
			var pickup = go.Components.Create<StoryPickup>();
			pickup.Setup( def );
			_spawned.Add( go );
		}
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
