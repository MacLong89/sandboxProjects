using System.Collections.Generic;
using System.Linq;

namespace Sandbox;

/// <summary>Mounts sboxweapons, creates a lobby, and spawns one Thorns-style networked pawn per connection.</summary>
[Title( "YouAreNotAlone — Game Manager" )]
[Category( "YouAreNotAlone" )]
[Icon( "hub" )]
public sealed class YaGameManager : Component, Component.INetworkListener
{
	[Property] public GameObject PlayerPrefab { get; set; }
	[Property] public List<GameObject> SpawnPoints { get; set; }
	[Property] public bool CreateLobbyOnLoad { get; set; } = true;

	[Property]
	public List<string> WeaponContentPackageIdents { get; set; } = new() { "facepunch.sboxweapons" };

	const string SboxWeaponsPackageIdent = "facepunch.sboxweapons";

	void EnsureWeaponContentPackageIdentsList()
	{
		if ( WeaponContentPackageIdents is null )
			WeaponContentPackageIdents = new List<string>();
		for ( var i = WeaponContentPackageIdents.Count - 1; i >= 0; i-- )
		{
			if ( string.IsNullOrWhiteSpace( WeaponContentPackageIdents[i] ) )
				WeaponContentPackageIdents.RemoveAt( i );
		}

		if ( !WeaponContentPackageIdents.Any( s => string.Equals( s, SboxWeaponsPackageIdent, StringComparison.OrdinalIgnoreCase ) ) )
			WeaponContentPackageIdents.Add( SboxWeaponsPackageIdent );
	}

	protected override async Task OnLoad()
	{
		if ( Scene.IsEditor )
			return;
		EnsureWeaponContentPackageIdentsList();
		await YaWeaponContentBootstrap.MountOptionalPackagesAsync( WeaponContentPackageIdents );
		if ( CreateLobbyOnLoad && !Networking.IsActive )
		{
			LoadingScreen.Title = "YouAreNotAlone";
			await Task.DelayRealtimeSeconds( 0.1f );
			Networking.CreateLobby( new() );
		}
	}

	protected override void OnStart()
	{
		if ( Scene.IsEditor || !Game.IsPlaying )
			return;
		EnsureWeaponContentPackageIdentsList();
		if ( !Components.Get<YaAmbientAudio>( FindMode.EnabledInSelf ).IsValid() )
			_ = Components.Create<YaAmbientAudio>();
		_ = RemountWeaponPackagesOnStartAsync();
		_ = TrySpawnMatchDirectorAsync();
	}

	async Task TrySpawnMatchDirectorAsync()
	{
		await Task.DelayRealtimeSeconds( 0.2f );
		if ( !IsValid || !Game.IsPlaying )
			return;
		TrySpawnMatchDirector();
	}

	/// <summary>Host-only: networked match loop (lobby → intermission → rounds).</summary>
	void TrySpawnMatchDirector()
	{
		if ( !Networking.IsHost )
			return;

		foreach ( var existing in Scene.GetAllComponents<YaGameStateSystem>() )
		{
			if ( existing.IsValid() )
				return;
		}

		var go = new GameObject( true, "YaMatchDirector" );
		go.SetParent( GameObject );
		go.NetworkMode = NetworkMode.Object;
		// Timer must exist before GameState/Round OnAwake — they cache Get<YaServerTimerSystem>().
		_ = go.Components.Create<YaServerTimerSystem>();
		_ = go.Components.Create<YaWeeklyMutatorSystem>();
		_ = go.Components.Create<YaRoundSystem>();
		_ = go.Components.Create<YaGameStateSystem>();
		_ = go.Components.Create<YaPracticeModeSystem>();
		_ = go.Components.Create<YaPawnCrowdSeparation>();
		go.NetworkSpawn();
		Log.Info( "[YA] Match director spawned (game state + round + timer)." );
	}

	/// <summary>Used by <see cref="YaGameStateSystem"/> for round respawns.</summary>
	public Transform GetHostSpawnTransform() => FindSpawnLocation();

	public const string AloneSpawnDefaultName = "AloneSpawn";

	public const string NotAloneSpawnDefaultName = "NotAloneSpawn";

