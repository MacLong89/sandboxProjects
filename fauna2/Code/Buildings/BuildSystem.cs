namespace Fauna2;

/// <summary>
/// Host-authoritative construction. Clients send placement/demolition requests
/// via RPC; the host validates land ownership, overlap, unlock level and money
/// before spawning networked objects everyone (including late joiners) sees.
/// </summary>
public sealed class BuildSystem : Component
{
	public static BuildSystem Instance { get; private set; }

	protected override void OnAwake() => Instance = this;
	protected override void OnDestroy() { if ( Instance == this ) Instance = null; }

	// ── Requests ────────────────────────────────────────────

	[Rpc.Host]
	public void RequestPlace( string definitionId, Vector3 position, float yaw )
	{
		if ( !RpcAuthorization.IsOwnerCaller() ) return;
		Place( Defs.Placeable( definitionId ), position, yaw );
	}

	/// <summary>Host-only placement logic (called by RPC and local tools).</summary>
	public void Place( PlaceableDefinition def, Vector3 position, float yaw )
	{
		if ( !Networking.IsHost )
		{
			Log.Warning( "[Fauna2 Build] Place() skipped — not host." );
			return;
		}

		var state = ZooState.Instance;

		if ( def is null )
		{
			Log.Warning( $"[Fauna2 Build] Place() failed — unknown definition. Loaded placeables: {Defs.Placeables.Count()}" );
			return;
		}

		if ( !state.IsValid() )
		{
			Log.Warning( "[Fauna2 Build] Place() failed — ZooState missing." );
			return;
		}

		if ( !BuildValidation.IsUnlocked( def ) )
		{
			var lockMsg = def.RequiredPrestige > 0
				? $"{def.DisplayName} unlocks at level {def.UnlockLevel} with {def.RequiredPrestige} prestige."
				: $"{def.DisplayName} unlocks at level {def.UnlockLevel}.";
			Fauna2Debug.Info( "Build", $"Place() blocked — {lockMsg}" );
			state.Notify( lockMsg, "lock" );
			return;
		}

		position = BuildSnap.ResolvePlacement( position, def, yaw, PlotSystem.Instance );

		BuildDiagnostics.Write( "Place() begin", def, position, yaw );

		if ( !BuildValidation.CanPlace( def, position, out var error, out var resolvedPosition, yaw ) )
		{
			Fauna2Debug.Info( "Build", $"Place() blocked — {error} at {position}" );
			state.Notify( error, "block" );
			return;
		}

		position = resolvedPosition;

		if ( !def.IsHabitat && PlaceableRegistry.Count >= GameConstants.MaxPlaceables )
		{
			Log.Warning( $"[Fauna2 Build] Place() blocked — placeable cap reached ({PlaceableRegistry.Count}/{GameConstants.MaxPlaceables})." );
			state.Notify( "Too many buildings and paths — demolish some before placing more.", "block" );
			return;
		}

		var cost = EffectiveCost( def );
		if ( !state.TrySpend( cost ) )
		{
			Fauna2Debug.Info( "Build", $"Place() blocked — not enough money (${state.Money} < ${cost})." );
			state.Notify( "Not enough money.", "payments" );
			return;
		}

		if ( def.IsHabitat )
		{
			var habitat = SpawnHabitat( def, position );
			Fauna2Debug.Info( "Build", $"Spawned habitat '{def.DisplayName}' at {position} id={habitat?.HabitatId} category={def.Category}" );
		}
		else
		{
			var placeable = SpawnPlaceable( def, position, yaw );
			if ( placeable is null )
			{
				state.AddMoney( cost );
				state.Notify( "Could not place building — try demolishing unused paths or buildings.", "block" );
				return;
			}

			BuildDiagnostics.Write( "Spawned", def, position, yaw, $"registered={PlaceableRegistry.Count}" );
		}

		try
		{
			var xp = def.IsHabitat ? GameConstants.XpPlaceHabitat
				: def.IsPathTile ? GameConstants.XpPlacePath
				: GameConstants.XpPlaceDecoration;
			state.AddXp( xp );
			GameEvents.RaiseZooModified();

			if ( SaveSystem.Instance is null || !SaveSystem.Instance.IsApplying )
			{
				if ( def.IsPathTile )
					ZooSoundNetwork.PlayPlaceForAll();
				else
					ZooSoundNetwork.PlayBuildForAll();
			}

			BuildDiagnostics.Write( "Place() complete", def, position, yaw );
		}
		catch ( Exception e )
		{
			Log.Error( $"[Fauna2 Build] Place() post-spawn failed for '{def.DisplayName}' at {position} — {e}" );
			state.Notify( "Something went wrong placing that building.", "block" );
		}
	}

