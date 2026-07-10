using System.Buffers;

namespace Sandbox;

/// <summary>
/// Host-scheduled premium supply drops (minimap POI + crate). Timing and footprint use nondeterministic RNG — not tied to <see cref="ThornsTerrainNetSpec.Seed"/> — so repeats of the same static world layout still vary events.
/// </summary>
[Title( "Thorns — Dynamic supply director" )]
[Category( "Thorns/World" )]
[Icon( "flight_takeoff" )]
public sealed class ThornsDynamicSupplyDirector : Component
{
	[Property] public bool SupplyEventsEnabled { get; set; } = true;

	[Property] public float FirstSpawnDelaySeconds { get; set; } = 60f;

	[Property] public float MinIntervalSeconds { get; set; } = 60f;

	[Property] public float MaxIntervalSeconds { get; set; } = 60f;

	[Property] public int MaxConcurrent { get; set; } = 1;

	[Property] public float ScatterEdgeInsetFraction { get; set; } = 0.07f;

	[Property] public float MinSeparationFromExistingWorld { get; set; } = 1840f;

	double _nextAttemptTime;
	int _placementSalt;

	protected override void OnStart()
	{
		if ( !Game.IsPlaying )
			return;

		_nextAttemptTime = Time.Now + Math.Max( 15f, FirstSpawnDelaySeconds );
	}

	protected override void OnFixedUpdate()
	{
		if ( !Game.IsPlaying || !SupplyEventsEnabled )
			return;

		if ( Networking.IsActive && !Networking.IsHost )
			return;

		if ( Time.Now < _nextAttemptTime )
			return;

		var scene = GameObject.Scene;
		if ( scene is null || !scene.IsValid() )
		{
			_nextAttemptTime = Time.Now + 30f;
			return;
		}

		if ( !TryCountActiveBeacons( scene, out var active ) )
		{
			_nextAttemptTime = Time.Now + 20f;
			return;
		}

		if ( active >= Math.Max( 1, MaxConcurrent ) )
		{
			_nextAttemptTime = Time.Now + 12f;
			return;
		}

		if ( !TrySpawnOne( scene ) )
		{
			_nextAttemptTime = Time.Now + 40f;
			return;
		}

		var span = Math.Max( 0f, MaxIntervalSeconds - MinIntervalSeconds );
		var gap = MinIntervalSeconds + Random.Shared.NextSingle() * span;
		_nextAttemptTime = Time.Now + gap;

		ThornsPoiAuthority.SpawnHostSingleton();
		ThornsPoiAuthority.Instance?.HostRebuildFromSceneMarkers();
		Log.Info( $"[Thorns] Dynamic supply drop spawned (next eligible after ~{gap:F0}s)." );
	}

	static bool TryCountActiveBeacons( Scene scene, out int count )
	{
		count = 0;
		foreach ( var b in scene.GetAllComponents<ThornsDynamicSupplyBeacon>() )
		{
			if ( b.IsValid() && b.Enabled )
				count++;
		}

		return true;
	}

	static ThornsTerrainChunk FindTerrainChunk( Scene scene )
	{
		foreach ( var c in scene.GetAllComponents<ThornsTerrainChunk>() )
		{
			if ( c.IsValid() && c.Enabled )
				return c;
		}

		return default;
	}

