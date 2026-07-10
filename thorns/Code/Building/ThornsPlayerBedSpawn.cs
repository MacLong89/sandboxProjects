namespace Sandbox;

/// <summary>
/// Host-only: per-account respawn point from the most recently placed <c>bed</c> structure.
/// </summary>
public static class ThornsPlayerBedSpawn
{
	const float RespawnFeetClearance = 18f;

	sealed class BedBinding
	{
		public Guid InstanceId;
		public string AccountKey = "";
		public Vector3 Position;
		public Rotation Rotation = Rotation.Identity;
		public long PlacementSequence;
	}

	static readonly Dictionary<string, BedBinding> ActiveByAccount = new( StringComparer.Ordinal );
	static readonly Dictionary<Guid, string> AccountKeyByInstance = new();
	static readonly Dictionary<Guid, long> PlacementSequenceByInstance = new();
	static long _nextPlacementSequence;

	public static bool HostTryGetRespawnTransform( Scene scene, GameObject pawnRoot, out Transform transform )
	{
		transform = default;
		if ( !Networking.IsHost || scene is null || !scene.IsValid() || pawnRoot is null || !pawnRoot.IsValid() )
			return false;

		var key = HostResolveAccountKey( pawnRoot );
		if ( string.IsNullOrEmpty( key ) )
			return false;

		if ( !ActiveByAccount.TryGetValue( key, out var binding ) || binding is null )
			return false;

		if ( !HostValidateBindingStructure( binding ) )
		{
			HostClearBindingForAccount( key );
			return false;
		}

		var raw = new Transform( binding.Position, binding.Rotation, 1f );
		transform = ThornsGameManager.ClampSpawnTransformOntoTerrainSurface( scene, raw );
		return true;
	}

	public static void HostOnBedPlaced( ThornsPlacedStructure structure )
	{
		if ( !Networking.IsHost || structure is null || !structure.IsValid() )
			return;

		if ( !string.Equals( structure.StructureDefId, "bed", StringComparison.OrdinalIgnoreCase ) )
			return;

		var key = structure.OwnerAccountKeySync ?? "";
		if ( string.IsNullOrEmpty( key ) )
			return;

		var seq = ++_nextPlacementSequence;
		var binding = new BedBinding
		{
			InstanceId = structure.InstanceId,
			AccountKey = key,
			Position = HostComputeRespawnPosition( structure ),
			Rotation = structure.GameObject.WorldRotation,
			PlacementSequence = seq
		};

		if ( ActiveByAccount.TryGetValue( key, out var prev ) && prev is not null && prev.InstanceId != Guid.Empty )
		{
			AccountKeyByInstance.Remove( prev.InstanceId );
			PlacementSequenceByInstance.Remove( prev.InstanceId );
		}

		ActiveByAccount[key] = binding;
		AccountKeyByInstance[binding.InstanceId] = key;
		PlacementSequenceByInstance[binding.InstanceId] = seq;

		HostSyncPlayerDtoBed( key, binding );
		HostPushBedMinimapForAccount( key, structure.GameObject.WorldPosition );
		Log.Info(
			$"[Thorns] Bed respawn set account={key} instance={binding.InstanceId} pos={binding.Position} seq={seq}" );
	}

	public static void HostOnBedRemoved( Guid instanceId )
	{
		if ( !Networking.IsHost || instanceId == Guid.Empty )
			return;

		if ( !AccountKeyByInstance.TryGetValue( instanceId, out var key ) )
			return;

		AccountKeyByInstance.Remove( instanceId );
		PlacementSequenceByInstance.Remove( instanceId );

		if ( !ActiveByAccount.TryGetValue( key, out var active ) || active is null || active.InstanceId != instanceId )
			return;

		if ( HostTryRebindLatestBedForAccount( key, out var replacement ) )
		{
			HostSyncPlayerDtoBed( key, replacement );
			HostPushBedMinimapForAccount( key, HostTryGetStructureWorldPosition( replacement.InstanceId ) );
			Log.Info(
				$"[Thorns] Bed removed — respawn reverted to older bed account={key} instance={replacement.InstanceId}" );
			return;
		}

		HostClearBindingForAccount( key );
		Log.Info( $"[Thorns] Bed removed — respawn cleared for account={key}" );
	}

