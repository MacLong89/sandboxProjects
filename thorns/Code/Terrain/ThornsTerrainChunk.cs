#nullable disable

using System.Text;

namespace Sandbox;

/// <summary>
/// One replicated terrain volume: host assigns <see cref="SyncSpecPayloadV1Base64"/> (compact v1 binary) with
/// <see cref="TerrainSpecDescriptorVersion"/> / <see cref="TerrainSpecContentHash"/>.
/// <b>Legacy:</b> older scenes may still populate <see cref="SyncSpecJson"/> — clients decode JSON if binary is absent.
/// Every peer rebuilds the same terraingen terrain from the resolved <see cref="ThornsTerrainNetSpec"/>.
/// </summary>
[Title( "Thorns — Terrain chunk (network)" )]
[Category( "Thorns/World" )]
[Icon( "terrain" )]
public sealed class ThornsTerrainChunk : Component, Component.INetworkSpawn
{
	/// <summary>LEGACY fallback — monolithic JSON from <see cref="ThornsTerrainNetSpec.Serialize"/> (prefabs / old saves).</summary>
	[Sync( SyncFlags.FromHost )] public string SyncSpecJson { get; set; } = "";

	[Sync( SyncFlags.FromHost )] public string SyncSpecPayloadV1Base64 { get; set; } = "";

	[Sync( SyncFlags.FromHost )] public int TerrainSpecDescriptorVersion { get; set; }

	[Sync( SyncFlags.FromHost )] public long TerrainSpecContentHash { get; set; }

	ModelRenderer _renderer;
	ModelCollider _collider;
	long _lastSpecSyncToken = long.MinValue;
	byte[] _cachedSpecPayloadBytes;
	ThornsTerrainNetSpec _cachedResolvedSpec;

	public void OnNetworkSpawn( Connection owner ) =>
		RebuildIfNeeded( force: true, reason: "network_spawn" );

	/// <summary>True when either v1 binary or legacy JSON carries a spec.</summary>
	public bool HasReplicatedTerrainSpec() =>
		( TerrainSpecDescriptorVersion >= 1 && !string.IsNullOrWhiteSpace( SyncSpecPayloadV1Base64 ) )
		|| !string.IsNullOrWhiteSpace( SyncSpecJson );

	/// <summary>Resolves replicated payload to a spec (binary v1 preferred, then legacy JSON).</summary>
	public bool TryGetResolvedNetSpec( out ThornsTerrainNetSpec spec )
	{
		if ( TerrainSpecDescriptorVersion >= 1 && !string.IsNullOrWhiteSpace( SyncSpecPayloadV1Base64 ) )
		{
			if ( ThornsTerrainReplicaBinaryV1.TryDecodeFromBase64( SyncSpecPayloadV1Base64, out spec ) )
			{
				ThornsWorldReplicaMetrics.TerrainSpecDescriptorVersion = TerrainSpecDescriptorVersion;
				ThornsWorldReplicaMetrics.TerrainSpecContentHash = TerrainSpecContentHash;
				return true;
			}
		}

		if ( !string.IsNullOrWhiteSpace( SyncSpecJson ) )
		{
			spec = ThornsTerrainNetSpec.Deserialize( SyncSpecJson );
			return true;
		}

		spec = ThornsTerrainNetSpec.Deserialize( "" );
		return true;
	}

	/// <summary>Offline / editor: apply spec without networking (writes v1 binary fields, clears legacy JSON).</summary>
	public void ApplySpecLocal( ThornsTerrainNetSpec spec )
	{
		var bytes = ThornsTerrainReplicaBinaryV1.Encode( spec );
		TerrainSpecDescriptorVersion = ThornsTerrainReplicaBinaryV1.FormatVersion;
		SyncSpecPayloadV1Base64 = Convert.ToBase64String( bytes );
		TerrainSpecContentHash = unchecked((long)ThornsTerrainReplicaBinaryV1.Fnv1a64( bytes ));
		SyncSpecJson = "";
		ThornsReplicationDiagnostics.WarnIfLargeSyncString( nameof(ThornsTerrainChunk) + ".SyncSpecPayloadV1Base64",
			SyncSpecPayloadV1Base64.Length );
		RebuildIfNeeded( force: true, reason: "apply_spec_local" );
	}

	protected override void OnFixedUpdate()
	{
		if ( !Game.IsPlaying )
			return;

		var tok = ComputeSpecSyncToken();
		if ( tok != _lastSpecSyncToken )
			RebuildIfNeeded( force: true, reason: "sync_token_changed" );
	}

	long ComputeSpecSyncToken()
	{
		if ( TerrainSpecDescriptorVersion >= 1 && !string.IsNullOrWhiteSpace( SyncSpecPayloadV1Base64 ) )
			return HashCode.Combine( TerrainSpecDescriptorVersion, TerrainSpecContentHash );

		if ( !string.IsNullOrWhiteSpace( SyncSpecJson ) )
			return HashCode.Combine( 0, SyncSpecJson.GetHashCode( StringComparison.Ordinal ) );

		return 0;
	}