	/// <summary>Optional — drag scene empties here; otherwise markers are found by name (<see cref="AloneSpawnDefaultName"/> / <see cref="NotAloneSpawnDefaultName"/>).</summary>
	[Property] public GameObject AloneSpawnMarker { get; set; }

	/// <summary>Optional — drag scene empties here; otherwise markers are found by name.</summary>
	[Property] public GameObject NotAloneSpawnMarker { get; set; }

	/// <summary>Round respawns: Alone vs hunters use separate scene transforms (<see cref="YaRoundSystem.HostStartRound"/>).</summary>
	public Transform GetRoundSpawnTransformForRole( YaPlayerRole role, int notAloneOrdinal = 0 )
	{
		if ( role != YaPlayerRole.Alone && role != YaPlayerRole.NotAlone )
			return FindSpawnLocation();

		if ( role == YaPlayerRole.NotAlone )
		{
			var notAloneMarkers = ResolveNotAloneSpawnMarkers();
			if ( notAloneMarkers.Count > 0 )
			{
				var idx = Math.Abs( notAloneOrdinal ) % notAloneMarkers.Count;
				return notAloneMarkers[idx].WorldTransform;
			}
		}

		GameObject marker = role == YaPlayerRole.Alone ? AloneSpawnMarker : NotAloneSpawnMarker;
		if ( marker is null || !marker.IsValid() )
			marker = ResolveSpawnMarkerBySceneName( role == YaPlayerRole.Alone ? AloneSpawnDefaultName : NotAloneSpawnDefaultName );

		if ( marker.IsValid() )
			return marker.WorldTransform;

		Log.Warning( $"[YA] Missing spawn marker for {role} ('{(role == YaPlayerRole.Alone ? AloneSpawnDefaultName : NotAloneSpawnDefaultName)}') — using fallback spawn." );
		return FindSpawnLocation();
	}

	/// <summary>Role spawn transform with stable spread across numbered NotAlone markers (e.g. NotAloneSpawn (1..N)).</summary>
	public Transform GetRoundSpawnTransformForPlayer( GameObject playerRoot, YaPlayerRole role )
	{
		if ( role != YaPlayerRole.NotAlone )
			return GetRoundSpawnTransformForRole( role );

		var markers = ResolveNotAloneSpawnMarkers();
		if ( markers.Count <= 0 )
			return GetRoundSpawnTransformForRole( role );

		var idx = 0;
		if ( playerRoot is { IsValid: true } )
		{
			if ( playerRoot.Network.OwnerId != default )
				idx = Math.Abs( playerRoot.Network.OwnerId.GetHashCode() );
			else if ( !string.IsNullOrWhiteSpace( playerRoot.Name ) )
				idx = Math.Abs( playerRoot.Name.GetHashCode() );
		}

		return markers[idx % markers.Count].WorldTransform;
	}

	GameObject ResolveSpawnMarkerBySceneName( string objectName )
	{
		var scene = GameObject.Scene;
		if ( scene is null || !scene.IsValid() )
			return default;

		// FindByName can return objects whose names *contain* the search string; "NotAloneSpawn" contains "AloneSpawn".
		// Require exact, case-sensitive full-name match to avoid overlap/partial hits.
		foreach ( var go in scene.Directory.FindByName( objectName, true ) )
		{
			if ( go.IsValid() && string.Equals( go.Name, objectName, StringComparison.Ordinal ) )
				return go;
		}

		return default;
	}

	List<GameObject> ResolveNotAloneSpawnMarkers()
	{
		var markers = new List<GameObject>();
		if ( NotAloneSpawnMarker is { IsValid: true } )
			markers.Add( NotAloneSpawnMarker );

		// Always include exact base marker by name if present.
		var baseMarker = ResolveSpawnMarkerBySceneName( NotAloneSpawnDefaultName );
		if ( baseMarker.IsValid() && !markers.Contains( baseMarker ) )
			markers.Add( baseMarker );

		// Collect numbered variants explicitly: NotAloneSpawn (1), (2), ...
		// This avoids directory-name query behavior that may omit partial-name matches on some scenes.
		const int maxVariantScan = 64;
		for ( var i = 1; i <= maxVariantScan; i++ )
		{
			var go = ResolveSpawnMarkerBySceneName( $"{NotAloneSpawnDefaultName} ({i})" );
			if ( go.IsValid() && !markers.Contains( go ) )
				markers.Add( go );
		}

		markers.Sort( static ( a, b ) => GetNotAloneSpawnVariantSortKey( a.Name ).CompareTo( GetNotAloneSpawnVariantSortKey( b.Name ) ) );
		return markers;
	}

