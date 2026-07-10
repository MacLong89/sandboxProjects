using System.Text.Json;

namespace Sandbox;

/// <summary>
/// Player snapshot domain — transform, vitals, inventory, equipment, wallet, milestones, guild, bed spawn.
/// </summary>
public static class ThornsPlayerSnapshotService
{
	static Dictionary<string, ThornsPersistentPlayerDto> _runtimePlayerSnapshots;

	static void EnsureRuntimeSnapshots()
	{
		if ( _runtimePlayerSnapshots is null )
			_runtimePlayerSnapshots = new Dictionary<string, ThornsPersistentPlayerDto>( StringComparer.Ordinal );
	}

	public static void HostClearRuntimeCache()
	{
		EnsureRuntimeSnapshots();
		_runtimePlayerSnapshots.Clear();
	}

	public static bool TryGetRuntimePlayerSnapshot( string accountKey, out ThornsPersistentPlayerDto dto )
	{
		dto = null;
		if ( string.IsNullOrEmpty( accountKey ) )
			return false;

		EnsureRuntimeSnapshots();
		return _runtimePlayerSnapshots.TryGetValue( accountKey, out dto ) && dto is not null;
	}

	public static void HostWriteBedSpawnForAccount(
		ThornsPersistentWorldDto live,
		string accountKey,
		ThornsPersistentPlayerDto bedFieldsSource )
	{
		if ( !Networking.IsHost || string.IsNullOrEmpty( accountKey ) || bedFieldsSource is null )
			return;

		if ( live?.PlayersByAccountKey is not null
		     && live.PlayersByAccountKey.TryGetValue( accountKey, out var liveDto )
		     && liveDto is not null )
		{
			CopyBedFields( liveDto, bedFieldsSource );
		}

		EnsureRuntimeSnapshots();
		if ( _runtimePlayerSnapshots.TryGetValue( accountKey, out var rt ) && rt is not null )
			CopyBedFields( rt, bedFieldsSource );
	}

	static void CopyBedFields( ThornsPersistentPlayerDto dst, ThornsPersistentPlayerDto src )
	{
		dst.BedInstanceId = src.BedInstanceId ?? "";
		dst.BedPx = src.BedPx;
		dst.BedPy = src.BedPy;
		dst.BedPz = src.BedPz;
		dst.BedRPitch = src.BedRPitch;
		dst.BedRYaw = src.BedRYaw;
		dst.BedRRoll = src.BedRRoll;
		dst.BedPlacementSequence = src.BedPlacementSequence;
	}

	public static void HostTryRememberPlayerBeforeTeardown(
		ThornsPlayer session,
		ThornsInventorySlotNet[] inventorySlotsOverride = null )
	{
		if ( session is null )
			return;

		var go = session.GameObject;
		if ( go is null || !go.IsValid() )
			return;

		var key = session.HostPersistenceAccountKey;
		if ( string.IsNullOrEmpty( key ) && session.OwnerConnection is not null )
			key = ThornsPersistenceIdentity.GetStableAccountKey( session.OwnerConnection );

		HostTryRememberPlayerBeforeTeardownFromRoot(
			key,
			session.OwnerConnection?.Id ?? Guid.Empty,
			go,
			inventorySlotsOverride );
	}

	public static void HostTryRememberPlayerBeforeTeardownFromRoot(
		string accountKey,
		Guid ownerConnectionId,
		GameObject pawnRoot,
		ThornsInventorySlotNet[] inventorySlotsOverride = null )
	{
		if ( !Networking.IsHost || !Game.IsPlaying || pawnRoot is null || !pawnRoot.IsValid() )
			return;

		if ( string.IsNullOrEmpty( accountKey ) )
			return;

		EnsureRuntimeSnapshots();
		var dto = HostCapturePlayerDto( ownerConnectionId, pawnRoot, inventorySlotsOverride );
		if ( inventorySlotsOverride is not null )
			MergeEquipmentWalletHotbarFromPreviousRuntimeSnapshot( accountKey, dto );

		_runtimePlayerSnapshots[accountKey] = dto;
	}

