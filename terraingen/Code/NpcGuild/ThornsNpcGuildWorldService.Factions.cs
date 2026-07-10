namespace Terraingen.NpcGuild;

using Terraingen.AI;
using Terraingen.Animals;
using Terraingen.Buildings;
using Terraingen.Core;
using Terraingen.GameData;
using Terraingen.Multiplayer;
using Terraingen.Player;
using Terraingen.TerrainGen;
using Terraingen.Victory;
using Terraingen.World;

public sealed partial class ThornsNpcGuildWorldService
{
	public const float HeadquartersMinSeparationInches = 19685f;

	sealed class GuildFaction
	{
		public readonly string GuildId;
		public bool IsEliminated;
		public bool HasDominionVictory;
		public float ExpansionAccumulator;
		public float NextExpansionIntervalSeconds;
		public int NextOutpostSeed = 1;

		readonly List<OutpostRuntime> _outposts = new();
		readonly Dictionary<string, OutpostRuntime> _outpostsById = new( StringComparer.OrdinalIgnoreCase );
		List<ThornsPersistentNpcGuildOutpostDto> _pendingRestore = new();

		public GuildFaction( string guildId )
		{
			GuildId = guildId;
			NextExpansionIntervalSeconds = RollNextExpansionDelay();
		}

		public IReadOnlyList<OutpostRuntime> Outposts => _outposts;

		public void ImportSaved( ThornsPersistentNpcGuildDto saved )
		{
			if ( saved is null )
				return;

			IsEliminated = saved.IsEliminated;
			HasDominionVictory = saved.HasDominionVictory;
			ExpansionAccumulator = Math.Max( 0f, saved.ExpansionAccumulatorSeconds );
			NextOutpostSeed = Math.Max( 1, saved.NextOutpostSeed );
			_pendingRestore = saved.Outposts?.ToList() ?? new List<ThornsPersistentNpcGuildOutpostDto>();
		}

		public ThornsPersistentNpcGuildDto ExportSaved()
		{
			return new ThornsPersistentNpcGuildDto
			{
				GuildId = GuildId,
				IsEliminated = IsEliminated,
				HasDominionVictory = HasDominionVictory,
				ExpansionAccumulatorSeconds = ExpansionAccumulator,
				NextOutpostSeed = NextOutpostSeed,
				Outposts = _outposts.Select( o => o.ToPersistent() ).ToList()
			};
		}

		public void ResyncGuildMenuState() => SyncNpcDominionProgress();

		public ThornsNpcGuildRivalDto BuildRivalSnapshot()
		{
			var template = ThornsNpcGuildCatalog.TryGet( GuildId );
			var outpostCount = _outposts.Count;
			var dominionPercent = HasDominionVictory
				? 100f
				: Math.Clamp( outpostCount / (float)OutpostVictoryTarget * 100f, 0f, 100f );

			var guildName = template?.GuildName ?? GuildId;
			var status = IsEliminated
				? "ELIMINATED"
				: HasDominionVictory
					? "DOMINION VICTORY"
					: $"Expanding — {outpostCount}/{OutpostVictoryTarget} outposts";

			return new ThornsNpcGuildRivalDto
			{
				HasRival = true,
				GuildId = GuildId,
				GuildName = guildName,
				Motto = template?.Motto ?? "",
				IsEliminated = IsEliminated,
				HasDominionVictory = HasDominionVictory,
				OutpostCount = outpostCount,
				OutpostTarget = OutpostVictoryTarget,
				DominionPercent = dominionPercent,
				StatusLine = status
			};
		}

		public void HostRestoreOrCreate(
			ThornsNpcGuildWorldService host,
			List<Vector3> occupiedHqCenters,
			ThornsPersistentNpcGuildDto saved )
		{
			if ( saved is not null && (saved.Outposts?.Count > 0 || saved.IsEliminated) )
				ImportSaved( saved );

			if ( IsEliminated )
			{
				ThornsGuildWorldService.Instance?.HostMarkNpcGuildEliminated( GuildId );
				SyncNpcDominionProgress();
				return;
			}

			if ( _pendingRestore.Count > 0 )
			{
				foreach ( var entry in _pendingRestore )
					HostSpawnOutpostFromPersistent( host, entry );

				_pendingRestore.Clear();
				SyncNpcDominionProgress();
				ThornsBuildingLootWorldService.Instance?.HostSyncFurnitureContainers();
				ThornsMapWorldService.Instance?.NotifyWorldMarkersChanged();
				return;
			}

			if ( _outposts.Count == 0 )
				HostCreateHeadquarters( host, occupiedHqCenters );

			SyncNpcDominionProgress();
			ThornsMapWorldService.Instance?.NotifyWorldMarkersChanged();
		}

