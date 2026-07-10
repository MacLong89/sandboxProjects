namespace Terraingen.Player;

using Sandbox.Network;
using Terraingen.Buildings;
using Terraingen.GameData;
using Terraingen.Multiplayer;
using Terraingen.UI;
using Terraingen.Victory;

/// <summary>Sequential Research Station progress for the Ascension victory path.</summary>
public sealed partial class ThornsPlayerGameplay
{
	const int ResearchLevelXp = 100;
	const string ResearchVictorySource = "research_level_completed";
	const string ResearchCapstoneVictorySource = "research_capstone_completed";

	public bool HasOpenResearch => _research.IsOpen;

	public void RequestOpenResearchStation( string instanceKey )
	{
		if ( !IsLocalPlayer() || string.IsNullOrWhiteSpace( instanceKey ) )
			return;

		if ( Networking.IsActive && !Networking.IsHost )
			RpcOpenResearchStation( instanceKey );
		else
			HostOpenResearchStation( instanceKey );
	}

	public void RequestCloseResearchStation()
	{
		if ( !IsLocalPlayer() )
			return;

		if ( Networking.IsActive && !Networking.IsHost )
			RpcCloseResearchStation();
		else
			HostCloseResearchStation();
	}

	public void RequestStartResearchLevel( int level )
	{
		if ( !IsLocalPlayer() )
			return;

		if ( Networking.IsActive && !Networking.IsHost )
			RpcStartResearchLevel( level );
		else
			HostStartResearchLevel( level );
	}

	[Rpc.Host]
	void RpcOpenResearchStation( string instanceKey )
	{
		if ( !ValidateCaller() )
			return;

		HostOpenResearchStation( instanceKey );
	}

	[Rpc.Host]
	void RpcCloseResearchStation()
	{
		if ( !ValidateCaller() )
			return;

		HostCloseResearchStation();
	}

	[Rpc.Host]
	void RpcStartResearchLevel( int level )
	{
		if ( !ValidateCaller() )
			return;

		HostStartResearchLevel( level );
	}