	static void MergeEquipmentWalletHotbarFromPreviousRuntimeSnapshot( string key, ThornsPersistentPlayerDto dto )
	{
		if ( string.IsNullOrEmpty( key ) || _runtimePlayerSnapshots is null )
			return;
		if ( !_runtimePlayerSnapshots.TryGetValue( key, out var prev ) || prev is null )
			return;

		dto.WalletGold = prev.WalletGold;
		dto.WalletMetal = prev.WalletMetal;
		dto.SelectedHotbarIndex = prev.SelectedHotbarIndex;
		dto.Helmet = prev.Helmet;
		dto.Chest = prev.Chest;
		dto.Pants = prev.Pants;
	}

	public static bool HostTryResolveSpawnProfile(
		ThornsPersistentWorldDto live,
		Connection channel,
		out ThornsPersistentPlayerDto profile,
		out string matchedKey )
	{
		profile = null;
		matchedKey = null;
		if ( !Networking.IsHost || channel is null || live?.PlayersByAccountKey is null )
			return false;

		var key = ThornsPersistenceIdentity.GetStableAccountKey( channel );

		if ( !string.IsNullOrEmpty( key ) && live.PlayersByAccountKey.TryGetValue( key, out var exact ) && exact is not null )
		{
			profile = exact;
			matchedKey = key;
			return true;
		}

		if ( live.PlayersByAccountKey.Count == 1 )
		{
			foreach ( var kv in live.PlayersByAccountKey )
			{
				if ( string.IsNullOrEmpty( kv.Key ) || kv.Value is null )
					continue;
				profile = kv.Value;
				matchedKey = kv.Key;
				Log.Info(
					$"[Thorns] Persistence: spawn restore using sole saved player (connection key '{key ?? "(empty)"}' not in file; file key '{matchedKey}')." );
				return true;
			}
		}

		var keys = live.PlayersByAccountKey.Count == 0
			? "(none)"
			: string.Join( ", ", live.PlayersByAccountKey.Keys );
		Log.Info( $"[Thorns] Persistence: spawn restore skipped (no player blob for connection key='{key}'; file keys: {keys})." );
		return false;
	}