		void HostCreateHeadquarters( ThornsNpcGuildWorldService host, List<Vector3> occupiedHqCenters )
		{
			var template = ThornsNpcGuildCatalog.TryGet( GuildId );
			var guildName = template?.GuildName ?? GuildId;
			var seedSalt = template?.HeadquartersSeedSalt
			               ?? unchecked( (int)HashCode.Combine( GuildId, ThornsNpcGuildCatalog.IronWolvesHqSeedSalt ) );

			if ( !ThornsNpcGuildOutpostBuilder.TryFindHeadquartersCenter(
				     host._terrain,
				     host._terrainConfig,
				     host._terrainConfig.WorldSeed,
				     seedSalt,
				     occupiedHqCenters,
				     HeadquartersMinSeparationInches,
				     out var center,
				     out var rotation ) )
			{
				Log.Warning( $"[Thorns NPC Guild] Failed to find HQ for {guildName} — using terrain fallback." );
				var idx = occupiedHqCenters.Count;
				var size = host._terrain.TerrainSize;
				var origin = host._terrain.GameObject.WorldPosition;
				center = origin + new Vector3( size * (0.62f - idx * 0.12f), size * (0.38f + idx * 0.1f), 0f );
				ThornsTerrainSurface.TrySnapToTerrain( host._terrain, center, out center );
				rotation = Rotation.FromYaw( 45f + idx * 40f );
			}

			var outpostSeed = unchecked( (int)HashCode.Combine( host._terrainConfig.WorldSeed, seedSalt ) );
			HostSpawnOutpost( host, "hq", isHeadquarters: true, center, rotation, outpostSeed, buildingIndexOffset: 0 );
			occupiedHqCenters.Add( center );

			ThornsGuildWorldService.Instance?.HostAddNpcGuildActivity(
				GuildId,
				"dominion",
				$"{guildName} established their headquarters in the wasteland." );
		}

		void HostSpawnOutpostFromPersistent( ThornsNpcGuildWorldService host, ThornsPersistentNpcGuildOutpostDto entry )
		{
			if ( entry is null || string.IsNullOrWhiteSpace( entry.OutpostId ) )
				return;

			var center = new Vector3( entry.Px, entry.Py, entry.Pz );
			var rotation = Rotation.FromYaw( entry.RYaw );
			HostSpawnOutpost(
				host,
				entry.OutpostId,
				entry.IsHeadquarters,
				center,
				rotation,
				entry.OutpostSeed,
				entry.BuildingIndexOffset );
		}

		void HostSpawnOutpost(
			ThornsNpcGuildWorldService host,
			string outpostId,
			bool isHeadquarters,
			Vector3 center,
			Rotation rotation,
			int outpostSeed,
			int buildingIndexOffset )
		{
			if ( _outpostsById.ContainsKey( outpostId ) )
				return;

			var result = ThornsNpcGuildOutpostBuilder.HostSpawnOutpost(
				host.Scene,
				host._root,
				host._terrain,
				host._terrainConfig,
				center,
				rotation,
				outpostSeed,
				GuildId,
				outpostId,
				isHeadquarters,
				buildingIndexOffset );

			if ( result is null )
				return;

			var runtime = new OutpostRuntime
			{
				OutpostId = outpostId,
				IsHeadquarters = isHeadquarters,
				Center = result.Core?.CenterWorld ?? center,
				Root = result.OutpostRoot,
				Core = result.Core,
				OutpostSeed = outpostSeed,
				BuildingIndexOffset = buildingIndexOffset,
				FurnitureIds = result.FurnitureIds
			};

			_outposts.Add( runtime );
			_outpostsById[outpostId] = runtime;
			ThornsBuildingLootWorldService.Instance?.HostSyncFurnitureContainers();
		}