	public void HostOpenResearchStation( string instanceKey )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || string.IsNullOrWhiteSpace( instanceKey ) )
		{
			PushResearchToOwner( closed: true );
			return;
		}

		if ( !ValidateResearchStation( instanceKey ) )
		{
			PushResearchToOwner( closed: true );
			return;
		}

		if ( HasOpenWorldContainer )
			HostCloseWorldContainer();
		if ( HasOpenRadioShop )
			HostCloseRadioShop();
		if ( HasOpenCampfire )
			HostCloseCampfire();
		if ( HasOpenWorkbench )
			HostCloseWorkbench();

		_research.IsOpen = true;
		_research.StationInstanceKey = instanceKey;
		PushResearchToOwner();
	}

	public void HostCloseResearchStation()
	{
		if ( !_research.IsOpen )
			return;

		_research.IsOpen = false;
		_research.StationInstanceKey = "";
		PushResearchToOwner( closed: true );
	}

	public void HostTickResearch( float delta )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || delta <= 0f || _research.ActiveLevel <= 0 )
			return;

		_research.ActiveSecondsRemaining = Math.Max( 0f, _research.ActiveSecondsRemaining - delta );
		if ( _research.ActiveSecondsRemaining > 0f )
		{
			if ( _research.IsOpen && _researchPushDebounce > 0.5f )
			{
				_researchPushDebounce = 0;
				PushResearchToOwner();
			}
			return;
		}

		HostCompleteResearchLevel( _research.ActiveLevel );
	}

	public ThornsResearchSnapshotDto HostBuildResearchSnapshot()
	{
		return BuildResearchSnapshot( _research.IsOpen, _research.StationInstanceKey );
	}

	void HostStartResearchLevel( int level )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || HostIsDead() )
			return;

		if ( !_research.IsOpen || string.IsNullOrWhiteSpace( _research.StationInstanceKey ) )
			return;

		if ( !ValidateResearchStation( _research.StationInstanceKey ) )
		{
			HostCloseResearchStation();
			return;
		}

		if ( _research.ActiveLevel > 0 || level != _research.CompletedLevel + 1 )
			return;

		if ( !ThornsResearchCatalog.TryGet( level, out var def ) )
			return;

		if ( !HostHasResearchMaterials( def ) )
			return;

		foreach ( var cost in def.Costs )
			HostRemoveItemCount( cost.ItemId, cost.Count );

		_research.ActiveLevel = level;
		_research.ActiveSecondsRemaining = Math.Max( 1f, def.ResearchSeconds );
		PushInventoryToOwner();
		PushResearchToOwner();
		HostPersistPlayerState();
	}

	void HostCompleteResearchLevel( int level )
	{
		if ( !ThornsResearchCatalog.TryGet( level, out var def ) )
			return;

		_research.CompletedLevel = Math.Max( _research.CompletedLevel, level );
		_research.ActiveLevel = 0;
		_research.ActiveSecondsRemaining = 0f;
		_research.PercentComplete = ResearchPercent( _research.CompletedLevel );

		HostGrantXp( ResearchLevelXp );
		if ( !string.IsNullOrWhiteSpace( def.RewardItemId ) && def.RewardCount > 0 )
			HostAddItem( def.RewardItemId, def.RewardCount );

		ThornsVictoryManager.EnsureInstance()?.HostReportSource( AccountKey, ResearchVictorySource );
		if ( _research.CompletedLevel >= ThornsResearchCatalog.MaxLevel )
			ThornsVictoryManager.EnsureInstance()?.HostReportSource( AccountKey, ResearchCapstoneVictorySource );

		if ( def.RewardCount > 0 && !string.IsNullOrWhiteSpace( def.RewardItemId ) )
		{
			MarkInventorySyncDirty();
			PushInventoryToOwner();
		}

		PushMilestoneToastToOwner( $"Research Level {level} Complete", ResearchLevelXp );
		PushResearchToOwner();
		HostPushVictorySnapshot();
		HostPersistPlayerState();
	}

	bool HostHasResearchMaterials( ThornsResearchLevelDefinition def )
	{
		foreach ( var cost in def.Costs )
		{
			if ( HostCountItem( cost.ItemId ) < cost.Count )
				return false;
		}

		return true;
	}

	bool ValidateResearchStation( string instanceKey )
	{
		if ( string.IsNullOrWhiteSpace( instanceKey ) )
			return false;

		if ( !ThornsPlacedBuildStructure.TryFindByInstanceKey( instanceKey, out var placed ) || !placed.IsValid() )
			return false;

		if ( !string.Equals( placed.StructureId, "research", StringComparison.OrdinalIgnoreCase ) )
			return false;

		return Vector3.DistanceBetween( GameObject.WorldPosition, placed.GameObject.WorldPosition ) <= ThornsPlacedStructureInteraction.UseRange + 60f;
	}

	ThornsResearchSnapshotDto BuildResearchSnapshot( bool isOpen, string stationInstanceKey )
	{
		var snap = new ThornsResearchSnapshotDto
		{
			IsOpen = isOpen,
			StationInstanceKey = stationInstanceKey ?? "",
			CompletedLevel = Math.Clamp( _research.CompletedLevel, 0, ThornsResearchCatalog.MaxLevel ),
			ActiveLevel = Math.Clamp( _research.ActiveLevel, 0, ThornsResearchCatalog.MaxLevel ),
			ActiveSecondsRemaining = Math.Max( 0f, _research.ActiveSecondsRemaining ),
			PercentComplete = ResearchPercent( _research.CompletedLevel )
		};

		foreach ( var def in ThornsResearchCatalog.All )
		{
			var active = def.Level == snap.ActiveLevel;
			snap.Levels.Add( new ThornsResearchLevelDto
			{
				Level = def.Level,
				Title = def.Title,
				Description = def.Description,
				ResearchSeconds = def.ResearchSeconds,
				Completed = def.Level <= snap.CompletedLevel,
				Active = active,
				Available = snap.ActiveLevel <= 0 && def.Level == snap.CompletedLevel + 1,
				SecondsRemaining = active ? snap.ActiveSecondsRemaining : 0f,
				Costs = def.Costs.Select( c => new ThornsResearchIngredientDto { ItemId = c.ItemId, Count = c.Count } ).ToList(),
				RewardItemId = def.RewardItemId ?? "",
				RewardCount = Math.Max( 0, def.RewardCount )
			} );
		}

		return snap;
	}

	static float ResearchPercent( int completedLevel ) =>
		Math.Clamp( completedLevel / (float)ThornsResearchCatalog.MaxLevel * 100f, 0f, 100f );

	void PushResearchToOwner( bool closed = false )
	{
		if ( closed )
		{
			_research.IsOpen = false;
			_research.StationInstanceKey = "";
		}

		var snap = closed
			? BuildResearchSnapshot( false, "" )
			: HostBuildResearchSnapshot();

		if ( IsLocalPlayer() )
			ThornsUiClientState.ApplyPartialResearch( snap );
		else if ( Networking.IsActive )
			RpcSyncResearchJson( Json.Serialize( snap ) );
	}

	[Rpc.Owner]
	void RpcSyncResearchJson( string json )
	{
		if ( !ThornsNetAuthority.TryDeserializeJson( json, ThornsNetAuthority.DefaultOwnerJsonMaxBytes, out ThornsResearchSnapshotDto snap ) )
			return;

		ThornsUiClientState.ApplyPartialResearch( snap );
	}
}