	public static void HostApplySpawnRestoreProfile(
		Connection channel,
		GameObject playerRoot,
		ThornsPersistentPlayerDto dto,
		string spawnRestoreMatchedAccountKey,
		ThornsPersistentWorldDto live )
	{
		if ( channel is null || dto is null || playerRoot is null || !playerRoot.IsValid() )
			return;

		var pawn = playerRoot.Components.GetInDescendantsOrSelf<ThornsPawn>( true );
		var root = pawn.IsValid() ? pawn.GameObject : playerRoot;

		root.WorldPosition = new Vector3( dto.Px, dto.Py, dto.Pz );
		root.WorldRotation = Rotation.From( dto.RPitch, dto.RYaw, dto.RRoll );

		var upgrades = root.Components.Get<ThornsPlayerUpgrades>();
		if ( upgrades.IsValid() )
		{
			ThornsPersistentPlayerDto.MergeLegacySkillRankMigration( dto );

			upgrades.UnspentUpgradePoints = dto.UnspentUpgradePoints;
			upgrades.HydrationRank = dto.HydrationRank;
			upgrades.IronGutRank = dto.IronGutRank;
			upgrades.StrongStomachRank = dto.StrongStomachRank;
			upgrades.WeatheredRank = dto.WeatheredRank;
			upgrades.ThickHideRank = dto.ThickHideRank;
			upgrades.EnduranceRank = dto.EnduranceRank;
			upgrades.GhostRank = dto.GhostRank;
			upgrades.BeastmasterRank = dto.BeastmasterRank;
			upgrades.HardenedRank = dto.HardenedRank;
			upgrades.LuckyChamberRank = dto.LuckyChamberRank;
			upgrades.LumberjackRank = dto.LumberjackRank;
			upgrades.MinerRank = dto.MinerRank;
			upgrades.ScavengerRank = dto.ScavengerRank;
			upgrades.ReinforcedRank = dto.ReinforcedRank;
			upgrades.TechnicianRank = dto.TechnicianRank;
		}

		var vitals = root.Components.Get<ThornsVitals>();
		if ( vitals.IsValid() )
			vitals.HostApplyPersistedSurvivalState(
				dto.Hunger,
				dto.Thirst,
				dto.Stamina,
				dto.PoisonLevel,
				dto.TotalXp,
				dto.ServerSprinting,
				dto.ServerCrouching );

		var hp = root.Components.Get<ThornsHealth>();
		if ( hp.IsValid() )
		{
			hp.MaxHealth = dto.HealthMax > 0.01f ? dto.HealthMax : hp.MaxHealth;
			hp.CurrentHealth = Math.Clamp( dto.HealthCurrent, 0f, hp.MaxHealth );
			hp.IsDeadState = dto.HealthIsDeadState;
		}

		var wallet = root.Components.Get<ThornsWallet>();
		if ( wallet.IsValid() )
		{
			wallet.Gold = Math.Max( 0, dto.WalletGold );
			wallet.Metal = Math.Max( 0, dto.WalletMetal );
		}

		var inv = HostTryResolveInventoryForPersistence( playerRoot );
		if ( inv.IsValid() )
		{
			Log.Info( $"[Thorns] Persistence [inv] restore: ThornsInventory on '{inv.GameObject.Name}' (Networking.IsHost={Networking.IsHost})" );
			var rows = HostTryDecodeInventoryRowsForRestore( dto );
			var netSlots = ThornsPersistInventorySlotDto.ToSlotNetArray( rows );
			Log.Info(
				$"[Thorns] Persistence [inv] restore: applying netSlots total={netSlots.Length} nonEmpty={HostCountNonEmptyNetSlots( netSlots )}" );
			inv.HostRestoreInventorySlotsFromPersistence( netSlots );
			var after = inv.HostSnapshotSlotsForPersistence();
			Log.Info(
				$"[Thorns] Persistence [inv] restore: host re-snapshot nonEmpty={HostCountNonEmptyNetSlots( after )}" );
		}
		else
			Log.Warning( "[Thorns] Persistence [inv] restore: ThornsInventory not found on hierarchy — inventory skipped." );

		var armor = root.Components.Get<ThornsArmorEquipment>();
		if ( armor.IsValid() )
			armor.HostRestoreEquippedArmorFromPersistence( dto.Helmet, dto.Chest, dto.Pants );

		var hb = root.Components.Get<ThornsHotbarEquipment>();
		if ( hb.IsValid() && dto.SelectedHotbarIndex >= 0 && dto.SelectedHotbarIndex < ThornsInventory.HotbarSlotCount )
			hb.HostTryEquipHotbarSlotAfterSpawn( dto.SelectedHotbarIndex );

		var guild = root.Components.Get<ThornsGuildRoster>();
		if ( guild.IsValid() )
			guild.HostApplyPersistedPackedKeys( dto.ResolveGuildMemberKeysPacked() );

		var milestones = root.Components.Get<ThornsPlayerMilestones>();
		if ( milestones.IsValid() )
		{
			if ( !string.IsNullOrEmpty( dto.MilestoneProgressPacked ) )
				milestones.MilestoneProgressPacked = dto.MilestoneProgressPacked;
			else
			{
				milestones.ActiveMilestoneIndex = dto.ActiveMilestoneIndex;
				milestones.ActiveProgress = dto.MilestoneActiveProgress;
			}

			milestones.HostMaterializePackedFromLegacyIfNeeded();
		}

		var charProg = root.Components.Get<ThornsCharacterProgression>();
		if ( charProg.IsValid() )
		{
			charProg.CharacterLevel = dto.CharacterLevel > 0 ? dto.CharacterLevel : 1;
			charProg.XpProgressInCurrentLevel = dto.XpProgressInCurrentLevel;
		}

		var weapon = root.Components.Get<ThornsWeapon>();
		if ( weapon.IsValid() )
			weapon.HostPushWeaponHudFromInventory();

		var currentConnKey = ThornsPersistenceIdentity.GetStableAccountKey( channel );
		var remapFromKey = string.IsNullOrEmpty( spawnRestoreMatchedAccountKey ) ? currentConnKey : spawnRestoreMatchedAccountKey;
		ThornsStructureSnapshotService.HostRemapStructureOwnersForAccountKey( remapFromKey, channel.Id );
		ThornsWildlifeSnapshotService.HostRemapWildlifeOwnersForAccountKey( remapFromKey, channel.Id );

		if ( live?.PlayersByAccountKey is not null
		     && !string.IsNullOrEmpty( spawnRestoreMatchedAccountKey )
		     && !string.Equals( spawnRestoreMatchedAccountKey, currentConnKey, StringComparison.Ordinal ) )
		{
			live.PlayersByAccountKey.Remove( spawnRestoreMatchedAccountKey );
		}

		Log.Info( $"[Thorns] Persistence: player restore applied for '{channel.DisplayName}'." );
	}