		public bool HostTryClaimCore( ThornsPlayerGameplay player, string outpostId, out string failReason )
		{
			failReason = null;
			if ( IsEliminated )
			{
				failReason = "This rival guild has already been eliminated.";
				return false;
			}

			if ( string.IsNullOrWhiteSpace( outpostId ) )
			{
				failReason = "Rival core is not linked to an outpost.";
				return false;
			}

			if ( !_outpostsById.TryGetValue( outpostId, out var outpost ) || outpost.Core is null || !outpost.Core.IsValid() )
				return false;

			if ( !HostValidateClaim( player, outpost, out failReason ) )
				return false;

			if ( outpost.IsHeadquarters )
				HostEliminateGuild( player );
			else
				HostDestroyOutpost( player, outpost );

			ThornsWorldPersistence.RequestSave();
			return true;
		}

		public void HostTryExpandOutpost( ThornsNpcGuildWorldService host )
		{
			if ( HasDominionVictory || _outposts.Count >= OutpostVictoryTarget )
				return;

			var hq = _outposts.FirstOrDefault( o => o.IsHeadquarters ) ?? _outposts.FirstOrDefault();
			if ( hq is null )
				return;

			var template = ThornsNpcGuildCatalog.TryGet( GuildId );
			var guildName = template?.GuildName ?? GuildId;
			var seed = NextOutpostSeed++;
			var outpostId = $"outpost_{seed}";
			var centers = _outposts.Select( o => o.Center ).ToList();
			if ( !ThornsNpcGuildOutpostBuilder.TryFindExpansionCenter(
				     host._terrain,
				     host._terrainConfig,
				     hq.Center,
				     seed,
				     centers,
				     out var center,
				     out var rotation ) )
			{
				Log.Info( $"[Thorns NPC Guild] {guildName} expansion failed — no valid lot near HQ." );
				return;
			}

			HostSpawnOutpost( host, outpostId, isHeadquarters: false, center, rotation, seed, buildingIndexOffset: seed * 5 );
			ThornsGuildWorldService.Instance?.HostAddNpcGuildActivity(
				GuildId,
				"dominion",
				$"{guildName} raised outpost {_outposts.Count}/{OutpostVictoryTarget}.",
				broadcastWorldAlert: false );
			ThornsGuildWorldService.Instance?.HostBroadcastOutpostGrowth( guildName, _outposts.Count, OutpostVictoryTarget );
			SyncNpcDominionProgress();
			ThornsMapWorldService.Instance?.NotifyWorldMarkersChanged();
		}

		void HostDestroyOutpost( ThornsPlayerGameplay player, OutpostRuntime outpost )
		{
			var template = ThornsNpcGuildCatalog.TryGet( GuildId );
			var guildName = template?.GuildName ?? GuildId;

			HostDespawnOutpostBandits( outpost );
			HostUnregisterOutpostFurniture( outpost );
			if ( outpost.Root.IsValid() )
				outpost.Root.Destroy();

			_outposts.Remove( outpost );
			_outpostsById.Remove( outpost.OutpostId );

			player.HostGrantXp( XpOutpostDestroyed );
			ThornsVictoryBridge.Report( player, "npc_outpost_destroyed", 1 );
			ThornsGuildWorldService.Instance?.HostAddNpcGuildActivity(
				GuildId,
				"dominion",
				$"A wasteland squad destroyed {guildName} outpost {outpost.OutpostId.ToUpper()}." );

			SyncNpcDominionProgress();
			ThornsMapWorldService.Instance?.NotifyWorldMarkersChanged();
		}

