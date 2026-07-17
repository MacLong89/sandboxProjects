namespace Fauna2;

using Fauna2.UI;

/// <summary>Timing-bar catch minigame and placement of caught animals.</summary>
public sealed class CatchSystem : Component
{
	public static CatchSystem Instance { get; private set; }

	[Sync( SyncFlags.FromHost )] public bool MinigameActive { get; set; }
	[Sync] public string ActiveWildId { get; set; } = "";
	[Sync] public float BarPosition { get; set; }
	[Sync] public float GreenStart { get; set; }
	[Sync] public float GreenEnd { get; set; }
	[Sync] public float BarSpeed { get; set; }
	[Sync] public string CatchSpeciesName { get; set; } = "";
	[Sync] public string CatchRarityLabel { get; set; } = "";
	[Sync] public int HitsRequired { get; set; }
	[Sync] public int HitsLanded { get; set; }

	private WildAnimalComponent _activeWild;
	private bool _barAscending = true;

	protected override void OnAwake() => Instance = this;
	protected override void OnDestroy() { if ( Instance == this ) Instance = null; }

	protected override void OnFixedUpdate()
	{
		if ( IsProxy || !MinigameActive ) return;

		// AUDIT FIX (edge): if the active wild was destroyed mid-catch (plot buy,
		// host clear, etc.) auto-cancel so the bar UI cannot soft-lock.
		if ( _activeWild is null || !_activeWild.IsValid() || _activeWild.Fled )
		{
			CancelCatchHost();
			return;
		}

		var bar = BarPosition;
		TimingBarMinigame.TickBar( ref bar, ref _barAscending, BarSpeed, Time.Delta );
		BarPosition = bar;
	}

	public void TryBeginCatch( WildAnimalComponent wild )
	{
		if ( wild is null || !wild.IsValid() || wild.Fled ) return;
		if ( PlayerState.Local?.IsZooOwner != true ) return;

		var inv = PlayerState.Local?.Components.Get<PlayerInventory>();
		if ( inv is null || !inv.HasNet )
		{
			UiState.PushToast( "You need a net — buy one at the kiosk.", "block" );
			return;
		}

		if ( !inv.CanCarryMore )
		{
			UiState.PushToast( "Carry slots full — place animals in a habitat first.", "block" );
			return;
		}

		var def = wild.Definition;
		if ( def is null ) return;

		if ( def.RequiresTranquilizer && inv.TranquilizerCount <= 0 )
		{
			UiState.PushToast( $"{def.DisplayName} needs a tranquilizer.", "block" );
			return;
		}

		if ( Networking.IsHost )
			BeginCatchHost( wild );
		else
			RequestBeginCatch( wild.WildId );
	}

	[Rpc.Host]
	private void RequestBeginCatch( string wildId )
	{
		if ( !RpcAuthorization.IsOwnerCaller() ) return;

		var wild = WildAnimalRegistry.FindById( wildId );
		if ( wild is not null )
			BeginCatchHost( wild );
	}

	private void BeginCatchHost( WildAnimalComponent wild )
	{
		if ( wild is null || !wild.IsValid() || wild.Fled || MinigameActive ) return;
		if ( WildAttackSystem.Instance?.EncounterActive == true ) return;

		// AUDIT FIX B6: Host proximity check. Client UI already ranges against
		// WildAnimalInteractRange, but RequestBeginCatch previously accepted any
		// known WildId — free remote catch. Revert: delete this block.
		if ( !RpcAuthorization.TryGetCallerFeet( out var feet )
			|| RpcAuthorization.HorizontalDistance( feet, wild.GameObject.WorldPosition )
				> GameConstants.WildAnimalInteractRange )
		{
			return;
		}

		var inv = FindOwnerInventory();
		if ( inv is null || !inv.HasNet || !inv.CanCarryMore ) return;

		var def = wild.Definition;
		if ( def is null ) return;

		if ( def.RequiresTranquilizer && inv.TranquilizerCount <= 0 ) return;

		_activeWild = wild;
		ActiveWildId = wild.WildId;
		MinigameActive = true;
		BarPosition = 0f;
		_barAscending = true;
		CatchSpeciesName = def.DisplayName;
		CatchRarityLabel = CatchDifficulty.RarityLabel( def.Rarity );

		var difficulty = CatchDifficulty.For( def );
		var zoneWidth = (CatchDifficulty.GreenZoneWidth( difficulty ) + (ResearchSystem.Instance?.FieldCatchBonus ?? 0f)).Clamp( 0.08f, 0.5f );

		TimingBarMinigame.RandomizeGreenZone( out var greenStart, out var greenEnd, zoneWidth );
		GreenStart = greenStart;
		GreenEnd = greenEnd;
		BarSpeed = CatchDifficulty.BarSpeed( difficulty );
		HitsRequired = CatchDifficulty.RequiredHits( def );
		HitsLanded = 0;

		UiState.OpenCatchMinigame();
	}