	public static Dictionary<string, ThornsPersistentPlayerDto> HostCapturePlayersForSnapshot(
		ThornsPersistentWorldDto live,
		Scene scene )
	{
		var players = new Dictionary<string, ThornsPersistentPlayerDto>( StringComparer.Ordinal );
		if ( live?.PlayersByAccountKey is not null )
		{
			foreach ( var kv in live.PlayersByAccountKey )
			{
				if ( string.IsNullOrEmpty( kv.Key ) || kv.Value is null )
					continue;
				players[kv.Key] = kv.Value;
			}
		}

		EnsureRuntimeSnapshots();
		var connectedAccountKeys = new HashSet<string>( StringComparer.Ordinal );
		foreach ( var session in EnumerateThornsPlayerSessionsInWorld( scene ) )
		{
			if ( !session.IsValid() )
				continue;

			var key = session.HostPersistenceAccountKey;
			if ( string.IsNullOrEmpty( key ) && session.OwnerConnection is not null )
				key = ThornsPersistenceIdentity.GetStableAccountKey( session.OwnerConnection );

			if ( string.IsNullOrEmpty( key ) )
				continue;

			var root = session.GameObject;
			if ( root is null || !root.IsValid() )
				continue;

			connectedAccountKeys.Add( key );

			var playerDto = HostCapturePlayerDto( session.OwnerConnection?.Id ?? Guid.Empty, root );
			players[key] = playerDto;
			_runtimePlayerSnapshots[key] = playerDto;
		}

		foreach ( var kv in _runtimePlayerSnapshots )
		{
			if ( string.IsNullOrEmpty( kv.Key ) || kv.Value is null )
				continue;
			if ( connectedAccountKeys.Contains( kv.Key ) )
				continue;
			players[kv.Key] = kv.Value;
		}

		return players;
	}

	public static void HostTryFlushDisconnectedPlayer( Connection channel, Scene scene )
	{
		if ( !Networking.IsHost || !Game.IsPlaying || channel is null )
			return;

		foreach ( var session in EnumerateThornsPlayerSessionsInWorld( scene ) )
		{
			if ( !session.IsValid() )
				continue;
			if ( session.OwnerConnection?.Id != channel.Id )
				continue;

			var inv = HostTryResolveInventoryForPersistence( session.GameObject );
			var slots = inv.IsValid() ? inv.HostSnapshotSlotsForPersistence() : null;
			HostTryRememberPlayerBeforeTeardown( session, slots );
			Log.Info(
				$"[Thorns] Persistence: disconnect inventory flush for '{channel.DisplayName}' key={session.HostPersistenceAccountKey}" );
			return;
		}

		Log.Info(
			$"[Thorns] Persistence: disconnect flush skipped (pawn already torn down) for '{channel.DisplayName}' — relying on inventory teardown snapshot." );
	}

	public static int CountRuntimeSnapshots() => _runtimePlayerSnapshots?.Count ?? 0;

	public static void LogInventoryDiagnosticsFromWorldDto( ThornsPersistentWorldDto dto, string phase )
	{
		if ( dto?.PlayersByAccountKey is null )
			return;

		foreach ( var kv in dto.PlayersByAccountKey )
		{
			var p = kv.Value;
			if ( p is null )
				continue;

			var blobLen = p.InventorySlotsBlob?.Length ?? 0;
			var legacyLen = p.InventorySlots?.Length ?? 0;
			var legacyNonEmpty = HostCountNonEmptyPersistRows( p.InventorySlots );
			Log.Info(
				$"[Thorns] Persistence [inv] {phase}: key={kv.Key} blobLen={blobLen} legacyLen={legacyLen} legacyNonEmpty={legacyNonEmpty}" );
		}
	}

	static IEnumerable<ThornsPlayer> EnumerateThornsPlayerSessionsInWorld( Scene scene )
	{
		var world = Game.ActiveScene;
		if ( world is null || !world.IsValid() )
			world = scene;

		if ( world is null || !world.IsValid() )
			yield break;

		foreach ( var session in world.GetAllComponents<ThornsPlayer>() )
		{
			if ( session.IsValid() )
				yield return session;
		}
	}

	static ThornsInventory HostTryResolveInventoryForPersistence( GameObject start )
	{
		if ( start is null || !start.IsValid() )
			return default;

		for ( var go = start; go.IsValid(); go = go.Parent )
		{
			var inv = go.Components.GetInDescendantsOrSelf<ThornsInventory>( true );
			if ( inv.IsValid() )
				return inv;
		}

		return default;
	}

