using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sandbox;

/// <summary>
/// Host-owned minimap POI replica (network object). Clients read synced JSON + bounds — they never author POIs (THORNS map / POI system).
/// Offline editor play uses the same composition path from scene markers (no networked replica).
/// </summary>
[Title( "Thorns — POI Authority (Network)" )]
[Category( "Thorns/World" )]
[Icon( "cloud_sync" )]
public sealed class ThornsPoiAuthority : Component
{
	public static ThornsPoiAuthority Instance { get; private set; }

	static ThornsPoiSceneSettings FindSettings( Scene scene )
	{
		if ( scene is null || !scene.IsValid() )
			return null;
		foreach ( var s in scene.GetAllComponents<ThornsPoiSceneSettings>() )
		{
			if ( s.IsValid() )
				return s;
		}

		return null;
	}

	static readonly JsonSerializerOptions JsonOpts = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
	};

	/// <summary>LEGACY: monolithic JSON array (camelCase). Prefer <see cref="PoiDatasetPayloadV1Base64"/> when <see cref="PoiDescriptorVersion"/> ≥ 1.</summary>
	[Sync( SyncFlags.FromHost )] public string PoiDatasetJson { get; set; } = "[]";

	[Sync( SyncFlags.FromHost )] public string PoiDatasetPayloadV1Base64 { get; set; } = "";

	[Sync( SyncFlags.FromHost )] public int PoiDescriptorVersion { get; set; }

	[Sync( SyncFlags.FromHost )] public long PoiContentDescriptorHash { get; set; }

	[Sync( SyncFlags.FromHost )] public float MapHorizMinX { get; set; }

	[Sync( SyncFlags.FromHost )] public float MapHorizMaxX { get; set; }

	[Sync( SyncFlags.FromHost )] public float MapHorizMinY { get; set; }

	[Sync( SyncFlags.FromHost )] public float MapHorizMaxY { get; set; }

	[Sync( SyncFlags.FromHost )] public int DatasetVersion { get; set; }

	/// <summary>Wired typed payload for minimap deserialization (camelCase JSON).</summary>
	public sealed class PoiClientRecord
	{
		public string Id { get; set; } = "";
		public string Key { get; set; } = "";
		public string Label { get; set; } = "";
		public float X { get; set; }
		public float Y { get; set; }
		public uint Rgba { get; set; }
		public float BlipDiameterPx { get; set; } = 9f;
	}

	public static bool TryComposeSceneMarkersToDataset(
		Scene scene,
		out List<PoiClientRecord> list,
		out float minX,
		out float maxX,
		out float minY,
		out float maxY )
	{
		list = new List<PoiClientRecord>();
		var settings = FindSettings( scene );
		var padding = settings?.BoundsPaddingWorld ?? 650f;
		var derive = settings?.DeriveHorizontalBoundsFromPois ?? true;

		if ( scene is not null && scene.IsValid() )
		{
			foreach ( var m in scene.GetAllComponents<ThornsPoiMarker>() )
			{
				if ( !m.IsValid() || !m.Enabled || !m.ShowOnMinimap )
					continue;

				if ( m.StableId == Guid.Empty )
					m.StableId = Guid.NewGuid();

				var wp = m.GameObject.WorldPosition;
				list.Add( new PoiClientRecord
				{
					Id = m.StableId.ToString( "N" ),
					Key = string.IsNullOrWhiteSpace( m.CategoryKey ) ? "general" : m.CategoryKey.Trim(),
					Label = string.IsNullOrWhiteSpace( m.DisplayName ) ? "POI" : m.DisplayName.Trim(),
					X = wp.x,
					Y = wp.y,
					Rgba = PackRgba( m.MinimapColor ),
					BlipDiameterPx = Math.Clamp( m.MinimapBlipDiameterPx, 4f, 28f )
				} );
			}
		}

		var manualMin = settings?.ManualHorizontalMin ?? new Vector2( -9000f, -9000f );
		var manualMax = settings?.ManualHorizontalMax ?? new Vector2( 9000f, 9000f );
		var fb = settings?.EmptyMapFallbackHalfExtent ?? new Vector2( 9000f, 9000f );

		var useTerrainBounds = settings?.UseTerrainWorldBounds ?? true;
		if ( useTerrainBounds && ThornsPoiMapBounds.TryGetTerrainPlayableBounds( scene, out minX, out maxX, out minY, out maxY ) )
		{
			return true;
		}

		if ( list.Count > 0 && derive )
		{
			minX = float.MaxValue;
			maxX = float.MinValue;
			minY = float.MaxValue;
			maxY = float.MinValue;
			foreach ( var p in list )
			{
				minX = Math.Min( minX, p.X );
				maxX = Math.Max( maxX, p.X );
				minY = Math.Min( minY, p.Y );
				maxY = Math.Max( maxY, p.Y );
			}

			minX -= padding;
			maxX += padding;
			minY -= padding;
			maxY += padding;
		}
		else if ( !derive && settings is not null )
		{
			minX = manualMin.x;
			minY = manualMin.y;
			maxX = manualMax.x;
			maxY = manualMax.y;
		}
		else
		{
			minX = -fb.x;
			minY = -fb.y;
			maxX = fb.x;
			maxY = fb.y;
		}

		if ( maxX <= minX + 32f )
		{
			minX -= 200f;
			maxX += 200f;
		}

		if ( maxY <= minY + 32f )
		{
			minY -= 200f;
			maxY += 200f;
		}

		return true;
	}

	public static bool HostSceneAlreadyHasReplica()
	{
		var scene = Game.ActiveScene;
		if ( scene is null || !scene.IsValid() )
			return false;

		foreach ( var a in scene.GetAllComponents<ThornsPoiAuthority>() )
		{
			if ( a?.IsValid == true )
				return true;
		}

		return false;
	}

	public static void SpawnHostSingleton()
	{
		if ( !Networking.IsHost || HostSceneAlreadyHasReplica() )
			return;

		var go = new GameObject( true, "ThornsPoiAuthority" );
		go.NetworkMode = NetworkMode.Object;
		_ = go.Components.Create<ThornsPoiAuthority>();
		go.NetworkSpawn();
		Log.Info( "[Thorns] POI authority network object spawned (host)." );
	}

	// After host spawns runtime ThornsPoiMarker roots (procedural sites), refresh replica JSON once authority is ready.
	// Must schedule on an instance: in static context `Task` would resolve to Component.Task (CS0120).
	public static void DelayedHostRebuildFromSceneMarkers( float delaySeconds = 0.35f )
	{
		var scene = Game.ActiveScene;
		if ( scene is null || !scene.IsValid() )
			return;

		foreach ( var auth in scene.GetAllComponents<ThornsPoiAuthority>() )
		{
			if ( auth.IsValid )
			{
				_ = auth.DelayedHostRebuildFromSceneMarkersAsync( delaySeconds );
				return;
			}
		}
	}

	async Task DelayedHostRebuildFromSceneMarkersAsync( float delaySeconds )
	{
		await Task.DelayRealtimeSeconds( delaySeconds );
		if ( !Networking.IsActive || !Networking.IsHost )
			return;

		if ( !IsValid )
			return;

		HostRebuildFromSceneMarkers( force: true );
	}

	protected override void OnEnabled()
	{
		base.OnEnabled();
		if ( IsValid )
			Instance = this;
	}

	protected override void OnDisabled()
	{
		if ( Instance == this )
			Instance = null;
	}

	protected override void OnStart()
	{
		if ( !Networking.IsHost )
			return;

		if ( HostShouldDeferInitialPoiRebuild( Scene ) )
			return;

		HostRebuildFromSceneMarkers();
	}

	static bool HostShouldDeferInitialPoiRebuild( Scene scene )
	{
		if ( scene is null || !scene.IsValid() )
			return false;

		foreach ( var ts in scene.GetAllComponents<ThornsTerrainSystem>() )
		{
			if ( !ts.IsValid() || !ts.Enabled )
				continue;

			if ( ts.IsHostWorldGenPending )
				return true;

			if ( ts.GenerateProceduralBuildings && ts.ScatterProceduralSites )
				return true;
		}

		return false;
	}

	static double _nextHostRebuildAllowedRealtime;

	/// <summary>Host: re-scan scene markers (e.g. after streaming).</summary>
	public void HostRebuildFromSceneMarkers( bool force = false )
	{
		if ( !Networking.IsHost )
			return;

		if ( !force && Time.Now < _nextHostRebuildAllowedRealtime )
			return;

		_nextHostRebuildAllowedRealtime = Time.Now + 0.4f;

		var scene = Game.ActiveScene;
		if ( scene is null || !scene.IsValid() )
			scene = Scene;

		TryComposeSceneMarkersToDataset( scene, out var list, out var minX, out var maxX, out var minY, out var maxY );
		var bytes = ThornsPoiReplicaBinaryV1.EncodeRecords( list );
		var hash = unchecked((long)ThornsPoiReplicaBinaryV1.Fnv1a64( bytes ) );
		if ( !force && hash == PoiContentDescriptorHash && DatasetVersion > 0 )
			return;
		PoiDescriptorVersion = ThornsPoiReplicaBinaryV1.FormatVersion;
		PoiDatasetPayloadV1Base64 = Convert.ToBase64String( bytes );
		PoiContentDescriptorHash = hash;
		PoiDatasetJson = "[]";
		MapHorizMinX = minX;
		MapHorizMaxX = maxX;
		MapHorizMinY = minY;
		MapHorizMaxY = maxY;
		DatasetVersion++;
		ThornsWorldReplicaMetrics.LastPoiPayloadBytes = PoiDatasetPayloadV1Base64.Length;
		ThornsWorldReplicaMetrics.PoiDescriptorVersion = PoiDescriptorVersion;
		ThornsWorldReplicaMetrics.PoiContentDescriptorHash = hash;
		ThornsWorldReplicaMetrics.PoiDatasetRebuildCount++;
		ThornsReplicationDiagnostics.WarnIfLargeSyncString( nameof(ThornsPoiAuthority) + ".PoiDatasetPayloadV1Base64",
			PoiDatasetPayloadV1Base64.Length );
		Log.Info( $"[Thorns] POI dataset rebuilt count={list.Count}, ver={DatasetVersion}, bounds=({minX:F0},{minY:F0})-({maxX:F0},{maxY:F0})" );
	}

	public static List<PoiClientRecord> DeserializeForUi( string json )
	{
		if ( string.IsNullOrWhiteSpace( json ) || json == "[]" )
			return new List<PoiClientRecord>();

		try
		{
			return JsonSerializer.Deserialize<List<PoiClientRecord>>( json, JsonOpts ) ?? new List<PoiClientRecord>();
		}
		catch ( Exception ex )
		{
			Log.Warning( $"[Thorns] POI dataset JSON deserialize failed: {ex.Message}" );
			return new List<PoiClientRecord>();
		}
	}

	/// <summary>Minimap / UI: binary v1 replica first, then <b>legacy</b> JSON.</summary>
	public static List<PoiClientRecord> GetClientRecordsForUi( ThornsPoiAuthority auth )
	{
		if ( auth is null || !auth.IsValid() )
			return new List<PoiClientRecord>();

		if ( auth.PoiDescriptorVersion >= 1 && !string.IsNullOrWhiteSpace( auth.PoiDatasetPayloadV1Base64 ) )
		{
			if ( ThornsPoiReplicaBinaryV1.TryDecodeFromBase64( auth.PoiDatasetPayloadV1Base64, out var bin ) )
				return bin;
		}

		return DeserializeForUi( auth.PoiDatasetJson );
	}

	public static Color UnpackRgba( uint rgba )
	{
		var r = (rgba >> 24) & 0xff;
		var g = (rgba >> 16) & 0xff;
		var b = (rgba >> 8) & 0xff;
		var a = rgba & 0xff;
		return new Color( r / 255f, g / 255f, b / 255f, a / 255f );
	}

	public static uint PackRgba( Color c )
	{
		byte Rf( float x ) => (byte)Math.Clamp( (int)MathF.Round( x * 255f ), 0, 255 );
		var r = Rf( c.r );
		var g = Rf( c.g );
		var b = Rf( c.b );
		var au = Rf( c.a );
		return (uint)((r << 24) | (g << 16) | (b << 8) | au);
	}
}