	static bool IsNotAloneSpawnVariantName( string name )
	{
		if ( string.Equals( name, NotAloneSpawnDefaultName, StringComparison.Ordinal ) )
			return true;

		const string prefix = NotAloneSpawnDefaultName + " (";
		if ( string.IsNullOrWhiteSpace( name )
		     || !name.StartsWith( prefix, StringComparison.Ordinal )
		     || !name.EndsWith( ")", StringComparison.Ordinal ) )
			return false;

		var idxText = name.Substring( prefix.Length, name.Length - prefix.Length - 1 );
		return int.TryParse( idxText, out var idx ) && idx >= 1;
	}

	static int GetNotAloneSpawnVariantSortKey( string name )
	{
		if ( string.Equals( name, NotAloneSpawnDefaultName, StringComparison.Ordinal ) )
			return 0;

		const string prefix = NotAloneSpawnDefaultName + " (";
		if ( string.IsNullOrWhiteSpace( name )
		     || !name.StartsWith( prefix, StringComparison.Ordinal )
		     || !name.EndsWith( ")", StringComparison.Ordinal ) )
			return int.MaxValue;

		var idxText = name.Substring( prefix.Length, name.Length - prefix.Length - 1 );
		return int.TryParse( idxText, out var idx ) ? idx : int.MaxValue;
	}

	async Task RemountWeaponPackagesOnStartAsync()
	{
		await Task.DelayRealtimeSeconds( 0.02f );
		if ( !IsValid || !Game.IsPlaying )
			return;
		EnsureWeaponContentPackageIdentsList();
		await YaWeaponContentBootstrap.MountOptionalPackagesAsync( WeaponContentPackageIdents );
	}

	public void OnActive( Connection channel )
	{
		Log.Info( $"[YA] Player active: '{channel.DisplayName}'" );
		GameObject root;
		if ( PlayerPrefab.IsValid() )
			root = PlayerPrefab.Clone( FindSpawnLocation().WithScale( 1 ), name: $"Player - {channel.DisplayName}" );
		else
		{
			root = BuildDefaultPlayerHierarchy( channel.DisplayName );
			root.WorldTransform = FindSpawnLocation().WithScale( 1 );
		}

		EnsurePawnWorldModel( root );
		EnsureYaPlayerHud( root );
		EnsureYaPlayerRole( root );
		EnsureYaPlayerStats( root );
		EnsureYaHunterPing( root );
		EnsureYaRoundSpectatorOnView( root );
		SetSubtreeNetworkModeObject( root );
		var session = root.Components.GetInDescendantsOrSelf<YaPlayerSession>( true );
		if ( !session.IsValid() )
		{
			Log.Error( "[YA] Player root must include YaPlayerSession." );
			root.Destroy();
			return;
		}

		root.NetworkSpawn( channel );
		if ( Networking.IsHost )
			HostTryAssignMidRoundJoiner( root );
	}

	/// <summary>Host: late join during an active round becomes a hunter instead of frozen spectate.</summary>
	void HostTryAssignMidRoundJoiner( GameObject root )
	{
		var flow = YaGameStateSystem.Instance;
		if ( flow is null || !flow.IsValid() || flow.CurrentState != YaGameState.InRound )
			return;

		var pr = root.Components.Get<YaPlayerRoleComponent>( FindMode.EnabledInSelf );
		if ( !pr.IsValid() )
			return;

		pr.HostSetRole( YaPlayerRole.NotAlone );
		YaLoadoutSystem.HostApplyRoleLoadout( root, YaPlayerRole.NotAlone );

		var spawn = flow.GetSpawnTransformForPlayer( root, YaPlayerRole.NotAlone );
		var health = root.Components.Get<YaPlayerHealth>( FindMode.EnabledInSelf );
		health?.HostRespawnFull( spawn );
		root.Components.Get<YaPawnMovement>( FindMode.EnabledInSelf )?.HostApplyRespawnSnap();

		var hb = root.Components.Get<YaHotbarEquipment>( FindMode.EnabledInSelf );
		hb?.HostApplyHotbarSlot( 0, requireAlive: true );

		Log.Info( $"[YA] Late join assigned Not Alone: '{root.Name}'." );
	}