	static int HostCountNonEmptyNetSlots( ThornsInventorySlotNet[] nets )
	{
		if ( nets is null )
			return 0;
		var n = 0;
		foreach ( var s in nets )
		{
			if ( !string.IsNullOrWhiteSpace( s.ItemId ) && s.Quantity > 0 )
				n++;
		}
		return n;
	}

	static int HostCountNonEmptyPersistRows( ThornsPersistInventorySlotDto[] rows )
	{
		if ( rows is null )
			return 0;
		var n = 0;
		foreach ( var r in rows )
		{
			if ( r is not null && !string.IsNullOrWhiteSpace( r.ItemId ) && r.Quantity > 0 )
				n++;
		}
		return n;
	}

	static void HostAssignPlayerInventoryForDisk( ThornsPersistentPlayerDto dto, ThornsInventorySlotNet[] netRows )
	{
		var nets = netRows ?? Array.Empty<ThornsInventorySlotNet>();
		var hostNonEmpty = HostCountNonEmptyNetSlots( nets );
		var rows = ThornsPersistInventorySlotDto.FromSlotNetArray( nets );
		try
		{
			dto.InventorySlotsBlob = JsonSerializer.Serialize( rows, ThornsPersistenceSerializer.InventoryJsonOptions );
			dto.InventorySlots = null;
			Log.Info(
				$"[Thorns] Persistence [inv] capture → disk: hostNonEmpty={hostNonEmpty} blobChars={(dto.InventorySlotsBlob?.Length ?? 0)}" );
		}
		catch ( Exception e )
		{
			Log.Warning( e, "[Thorns] Persistence: failed to serialize InventorySlotsBlob; falling back to nested InventorySlots array." );
			dto.InventorySlotsBlob = null;
			dto.InventorySlots = rows;
			Log.Info(
				$"[Thorns] Persistence [inv] capture → disk (legacy array): hostNonEmpty={hostNonEmpty} rows={rows?.Length ?? 0}" );
		}
	}

	static ThornsPersistInventorySlotDto[] HostTryDecodeInventoryRowsForRestore( ThornsPersistentPlayerDto dto )
	{
		if ( dto is null )
			return null;

		if ( !string.IsNullOrWhiteSpace( dto.InventorySlotsBlob ) )
		{
			try
			{
				var rows = JsonSerializer.Deserialize<ThornsPersistInventorySlotDto[]>(
					dto.InventorySlotsBlob,
					ThornsPersistenceSerializer.InventoryJsonOptions );
				if ( rows is not null )
				{
					Log.Info(
						$"[Thorns] Persistence [inv] decode: source=blob rows={rows.Length} nonEmpty={HostCountNonEmptyPersistRows( rows )}" );
					return rows;
				}
			}
			catch ( Exception e )
			{
				Log.Warning( e, "[Thorns] Persistence [inv] blob decode failed; trying legacy InventorySlots." );
			}
		}

		if ( dto.InventorySlots is { Length: > 0 } )
		{
			Log.Info(
				$"[Thorns] Persistence [inv] decode: source=legacyArray rows={dto.InventorySlots.Length} nonEmpty={HostCountNonEmptyPersistRows( dto.InventorySlots )}" );
			return dto.InventorySlots;
		}

		Log.Info( "[Thorns] Persistence [inv] decode: source=none (empty inventory)" );
		return null;
	}