	public static void HostRestoreBindingFromPlayerDto( ThornsPersistentPlayerDto dto )
	{
		if ( !Networking.IsHost || dto is null || string.IsNullOrEmpty( dto.BedInstanceId ) )
			return;

		if ( !Guid.TryParse( dto.BedInstanceId, out var instanceId ) || instanceId == Guid.Empty )
			return;

		if ( !ThornsPlacedStructure.ActiveByInstanceId.TryGetValue( instanceId, out var ps )
		     || !ps.IsValid()
		     || !string.Equals( ps.StructureDefId, "bed", StringComparison.OrdinalIgnoreCase ) )
			return;

		var key = ps.OwnerAccountKeySync ?? "";
		if ( string.IsNullOrEmpty( key ) )
			return;

		var seq = dto.BedPlacementSequence > _nextPlacementSequence ? dto.BedPlacementSequence : ++_nextPlacementSequence;
		_nextPlacementSequence = Math.Max( _nextPlacementSequence, seq );

		var binding = new BedBinding
		{
			InstanceId = instanceId,
			AccountKey = key,
			Position = HostComputeRespawnPosition( ps ),
			Rotation = Rotation.From( dto.BedRPitch, dto.BedRYaw, dto.BedRRoll ),
			PlacementSequence = seq
		};

		if ( ActiveByAccount.TryGetValue( key, out var prev ) && prev is not null && prev.InstanceId != Guid.Empty )
			AccountKeyByInstance.Remove( prev.InstanceId );

		ActiveByAccount[key] = binding;
		AccountKeyByInstance[instanceId] = key;
		PlacementSequenceByInstance[instanceId] = seq;
		HostPushBedMinimapForAccount( key, ps.GameObject.WorldPosition );
	}

	/// <summary>Horizontal world XY of the owner's active respawn bed structure, if any.</summary>
	public static bool HostTryGetBedMinimapWorldXy( string accountKey, out Vector2 worldXy )
	{
		worldXy = default;
		if ( !Networking.IsHost || string.IsNullOrEmpty( accountKey ) )
			return false;

		if ( !ActiveByAccount.TryGetValue( accountKey, out var binding ) || binding is null || binding.InstanceId == Guid.Empty )
			return false;

		if ( !HostTryGetStructureWorldPosition( binding.InstanceId, out var wp ) )
			return false;

		worldXy = new Vector2( wp.x, wp.y );
		return true;
	}

	static bool HostTryGetStructureWorldPosition( Guid instanceId, out Vector3 worldPosition )
	{
		worldPosition = default;
		if ( instanceId == Guid.Empty )
			return false;

		if ( !ThornsPlacedStructure.ActiveByInstanceId.TryGetValue( instanceId, out var ps )
		     || !ps.IsValid() )
			return false;

		worldPosition = ps.GameObject.WorldPosition;
		return true;
	}

	static Vector3? HostTryGetStructureWorldPosition( Guid instanceId ) =>
		HostTryGetStructureWorldPosition( instanceId, out var wp ) ? wp : null;

	static void HostPushBedMinimapForAccount( string accountKey, Vector3? structureWorldPosition )
	{
		if ( !Networking.IsHost || string.IsNullOrEmpty( accountKey ) )
			return;

		var scene = Game.ActiveScene;
		if ( scene is null || !scene.IsValid() )
			return;

		foreach ( var session in scene.GetAllComponents<ThornsPlayer>() )
		{
			if ( !session.IsValid() )
				continue;
			if ( !string.Equals( session.HostPersistenceAccountKey, accountKey, StringComparison.Ordinal ) )
				continue;

			var mm = session.GameObject.Components.Get<ThornsMinimapHud>( FindMode.EnabledInSelf );
			if ( !mm.IsValid() )
				continue;

			if ( structureWorldPosition is { } pos )
				mm.HostSetOwnedBedMinimapBlip( new Vector2( pos.x, pos.y ) );
			else
				mm.HostClearOwnedBedMinimapBlip();
			return;
		}
	}