	static void SetSubtreeNetworkModeObject( GameObject root )
	{
		if ( !root.IsValid() )
			return;
		root.NetworkMode = NetworkMode.Object;
		foreach ( var child in root.Children )
			SetSubtreeNetworkModeObject( child );
	}

	GameObject BuildDefaultPlayerHierarchy( string displayName )
	{
		var playerGo = new GameObject( true, $"Player - {displayName}" );
		playerGo.SetParent( GameObject );
		playerGo.LocalPosition = Vector3.Zero;
		playerGo.LocalRotation = Rotation.Identity;
		playerGo.LocalScale = Vector3.One;
		playerGo.Tags.Add( "player" );

		_ = playerGo.Components.Create<YaPlayerSession>();
		_ = playerGo.Components.Create<YaPawn>();
		_ = playerGo.Components.Create<YaPawnMovement>();
		_ = playerGo.Components.Create<YaPlayerHealth>();
		_ = playerGo.Components.Create<YaPlayerRoleComponent>();
		_ = playerGo.Components.Create<YaPlayerStats>();
		_ = playerGo.Components.Create<YaVitalsStub>();
		_ = playerGo.Components.Create<YaGameInventory>();
		_ = playerGo.Components.Create<YaHotbarEquipment>();

		var viewGo = new GameObject( true, "View" );
		viewGo.SetParent( playerGo );
		viewGo.LocalPosition = Vector3.Zero;
		viewGo.LocalRotation = Rotation.Identity;
		viewGo.LocalScale = Vector3.One;
		_ = viewGo.Components.Create<YaPawnCamera>();
		_ = viewGo.Components.Create<YaViewModelController>();
		_ = viewGo.Components.Create<YaRoundSpectator>();

		YaCitizenRig.SetupCitizenBody( playerGo );

		var weaponWorld = new GameObject( true, YaWeapon.WorldVisualChildName );
		weaponWorld.SetParent( playerGo );
		weaponWorld.LocalPosition = Vector3.Zero;
		weaponWorld.LocalScale = YaWeapon.WorldMeshLocalScaleDevBox;
		var wVis = weaponWorld.Components.Create<SkinnedModelRenderer>();
		wVis.Model = Model.Load( "models/dev/box.vmdl" );
		wVis.Tint = new Color( 0.85f, 0.45f, 0.12f, 1f );
		wVis.UseAnimGraph = false;
		_ = weaponWorld.Components.Create<YaWeaponWorldVisual>();

		_ = playerGo.Components.Create<YaWeapon>();
		_ = playerGo.Components.Create<YaAloneMechanics>();
		_ = playerGo.Components.Create<YaHunterPingSystem>();
		_ = playerGo.Components.Create<YaPlayerHud>();
		return playerGo;
	}

	/// <summary>Host-only helper for solo practice bots (local scene object, no network owner).</summary>
	public GameObject CreateNonNetworkedPlayerRoot( string displayName, Transform spawn )
	{
		GameObject root;
		if ( PlayerPrefab.IsValid() )
			root = PlayerPrefab.Clone( spawn.WithScale( 1 ), name: $"Bot - {displayName}" );
		else
		{
			root = BuildDefaultPlayerHierarchy( displayName );
			root.WorldTransform = spawn.WithScale( 1 );
		}

		if ( Scene is { IsValid: true } )
		{
			var safeSpawn = YaPawnPlacement.SanitizeSpawnTransform( Scene, root, root.WorldTransform );
			root.WorldTransform = safeSpawn;
		}

		if ( !root.Tags.Has( "player" ) )
			root.Tags.Add( "player" );

		EnsurePawnWorldModel( root );
		EnsureYaPlayerHud( root );
		EnsureYaPlayerRole( root );
		EnsureYaBotIdentity( root, displayName );
		EnsureYaPlayerStats( root );
		EnsureYaHunterPing( root );
		EnsureYaRoundSpectatorOnView( root );

		var cc = root.Components.Get<CharacterController>( FindMode.EnabledInSelf );
		if ( cc.IsValid() )
		{
			cc.UseCollisionRules = true;
			cc.Height = 72f;
			cc.Radius = 20f;
			cc.StepHeight = 18f;
		}

		return root;
	}