	public void TryResolveCatch( bool usedBait )
	{
		if ( !MinigameActive || PlayerState.Local?.IsZooOwner != true ) return;

		if ( Networking.IsHost )
			ResolveCatchHost( usedBait );
		else
			RequestResolveCatch( usedBait );
	}

	[Rpc.Host]
	private void RequestResolveCatch( bool usedBait )
	{
		if ( !RpcAuthorization.IsOwnerCaller() ) return;
		ResolveCatchHost( usedBait );
	}

	private void ResolveCatchHost( bool usedBait )
	{
		if ( !MinigameActive || _activeWild is null || !_activeWild.IsValid() || _activeWild.Fled )
		{
			CancelCatchHost();
			return;
		}

		var def = _activeWild.Definition;
		var inv = FindOwnerInventory();
		if ( def is null || inv is null )
		{
			CancelCatchHost();
			return;
		}

		var greenStart = GreenStart;
		var greenEnd = GreenEnd;
		if ( usedBait && inv.ConsumeBait() )
			greenEnd = MathF.Min( 1f, greenEnd + 0.08f );

		// Host BarPosition is authoritative — clients cannot spoof green-zone hits.
		var success = TimingBarMinigame.IsInGreenZone( BarPosition, greenStart, greenEnd );

		if ( success )
		{
			HitsLanded++;
			if ( HitsLanded < HitsRequired )
			{
				AdvanceRound( CatchDifficulty.For( def ) );
				ZooSoundEffects.PlayUiClick();
				return;
			}

			if ( def.RequiresTranquilizer && !inv.ConsumeTranquilizer() )
			{
				UiState.PushToast( "Tranquilizer required!", "block" );
				success = false;
			}
		}

		if ( success && inv.TryAddCatch( Defs.IdOf( def ) ) )
		{
			ZooState.Instance?.AddXp( GameConstants.XpCatchAnimal );
			if ( ZooState.Instance.IsValid() )
				ZooState.Instance.TotalAnimalsCaught++;

			GameEvents.RaiseZooModified();
			ZooState.Instance?.Notify( $"Caught a {def.DisplayName}!", "pets" );
			ZooSoundEffects.PlayDiscovery();

			_activeWild.GameObject.Destroy();
			ClientWorldSync.Instance?.ScheduleWildSync();
			SaveSystem.Instance?.RequestSave();
		}
		else if ( success )
		{
			UiState.PushToast( "Carry slots full — place animals in a habitat first.", "block" );
			_activeWild.Flee();
		}
		else
		{
			UiState.PushToast( $"{def.DisplayName} got away!", "block" );
			ZooSoundEffects.PlayPlacementError();
			_activeWild.Flee();
		}

		CancelCatchHost();
	}

	public void TryCancelCatch()
	{
		if ( !MinigameActive || PlayerState.Local?.IsZooOwner != true ) return;

		if ( Networking.IsHost )
			CancelCatchHost();
		else
			RequestCancelCatch();
	}

	[Rpc.Host]
	private void RequestCancelCatch()
	{
		if ( !RpcAuthorization.IsOwnerCaller() ) return;
		CancelCatchHost();
	}

	private void CancelCatchHost()
	{
		MinigameActive = false;
		_activeWild = null;
		ActiveWildId = "";
		CatchSpeciesName = "";
		CatchRarityLabel = "";
		HitsRequired = 0;
		HitsLanded = 0;
		UiState.CloseCatchMinigame();
	}

	private void AdvanceRound( float baseDifficulty )
	{
		var roundDifficulty = (baseDifficulty + HitsLanded * 0.05f).Clamp( 0.05f, 0.95f );
		var zoneWidth = (CatchDifficulty.GreenZoneWidth( roundDifficulty ) + (ResearchSystem.Instance?.FieldCatchBonus ?? 0f)).Clamp( 0.08f, 0.5f );

		var bar = BarPosition;
		TimingBarMinigame.AdvanceRound( ref bar, ref _barAscending, out var greenStart, out var greenEnd, zoneWidth, CatchDifficulty.BarSpeed( roundDifficulty ) );
		BarPosition = bar;
		GreenStart = greenStart;
		GreenEnd = greenEnd;
		BarSpeed = CatchDifficulty.BarSpeed( roundDifficulty );
	}