		void HostEliminateGuild( ThornsPlayerGameplay player )
		{
			var template = ThornsNpcGuildCatalog.TryGet( GuildId );
			var guildName = template?.GuildName ?? GuildId;

			foreach ( var outpost in _outposts.ToList() )
			{
				HostDespawnOutpostBandits( outpost );
				HostUnregisterOutpostFurniture( outpost );
				if ( outpost.Root.IsValid() )
					outpost.Root.Destroy();
			}

			_outposts.Clear();
			_outpostsById.Clear();
			IsEliminated = true;

			player.HostGrantXp( XpGuildEliminated );
			ThornsVictoryBridge.Report( player, "npc_guild_destroyed", 1 );
			ThornsGuildWorldService.Instance?.HostMarkNpcGuildEliminated( GuildId );
			var playerName = player.Network?.Owner?.DisplayName ?? "A survivor";
			ThornsGuildWorldService.Instance?.HostAddNpcGuildActivity(
				GuildId,
				"dominion",
				$"{playerName} claimed the {guildName} headquarters — the rival guild is eliminated." );

			SyncNpcDominionProgress();
			ThornsMapWorldService.Instance?.NotifyWorldMarkersChanged();
		}

		void SyncNpcDominionProgress()
		{
			var template = ThornsNpcGuildCatalog.TryGet( GuildId );
			var guildName = template?.GuildName ?? GuildId;
			var dominionPath = ThornsVictoryPathIds.Dominion;
			if ( IsEliminated )
			{
				ThornsVictoryManager.EnsureInstance()?.HostSetGuildPathProgress( GuildId, dominionPath, 0 );
				ThornsGuildWorldService.Instance?.HostSetNpcOutpostCount( GuildId, 0, OutpostVictoryTarget, false, true );
				ThornsGuildWorldService.RefreshAllGuildSnapshotsFromWorld();
				return;
			}

			var count = _outposts.Count;
			if ( count >= OutpostVictoryTarget && !HasDominionVictory )
			{
				HasDominionVictory = true;
				ThornsGuildWorldService.Instance?.HostAddNpcGuildActivity(
					GuildId,
					"dominion",
					$"{guildName} completed their Dominion expansion — {OutpostVictoryTarget} outposts stand across the wasteland." );
			}

			var progress = HasDominionVictory
				? 10_000L
				: Math.Min( 10_000L, count * (10_000L / OutpostVictoryTarget) );

			ThornsVictoryManager.EnsureInstance()?.HostSetGuildPathProgress( GuildId, dominionPath, progress );

			ThornsGuildWorldService.Instance?.HostSetNpcOutpostCount(
				GuildId,
				count,
				OutpostVictoryTarget,
				HasDominionVictory,
				IsEliminated );
			ThornsGuildWorldService.RefreshAllGuildSnapshotsFromWorld();
		}

		public void TickGarrisonBandits( Scene scene )
		{
			if ( IsEliminated || _outposts.Count == 0 )
				return;

			ThornsPlayerRootCache.RefreshIfStale( scene );
			var players = ThornsPlayerRootCache.RootsReadOnly;
			if ( players.Count == 0 )
			{
				HostDespawnAllGarrisonBandits();
				return;
			}

			foreach ( var outpost in _outposts )
			{
				var anyPlayerNear = false;
				GameObject nearestPlayer = null;
				var nearestDistSqr = float.MaxValue;
				var nearRadiusSqr = PlayerNearOutpostInches * PlayerNearOutpostInches;

				foreach ( var player in players )
				{
					if ( player is null || !player.IsValid() )
						continue;

					var distSqr = ( player.WorldPosition - outpost.Center ).LengthSquared;
					if ( distSqr > nearRadiusSqr )
						continue;

					anyPlayerNear = true;
					if ( distSqr < nearestDistSqr )
					{
						nearestDistSqr = distSqr;
						nearestPlayer = player;
					}
				}

				if ( !anyPlayerNear )
				{
					if ( outpost.SecondsSincePlayerNear > 8f )
						HostDespawnOutpostBandits( outpost );

					continue;
				}

				outpost.SecondsSincePlayerNear = 0f;
				HostEnsureOutpostBandits( scene, outpost, nearestPlayer );
			}
		}