	static bool HostTryRebindLatestBedForAccount( string accountKey, out BedBinding best )
	{
		best = null;
		if ( string.IsNullOrEmpty( accountKey ) )
			return false;

		foreach ( var ps in ThornsPlacedStructure.ActiveByInstanceId.Values )
		{
			if ( ps is null || !ps.IsValid() )
				continue;
			if ( !string.Equals( ps.StructureDefId, "bed", StringComparison.OrdinalIgnoreCase ) )
				continue;
			if ( !string.Equals( ps.OwnerAccountKeySync ?? "", accountKey, StringComparison.Ordinal ) )
				continue;

			var seq = PlacementSequenceByInstance.TryGetValue( ps.InstanceId, out var stored ) ? stored : 0L;
			if ( best is null || seq >= best.PlacementSequence )
			{
				best = new BedBinding
				{
					InstanceId = ps.InstanceId,
					AccountKey = accountKey,
					Position = HostComputeRespawnPosition( ps ),
					Rotation = ps.GameObject.WorldRotation,
					PlacementSequence = seq
				};
			}
		}

		if ( best is null )
			return false;

		ActiveByAccount[accountKey] = best;
		AccountKeyByInstance[best.InstanceId] = accountKey;
		PlacementSequenceByInstance[best.InstanceId] = best.PlacementSequence;
		return true;
	}

	static bool HostValidateBindingStructure( BedBinding binding )
	{
		if ( binding is null || binding.InstanceId == Guid.Empty )
			return false;

		return ThornsPlacedStructure.ActiveByInstanceId.TryGetValue( binding.InstanceId, out var ps )
		       && ps.IsValid()
		       && string.Equals( ps.StructureDefId, "bed", StringComparison.OrdinalIgnoreCase );
	}

	static void HostClearBindingForAccount( string accountKey )
	{
		if ( string.IsNullOrEmpty( accountKey ) )
			return;

		if ( ActiveByAccount.TryGetValue( accountKey, out var prev ) && prev is not null && prev.InstanceId != Guid.Empty )
		{
			AccountKeyByInstance.Remove( prev.InstanceId );
			PlacementSequenceByInstance.Remove( prev.InstanceId );
		}

		ActiveByAccount.Remove( accountKey );
		HostSyncPlayerDtoBed( accountKey, null );
		HostPushBedMinimapForAccount( accountKey, null );
	}

	static Vector3 HostComputeRespawnPosition( ThornsPlacedStructure structure )
	{
		var root = structure.GameObject;
		var halfMeshZ = ThornsBuildingModule.DevReferenceSize * root.LocalScale.z * 0.5f;
		return root.WorldPosition + root.WorldRotation * Vector3.Up * (halfMeshZ + RespawnFeetClearance);
	}

	static string HostResolveAccountKey( GameObject pawnRoot )
	{
		var session = pawnRoot.Components.GetInDescendantsOrSelf<ThornsPlayer>( true );
		if ( session.IsValid() && !string.IsNullOrEmpty( session.HostPersistenceAccountKey ) )
			return session.HostPersistenceAccountKey;

		if ( session.IsValid() && session.OwnerConnection is not null )
			return ThornsPersistenceIdentity.GetStableAccountKey( session.OwnerConnection );

		return "";
	}

	static void HostSyncPlayerDtoBed( string accountKey, BedBinding binding )
	{
		if ( string.IsNullOrEmpty( accountKey ) )
			return;

		var scratch = new ThornsPersistentPlayerDto();
		HostApplyBindingToDto( scratch, binding );

		var wp = ThornsWorldPersistence.Instance;
		if ( wp is not null && wp.IsValid() )
			wp.HostWriteBedSpawnForAccount( accountKey, scratch );
	}

	public static void HostMergeBedIntoCaptureDto( string accountKey, ThornsPersistentPlayerDto dto )
	{
		if ( string.IsNullOrEmpty( accountKey ) || dto is null )
			return;

		if ( ActiveByAccount.TryGetValue( accountKey, out var binding ) && binding is not null )
			HostApplyBindingToDto( dto, binding );
	}

	static void HostApplyBindingToDto( ThornsPersistentPlayerDto dto, BedBinding binding )
	{
		if ( dto is null )
			return;

		if ( binding is null || binding.InstanceId == Guid.Empty )
		{
			dto.BedInstanceId = "";
			dto.BedPx = dto.BedPy = dto.BedPz = 0f;
			dto.BedRPitch = dto.BedRYaw = dto.BedRRoll = 0f;
			dto.BedPlacementSequence = 0;
			return;
		}

		var ang = binding.Rotation.Angles();
		dto.BedInstanceId = binding.InstanceId.ToString( "D" );
		dto.BedPx = binding.Position.x;
		dto.BedPy = binding.Position.y;
		dto.BedPz = binding.Position.z;
		dto.BedRPitch = ang.pitch;
		dto.BedRYaw = ang.yaw;
		dto.BedRRoll = ang.roll;
		dto.BedPlacementSequence = binding.PlacementSequence;
	}
}