	/// <summary>
	/// AUDIT FIX B13: Cancel by connection SteamId — do NOT require a living
	/// PlayerState. NetworkOrphaned.Destroy may remove the owner avatar before
	/// OnDisconnected runs, which previously left MinigameActive stuck.
	/// </summary>
	public void CancelIfOwnerDisconnected( Connection channel )
	{
		if ( !MinigameActive || channel is null ) return;

		// Always clear if the disconnecting peer was the lobby host / zoo operator.
		var leavingId = channel.SteamId.Value;
		var ownerStillPresent = false;
		foreach ( var ps in Game.ActiveScene.GetAllComponents<PlayerState>() )
		{
			if ( !ps.IsValid() || !ps.IsZooOwner ) continue;
			if ( ps.SteamId == leavingId )
			{
				CancelCatchHost();
				return;
			}

			ownerStillPresent = true;
		}

		// Owner PlayerState already destroyed but leave is the host connection,
		// OR no owner left at all — still cancel to avoid soft-lock.
		if ( !ownerStillPresent
			|| (Connection.Host is not null && channel == Connection.Host)
			|| (Networking.IsHost && channel == Connection.Local) )
		{
			CancelCatchHost();
		}
	}

	private static PlayerInventory FindOwnerInventory()
	{
		foreach ( var ps in Game.ActiveScene.GetAllComponents<PlayerState>() )
		{
			if ( !ps.IsValid() || !ps.IsZooOwner ) continue;
			var inv = ps.Components.Get<PlayerInventory>();
			if ( inv is not null ) return inv;
		}

		return PlayerInventory.Local;
	}

	[Rpc.Host]
	public void RequestPlaceCarried( Vector3 position )
	{
		if ( !RpcAuthorization.IsOwnerCaller() ) return;

		var inv = FindOwnerInventory();
		if ( inv is null ) return;

		inv.NormalizeCarried();
		var slot = !string.IsNullOrEmpty( inv.CarriedSpecies0 ) ? 0
			: !string.IsNullOrEmpty( inv.CarriedSpecies1 ) ? 1 : -1;
		if ( slot < 0 ) return;

		PlaceCarriedHost( position, slot );
	}

	[Rpc.Host]
	public void RequestPlaceCarriedAtSlot( Vector3 position, int slot )
	{
		if ( !RpcAuthorization.IsOwnerCaller() ) return;
		PlaceCarriedHost( position, slot );
	}

	private void PlaceCarriedHost( Vector3 position, int slot )
	{
		var inv = FindOwnerInventory();
		if ( inv is null ) return;

		var carried = inv.GetCarriedAt( slot );
		if ( string.IsNullOrEmpty( carried ) ) return;

		var def = Defs.Animal( carried );
		if ( def is null ) return;

		// AUDIT FIX B7: Ignore forged far positions — snap to caller feet when
		// the claimed place point is beyond interact range of the player.
		// Habitat.FindAt still uses the (possibly clamped) position.
		if ( RpcAuthorization.TryGetCallerFeet( out var feet ) )
		{
			var placeRange = GameConstants.InteractionRange + GameConstants.TileSize;
			if ( RpcAuthorization.HorizontalDistance( feet, position ) > placeRange )
				position = feet;
		}

		var plots = PlotSystem.Instance;
		if ( plots is not null && AnimalRegistry.Count >= plots.AnimalCap )
		{
			ZooState.Instance?.Notify( "Animal capacity reached — buy more land!", "warning" );
			return;
		}

		var habitat = HabitatRegistry.FindAt( position );
		if ( habitat is null )
		{
			ZooState.Instance?.Notify( "Place inside a habitat.", "fence" );
			return;
		}

		if ( !habitat.TryAccept( def, null, out var err ) )
		{
			ZooState.Instance?.Notify( err, "block" );
			return;
		}

		if ( !inv.TryTakeCarriedAt( slot, out var speciesId ) || speciesId != carried )
			return;

		if ( !AnimalSystem.Instance?.TrySpawnCaught( def, habitat, position ) ?? true )
		{
			inv.TryAddCatch( speciesId );
			return;
		}

		ZooState.Instance?.Notify( $"Released {def.DisplayName} into the habitat!", "pets" );
		SaveSystem.Instance?.RequestSave();
	}
}