	public static void EnsurePawnWorldModel( GameObject root )
	{
		var pawn = root.Components.GetInDescendantsOrSelf<YaPawn>( true );
		if ( !pawn.IsValid() )
			return;
		var pawnGo = pawn.GameObject;
		foreach ( var mr in pawnGo.Components.GetAll<ModelRenderer>( FindMode.EverythingInSelfAndDescendants ) )
		{
			if ( mr.IsValid() )
				return;
		}

		YaCitizenRig.SetupCitizenBody( pawnGo );
	}

	public static void EnsureYaPlayerHud( GameObject root )
	{
		var pawn = root.Components.GetInDescendantsOrSelf<YaPawn>( true );
		if ( !pawn.IsValid() )
			return;
		var go = pawn.GameObject;
		if ( !go.Components.Get<YaPlayerHud>( FindMode.EnabledInSelf ).IsValid() )
			_ = go.Components.Create<YaPlayerHud>();
	}

	public static void EnsureYaBotIdentity( GameObject root, string displayName )
	{
		if ( !root.IsValid() || string.IsNullOrWhiteSpace( displayName ) )
			return;

		var identity = root.Components.Get<YaBotIdentity>( FindMode.EnabledInSelf );
		if ( !identity.IsValid() )
			identity = root.Components.Create<YaBotIdentity>();
		identity.DisplayName = displayName.Trim();
	}

	public static void EnsureYaPlayerRole( GameObject root )
	{
		var pawn = root.Components.GetInDescendantsOrSelf<YaPawn>( true );
		if ( !pawn.IsValid() )
			return;
		var go = pawn.GameObject;
		if ( !go.Components.Get<YaPlayerRoleComponent>( FindMode.EnabledInSelf ).IsValid() )
			_ = go.Components.Create<YaPlayerRoleComponent>();
	}

	public static void EnsureYaPlayerStats( GameObject root )
	{
		var pawn = root.Components.GetInDescendantsOrSelf<YaPawn>( true );
		if ( !pawn.IsValid() )
			return;
		var go = pawn.GameObject;
		if ( !go.Components.Get<YaPlayerStats>( FindMode.EnabledInSelf ).IsValid() )
			_ = go.Components.Create<YaPlayerStats>();
	}

	public static void EnsureYaHunterPing( GameObject root )
	{
		if ( !root.IsValid() )
			return;

		if ( !root.Components.Get<YaHunterPingSystem>( FindMode.EnabledInSelf ).IsValid() )
			_ = root.Components.Create<YaHunterPingSystem>();
	}

	public static void EnsureYaRoundSpectatorOnView( GameObject root )
	{
		var pawn = root.Components.GetInDescendantsOrSelf<YaPawn>( true );
		if ( !pawn.IsValid() )
			return;
		foreach ( var ch in pawn.GameObject.Children )
		{
			if ( !ch.IsValid() || ch.Name != "View" )
				continue;
			if ( !ch.Components.Get<YaRoundSpectator>( FindMode.EnabledInSelf ).IsValid() )
				_ = ch.Components.Create<YaRoundSpectator>();
			return;
		}
	}

	Transform FindSpawnLocation()
	{
		if ( SpawnPoints is { Count: > 0 } )
			return Random.Shared.FromList( SpawnPoints, default ).WorldTransform;
		var spawnPoints = Scene.GetAllComponents<SpawnPoint>().ToArray();
		if ( spawnPoints.Length > 0 )
			return Random.Shared.FromArray( spawnPoints ).WorldTransform;
		var t = WorldTransform;
		t.Position += new Vector3( 0f, 0f, 32f );
		return t;
	}
}