	void RebuildIfNeeded( bool force, string reason )
	{
		var tok = ComputeSpecSyncToken();
		if ( !force && tok == _lastSpecSyncToken )
			return;

		// Host world-gen can take a while before PushSpecToChunk; avoid building legacy procedural mesh from an empty spec.
		if ( tok == 0 )
			return;

		ThornsWorldReplicaMetrics.LastClientTerrainHashMatched = true;
		double decodeMs = 0;
		ThornsTerrainNetSpec spec;
		if ( TerrainSpecDescriptorVersion >= 1 && !string.IsNullOrWhiteSpace( SyncSpecPayloadV1Base64 ) )
		{
			byte[] raw;
			try
			{
				raw = Convert.FromBase64String( SyncSpecPayloadV1Base64.Trim() );
			}
			catch
			{
				raw = null;
			}

			if ( raw is not null
			     && _cachedSpecPayloadBytes is not null
			     && _cachedSpecPayloadBytes.AsSpan().SequenceEqual( raw )
			     && _cachedResolvedSpec is not null )
			{
				spec = _cachedResolvedSpec;
				ThornsWorldReplicaMetrics.LastClientTerrainHashMatched =
					unchecked( (long)ThornsTerrainReplicaBinaryV1.Fnv1a64( raw ) ) == TerrainSpecContentHash;
			}
			else
			{
				var d0 = Time.Now;
				if ( raw is null || !ThornsTerrainReplicaBinaryV1.TryDecode( raw, out spec ) )
				{
					ThornsWorldReplicaMetrics.LastClientTerrainHashMatched = false;
					spec = ThornsTerrainNetSpec.Deserialize( SyncSpecJson );
					decodeMs = ( Time.Now - d0 ) * 1000.0;
					_cachedSpecPayloadBytes = null;
					_cachedResolvedSpec = null;
				}
				else
				{
					decodeMs = ( Time.Now - d0 ) * 1000.0;
					_cachedSpecPayloadBytes = raw;
					_cachedResolvedSpec = spec;
					ThornsWorldReplicaMetrics.LastClientTerrainHashMatched =
						unchecked( (long)ThornsTerrainReplicaBinaryV1.Fnv1a64( raw ) ) == TerrainSpecContentHash;
				}
			}
		}
		else
		{
			_cachedSpecPayloadBytes = null;
			_cachedResolvedSpec = null;
			spec = ThornsTerrainNetSpec.Deserialize( SyncSpecJson );
		}

		EnsureComponents();
		DisposeBuilt();
		ThornsTerraingenTerrainRuntime.ClearUnderChunk( GameObject );

		var tBuild = Time.Now;
		_renderer.Enabled = false;
		_collider.Enabled = false;
		ThornsTerraingenTerrainRuntime.RebuildChunkVisuals( GameObject, GameObject.Scene, spec, TerrainSpecContentHash );

		_lastSpecSyncToken = tok;

		var rebuildMs = ( Time.Now - tBuild ) * 1000.0;
		ThornsWorldReplicaMetrics.LastTerrainDecodeMs = decodeMs;
		ThornsWorldReplicaMetrics.LastTerrainRebuildMs = rebuildMs;
		ThornsWorldReplicaMetrics.TerrainRebuildCount++;
		ThornsWorldReplicaMetrics.LastTerrainPayloadBytes = string.IsNullOrEmpty( SyncSpecPayloadV1Base64 )
			? ( SyncSpecJson?.Length ?? 0 )
			: SyncSpecPayloadV1Base64.Length;
		try
		{
			ThornsWorldReplicaMetrics.LastTerrainDecodedPayloadBytes = string.IsNullOrEmpty( SyncSpecPayloadV1Base64 )
				? Encoding.UTF8.GetByteCount( SyncSpecJson ?? "" )
				: Convert.FromBase64String( SyncSpecPayloadV1Base64.Trim() ).Length;
		}
		catch
		{
			ThornsWorldReplicaMetrics.LastTerrainDecodedPayloadBytes = 0;
		}

		ThornsWorldReplicaMetrics.LastTerrainClientRebuildReason = reason;
		ThornsWorldReplicaMetrics.TerrainSpecDescriptorVersion = TerrainSpecDescriptorVersion;
		ThornsWorldReplicaMetrics.TerrainSpecContentHash = TerrainSpecContentHash;

		if ( !spec.UseTerraingenFoliage )
			ThornsTerrainDecorScatter.TryScatterLocalUnderChunk( GameObject, GameObject.Scene, spec );

		TryDrawTerrainRepairDebug( GameObject.Scene, GameObject, spec );
	}

	static void TryDrawTerrainRepairDebug( Scene scene, GameObject chunkRoot, in ThornsTerrainNetSpec spec ) =>
		ThornsTerrainRepairDebug.TryDrawIfEnabled( scene, chunkRoot, in spec );

	void EnsureComponents()
	{
		if ( !_renderer.IsValid() )
			_renderer = GameObject.Components.GetOrCreate<ModelRenderer>();
		if ( !_collider.IsValid() )
			_collider = GameObject.Components.GetOrCreate<ModelCollider>();

		var orphanRb = GameObject.Components.Get<Rigidbody>();
		if ( orphanRb.IsValid() )
			orphanRb.Destroy();
	}

	void DisposeBuilt()
	{
		if ( _renderer.IsValid() )
			_renderer.Model = default;
		if ( _collider.IsValid() )
			_collider.Model = default;
	}

	protected override void OnDestroy()
	{
		DisposeBuilt();
	}
}