	public static ThornsPersistentPlayerDto HostCapturePlayerDto(
		Guid ownerConnectionId,
		GameObject pawnRoot,
		ThornsInventorySlotNet[] inventorySlotsOverride = null )
	{
		var t = pawnRoot.WorldTransform;
		var ang = t.Rotation.Angles();

		var dto = new ThornsPersistentPlayerDto
		{
			Px = t.Position.x,
			Py = t.Position.y,
			Pz = t.Position.z,
			RPitch = ang.pitch,
			RYaw = ang.yaw,
			RRoll = ang.roll
		};

		var hp = pawnRoot.Components.Get<ThornsHealth>();
		if ( hp.IsValid() )
		{
			dto.HealthCurrent = hp.CurrentHealth;
			dto.HealthMax = hp.MaxHealth;
			dto.HealthIsDeadState = hp.IsDeadState;
		}

		var ups = pawnRoot.Components.Get<ThornsPlayerUpgrades>();
		if ( ups.IsValid() )
		{
			dto.UnspentUpgradePoints = ups.UnspentUpgradePoints;
			dto.HydrationRank = ups.HydrationRank;
			dto.IronGutRank = ups.IronGutRank;
			dto.StrongStomachRank = ups.StrongStomachRank;
			dto.WeatheredRank = ups.WeatheredRank;
			dto.ThickHideRank = ups.ThickHideRank;
			dto.EnduranceRank = ups.EnduranceRank;
			dto.GhostRank = ups.GhostRank;
			dto.BeastmasterRank = ups.BeastmasterRank;
			dto.HardenedRank = ups.HardenedRank;
			dto.LuckyChamberRank = ups.LuckyChamberRank;
			dto.LumberjackRank = ups.LumberjackRank;
			dto.MinerRank = ups.MinerRank;
			dto.ScavengerRank = ups.ScavengerRank;
			dto.ReinforcedRank = ups.ReinforcedRank;
			dto.TechnicianRank = ups.TechnicianRank;
			dto.MiningRank = ups.MinerRank;
			dto.WoodcuttingRank = ups.LumberjackRank;
			dto.HungerMaxRank = ups.IronGutRank;
			dto.ThirstMaxRank = ups.HydrationRank;
			dto.StaminaMaxRank = ups.EnduranceRank;
			dto.TamingThresholdRank = ups.BeastmasterRank;
			dto.CraftingTierRank = ups.TechnicianRank;
		}

		var vitals = pawnRoot.Components.Get<ThornsVitals>();
		if ( vitals.IsValid() )
		{
			dto.Hunger = vitals.Hunger;
			dto.Thirst = vitals.Thirst;
			dto.Stamina = vitals.Stamina;
			dto.PoisonLevel = vitals.PoisonLevel;
			dto.TotalXp = vitals.TotalXp;
			dto.ServerSprinting = vitals.ServerSprinting;
			dto.ServerCrouching = vitals.ServerCrouching;
		}

		var wallet = pawnRoot.Components.Get<ThornsWallet>();
		if ( wallet.IsValid() )
		{
			dto.WalletGold = wallet.Gold;
			dto.WalletMetal = wallet.Metal;
		}

		if ( inventorySlotsOverride is not null )
			HostAssignPlayerInventoryForDisk( dto, inventorySlotsOverride );
		else
		{
			var inv = HostTryResolveInventoryForPersistence( pawnRoot );
			var net = inv.IsValid()
				? inv.HostSnapshotSlotsForPersistence()
				: new ThornsInventorySlotNet[ThornsInventory.TotalSlots];
			HostAssignPlayerInventoryForDisk( dto, net );
		}

		var armor = pawnRoot.Components.Get<ThornsArmorEquipment>();
		if ( armor.IsValid() )
		{
			armor.HostSnapshotEquippedArmorForPersistence( out var helmet, out var chest, out var pants );
			dto.Helmet = helmet;
			dto.Chest = chest;
			dto.Pants = pants;
		}

		var hb = pawnRoot.Components.Get<ThornsHotbarEquipment>();
		if ( hb.IsValid() )
			dto.SelectedHotbarIndex = hb.ServerGetSelectedHotbarIndex();

		var milestones = pawnRoot.Components.Get<ThornsPlayerMilestones>();
		if ( milestones.IsValid() )
		{
			milestones.HostMaterializePackedFromLegacyIfNeeded();
			dto.MilestoneProgressPacked = milestones.MilestoneProgressPacked;
			dto.ActiveMilestoneIndex = milestones.ActiveMilestoneIndex;
			dto.MilestoneActiveProgress = milestones.ActiveProgress;
		}

		var charProg = pawnRoot.Components.Get<ThornsCharacterProgression>();
		if ( charProg.IsValid() )
		{
			dto.CharacterLevel = charProg.CharacterLevel;
			dto.XpProgressInCurrentLevel = charProg.XpProgressInCurrentLevel;
		}

		if ( ThornsPersistenceIdentity.TryGetStableAccountKeyForConnection( ownerConnectionId, out var accountKey )
		     && !string.IsNullOrEmpty( accountKey ) )
			ThornsPlayerBedSpawn.HostMergeBedIntoCaptureDto( accountKey, dto );

		var guild = pawnRoot.Components.Get<ThornsGuildRoster>();
		if ( guild.IsValid() )
			dto.GuildMemberKeysPacked = guild.HostGetPackedForPersistence();

		return dto;
	}
}