	public static int EffectiveCost( PlaceableDefinition def )
	{
		if ( def is null ) return 0;
		var multiplier = SanctuaryEventSystem.Instance?.BuildCostMultiplier ?? 1f;
		return Math.Max( 0, (int)MathF.Round( def.Cost * multiplier ) );
	}

	[Rpc.Host]
	public void RequestDemolish( Vector3 position )
	{
		if ( !RpcAuthorization.IsOwnerCaller() ) return;

		var state = ZooState.Instance;

		// Habitat under the cursor?
		var habitat = HabitatRegistry.FindAt( position );

		// Prefer a nearby small object over the habitat that contains it.
		var placeable = PlaceableRegistry.Nearest( position, 90f );

		if ( placeable is not null )
		{
			if ( PlaceableRegistry.IsEntrance( placeable ) && PathNetwork.GetConnectedPaths().Count > 0 )
			{
				state.Notify( "Remove connected paths before demolishing the entrance.", "block" );
				return;
			}

			var refund = (int)((placeable.Definition?.Cost ?? 0) * GameConstants.DemolishRefundFraction);
			state.AddMoney( refund );
			state.Notify( $"Removed {placeable.Definition?.DisplayName} (+${refund:n0})", "construction" );
			placeable.GameObject.Destroy();
			GameEvents.RaiseZooModified();
			ZooSoundNetwork.PlayForAll( "demolish" );
			return;
		}

		if ( habitat is not null )
		{
			if ( AnimalRegistry.CountInHabitat( habitat.HabitatId ) > 0 )
			{
				state.Notify( "Rehome the animals before demolishing their habitat.", "pets" );
				return;
			}

			var refund = (int)((habitat.Definition?.Cost ?? 0) * GameConstants.DemolishRefundFraction);
			state.AddMoney( refund );
			state.Notify( $"Demolished habitat (+${refund:n0})", "construction" );
			habitat.GameObject.Destroy();
			GameEvents.RaiseZooModified();
			ZooSoundNetwork.PlayForAll( "demolish" );
		}
	}

	// ── Spawning (host only; also used by save loading) ─────

	public HabitatComponent SpawnHabitat( PlaceableDefinition def, Vector3 position, string habitatId = null )
	{
		if ( !Networking.IsHost ) return null;

		var go = new GameObject( true, $"Habitat - {def.DisplayName}" );
		go.Tags.Add( "habitat" );
		go.WorldPosition = BuildSnap.ResolvePlacement( position, def, 0f, PlotSystem.Instance ).WithZ( 0 );

		var habitat = go.AddComponent<HabitatComponent>();
		habitat.HabitatId = habitatId ?? Guid.NewGuid().ToString( "N" );
		habitat.DefinitionId = Defs.IdOf( def );
		habitat.Size = HabitatSizing.EffectiveFootprint( def.HabitatSize );
		habitat.Biome = def.HabitatBiome;

		go.NetworkMode = NetworkMode.Object;
		go.NetworkSpawn();
		go.Network.SetOrphanedMode( NetworkOrphaned.Host );

		// Register immediately so new-game saves / objective catch-up see the habitat
		// before OnStart runs (idempotent with HabitatComponent.OnStart).
		HabitatRegistry.Register( habitat );

		if ( SaveSystem.Instance is null || !SaveSystem.Instance.IsApplying )
			GameEvents.RaiseHabitatPlaced();

		return habitat;
	}

	public PlaceableComponent SpawnPlaceable( PlaceableDefinition def, Vector3 position, float yaw, float restaurantUncollected = 0f )
	{
		if ( !Networking.IsHost ) return null;

		position = BuildSnap.ResolvePlacement( position, def, yaw, PlotSystem.Instance );

		var go = new GameObject( true, $"Placeable - {def.DisplayName}" );
		go.Tags.Add( "placeable" );
		go.WorldPosition = position.WithZ( 0 );
		go.WorldRotation = Rotation.FromYaw( yaw );

		var placeable = go.AddComponent<PlaceableComponent>();
		placeable.Initialize( def, restaurantUncollected );

		try
		{
			go.NetworkMode = NetworkMode.Object;
			go.NetworkSpawn();
			go.Network.SetOrphanedMode( NetworkOrphaned.Host );
			BuildDiagnostics.Write( "NetworkSpawn ok", def, position, yaw );
		}
		catch ( Exception e )
		{
			Log.Error( $"[Fauna2 Build] NetworkSpawn failed for '{def.DisplayName}' at {position} — {e}" );
			go.Destroy();
			return null;
		}

		return placeable;
	}
}