		void HostEnsureOutpostBandits( Scene scene, OutpostRuntime outpost, GameObject nearestPlayer )
		{
			PruneDeadBandits( outpost );
			var live = CountLiveOutpostBandits( outpost );
			if ( live >= BanditsPerOutpost )
				return;

			if ( nearestPlayer is null || !nearestPlayer.IsValid() )
				return;

			var nearPlayerCount = CountGarrisonBanditsNear( nearestPlayer.WorldPosition, PlayerBanditCapRadiusInches );
			if ( nearPlayerCount >= MaxBanditsNearPlayer )
				return;

			var cfg = OutpostDefenderConfig( outpost );
			var groupId = unchecked( (int)HashCode.Combine( GuildId, outpost.OutpostId, 0xB4AD17 ) );

			if ( !outpost.GarrisonSeeded )
			{
				var toSpawn = Math.Min( BanditsPerOutpost - live, MaxBanditsNearPlayer - nearPlayerCount );
				HostSpawnOutpostGarrisonBandits( scene, outpost, cfg, groupId, live, toSpawn );
				outpost.GarrisonSeeded = true;
				outpost.SecondsSinceLastGarrisonSpawn = 0f;
				return;
			}

			if ( outpost.SecondsSinceLastGarrisonSpawn < GarrisonReplenishIntervalSeconds )
				return;

			HostSpawnOutpostGarrisonBandits( scene, outpost, cfg, groupId, live, 1 );
			outpost.SecondsSinceLastGarrisonSpawn = 0f;
		}

		ThornsNpcHumanBanditSpawn.Config OutpostDefenderConfig( OutpostRuntime outpost )
		{
			var template = ThornsNpcGuildCatalog.TryGet( GuildId );
			var shortName = template?.GuildName?.Replace( " ", "" ) ?? "NpcGuild";
			return new ThornsNpcHumanBanditSpawn.Config
			{
				ObjectName = $"{shortName}Defender",
				Tag = "npc_guild_garrison",
				BanditType = ThornsBanditType.CityDefender,
				Archetype = ThornsBanditArchetypeConfig.CityDefender(),
				UseLeashAnchor = true,
				LeashAnchorWorld = outpost.Center,
				LeashRadius = 900f,
				WanderRadius = 580f,
				AnchorWanderGoalsToCurrentPosition = false,
				AttackRange = ThornsBanditCombat.HumanNpcMaxEngagementRangeWorld,
				LoseRadius = ThornsBanditCombat.HumanNpcMaxEngagementRangeWorld * 1.15f
			};
		}

		void HostSpawnOutpostGarrisonBandits(
			Scene scene,
			OutpostRuntime outpost,
			ThornsNpcHumanBanditSpawn.Config cfg,
			int groupId,
			int liveBeforeSpawn,
			int count )
		{
			if ( count <= 0 )
				return;

			for ( var i = 0; i < count; i++ )
			{
				var slot = liveBeforeSpawn + i;
				var angle = slot * (360f / BanditsPerOutpost);
				var offset = new Vector3(
					MathF.Cos( angle * MathF.PI / 180f ) * 220f,
					MathF.Sin( angle * MathF.PI / 180f ) * 220f,
					0f );
				var spawnPos = outpost.Center + offset;
				if ( !ThornsAnimalSpawnUtil.TryPickDrySpawnPosition( scene, spawnPos, 220f, out spawnPos, out _ ) )
					continue;

				var spawned = ThornsNpcHumanBanditSpawn.HostSpawnM4Citizen( scene, spawnPos, Game.Random, cfg, groupId, i, count );
				if ( !spawned.IsValid() )
					continue;

				if ( !spawned.Tags.Has( "npc_guild_garrison" ) )
					spawned.Tags.Add( "npc_guild_garrison" );

				outpost.BanditRoots.Add( spawned );
			}
		}

		void HostDespawnOutpostBandits( OutpostRuntime outpost )
		{
			foreach ( var root in outpost.BanditRoots )
			{
				if ( root.IsValid() )
					root.Destroy();
			}

			outpost.BanditRoots.Clear();
			outpost.GarrisonSeeded = false;
			outpost.SecondsSinceLastGarrisonSpawn = 0f;
		}

		void HostDespawnAllGarrisonBandits()
		{
			foreach ( var outpost in _outposts )
				HostDespawnOutpostBandits( outpost );
		}

		static void HostUnregisterOutpostFurniture( OutpostRuntime outpost )
		{
			var loot = ThornsBuildingLootWorldService.Instance;
			foreach ( var furnitureId in outpost.FurnitureIds )
				loot?.HostUnregisterFurniture( furnitureId );
		}

	}
}