	bool TrySpawnOne( Scene scene )
	{
		var chunk = FindTerrainChunk( scene );
		if ( !chunk.IsValid() )
			return false;

		if ( !chunk.TryGetResolvedNetSpec( out var spec ) )
			return false;

		if ( spec.WorldWidth < 64f || spec.WorldDepth < 64f )
			return false;

		var rx = Math.Max( 2, spec.HeightmapResolutionX );
		var rz = Math.Max( 2, spec.HeightmapResolutionZ );
		var cells = rx * rz;
		ThornsHeightmapBakeCache.RentFilled( in spec, out var heights, out cells );
		try
		{
			var ww = Math.Max( 64f, spec.WorldWidth );
			var wd = Math.Max( 64f, spec.WorldDepth );
			var hw = ww * 0.5f;
			var hd = wd * 0.5f;
			var inset = Math.Clamp( ScatterEdgeInsetFraction, 0f, 0.45f );
			var minX = -hw + ww * inset;
			var maxX = hw - ww * inset;
			var minY = -hd + wd * inset;
			var maxY = hd - wd * inset;

			// Placement and crate rolls stay nondeterministic (not derived from world generation seed).
			// Avoid System.Environment.* — s&box whitelist blocks it (SB1000).
			_placementSalt++;
			var rnd = new Random(
				HashCode.Combine( Random.Shared.Next(), Random.Shared.Next(), Random.Shared.Next(), _placementSalt ) );

			for ( var attempt = 0; attempt < 80; attempt++ )
			{
				var lx = minX + (float)rnd.NextDouble() * (maxX - minX);
				var ly = minY + (float)rnd.NextDouble() * (maxY - minY);
				if ( !IsFarFromExistingSupply( scene, chunk, lx, ly ) )
					continue;

				var hz = ThornsTerrainGeometry.SampleHeightLocalZUp(
					heights.AsSpan( 0, cells ),
					rx,
					rz,
					ww,
					wd,
					spec.CenterOnWorldOrigin,
					lx,
					ly );

				var surfaceLocal = new Vector3( lx, ly, hz );
				var surfaceWorld = chunk.WorldPosition + chunk.WorldRotation * surfaceLocal;
				if ( !ThornsTerrainSystem.IsWorldTerrainSurfaceDryAccessible( scene, surfaceWorld.z ) )
					continue;

				var local = new Vector3( lx, ly, hz + 14f );
				var worldPos = chunk.WorldPosition + chunk.WorldRotation * local;
				return SpawnSupplyRoot( scene, worldPos, rnd );
			}
		}
		finally
		{
			ArrayPool<float>.Shared.Return( heights );
		}

		return false;
	}

	bool IsFarFromExistingSupply( Scene scene, ThornsTerrainChunk chunk, float lx, float ly )
	{
		var minD = MinSeparationFromExistingWorld;
		if ( minD <= 8f )
			return true;

		var minSq = minD * minD;
		var flat = chunk.WorldPosition + chunk.WorldRotation * new Vector3( lx, ly, 0f );

		foreach ( var b in scene.GetAllComponents<ThornsDynamicSupplyBeacon>() )
		{
			if ( !b.IsValid() || !b.Enabled )
				continue;

			var p = b.GameObject.WorldPosition;
			var dx = p.x - flat.x;
			var dy = p.y - flat.y;
			if ( dx * dx + dy * dy < minSq )
				return false;
		}

		return true;
	}

	static bool SpawnSupplyRoot( Scene scene, Vector3 worldPos, Random rnd )
	{
		var root = new GameObject( true, "ThornsDynamicSupply" );
		root.WorldPosition = worldPos;
		root.Tags.Add( "thorns_dynamic_supply" );

		var marker = root.Components.Create<ThornsPoiMarker>();
		marker.ShowOnMinimap = true;
		marker.CategoryKey = "supply_drop";
		marker.DisplayName = rnd.NextDouble() < 0.48 ? "Air Supply Drop" : "Abandoned Convoy Crate";
		marker.MinimapColor = new Color( 1f, 1f, 0f, 0.96f );
		marker.MinimapBlipDiameterPx = 15f;

		var beacon = root.Components.Create<ThornsDynamicSupplyBeacon>();

		if ( Networking.IsActive
		     && !ThornsNetworkReplication.TryNetworkSpawnHostOwned( root ) )
			Log.Warning( "[Thorns] Dynamic supply NetworkSpawn failed — joiners may not see this marker." );

		var cratePos = worldPos + Vector3.Up * 22f;
		var crate = ThornsLootCrate.SpawnHost( scene, cratePos, ThornsLootCrateKind.AirdropPremium, rnd );
		if ( crate is null || !crate.IsValid() )
		{
			root.Destroy();
			return false;
		}

		beacon.HostBindCrate( crate );
		ThornsAirdropGuardSpawner.HostSpawnGuardsAroundSupplyDrop( scene, worldPos, rnd );
		beacon.HostPlaySpawnSting();
		return true;
	}
}
