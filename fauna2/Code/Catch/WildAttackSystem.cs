namespace Fauna2;

using Fauna2.UI;

/// <summary>Host-authoritative wild predator encounter with a short fend-off timing challenge.</summary>
public sealed class WildAttackSystem : Component
{
	public static WildAttackSystem Instance { get; private set; }

	[Sync( SyncFlags.FromHost )] public bool EncounterActive { get; set; }
	[Sync] public string ActiveWildId { get; set; } = "";
	[Sync] public float BarPosition { get; set; }
	[Sync] public float GreenStart { get; set; }
	[Sync] public float GreenEnd { get; set; }
	[Sync] public float BarSpeed { get; set; }
	[Sync] public string ThreatSpeciesName { get; set; } = "";
	[Sync] public string ThreatLabel { get; set; } = "";
	[Sync] public int HitsRequired { get; set; }
	[Sync] public int HitsLanded { get; set; }
	[Sync] public int PenaltyPreview { get; set; }

	private WildAnimalComponent _activeWild;
	private PlayerState _targetPlayer;
	private bool _barAscending = true;

	protected override void OnAwake() => Instance = this;
	protected override void OnDestroy() { if ( Instance == this ) Instance = null; }

	protected override void OnFixedUpdate()
	{
		if ( IsProxy || !EncounterActive ) return;

		var bar = BarPosition;
		TimingBarMinigame.TickBar( ref bar, ref _barAscending, BarSpeed, Time.Delta );
		BarPosition = bar;
	}

	public void TryBeginAttack( WildAnimalComponent wild, PlayerState target )
	{
		if ( !Networking.IsHost ) return;
		if ( wild is null || !wild.IsValid() || wild.Fled || target is null || !target.IsValid() ) return;
		if ( EncounterActive || CatchSystem.Instance?.MinigameActive == true ) return;

		var def = wild.Definition;
		if ( def is null || def.WildAggression <= 0f ) return;

		_activeWild = wild;
		_targetPlayer = target;
		ActiveWildId = wild.WildId;
		EncounterActive = true;
		BarPosition = 0f;
		_barAscending = true;

		var difficulty = DifficultyFor( def );
		var zoneWidth = GreenZoneWidth( difficulty );
		float greenStart;
		float greenEnd;
		TimingBarMinigame.RandomizeGreenZone( out greenStart, out greenEnd, zoneWidth );
		GreenStart = greenStart;
		GreenEnd = greenEnd;
		BarSpeed = BarSpeedFor( difficulty );
		HitsRequired = RequiredHits( difficulty );
		HitsLanded = 0;
		ThreatSpeciesName = def.DisplayName;
		ThreatLabel = $"Dangerous {CatchDifficulty.RarityLabel( def.Rarity )}";
		PenaltyPreview = CalculatePenalty( def );

		UiState.OpenCatchMinigame();
		ZooSoundEffects.PlayPlacementError();
		ZooState.Instance?.Notify( $"{def.DisplayName} attacks! Fend it off!", "warning" );
	}

	public void TryResolveFend()
	{
		if ( !EncounterActive || PlayerState.Local?.IsZooOwner != true ) return;

		if ( Networking.IsHost )
			ResolveFendHost();
		else
			RequestResolveFend();
	}

	[Rpc.Host]
	private void RequestResolveFend()
	{
		if ( !RpcAuthorization.IsOwnerCaller() ) return;
		ResolveFendHost();
	}

	public void TryGiveUp()
	{
		if ( !EncounterActive || PlayerState.Local?.IsZooOwner != true ) return;

		if ( Networking.IsHost )
			KnockOutHost();
		else
			RequestGiveUp();
	}

	[Rpc.Host]
	private void RequestGiveUp()
	{
		if ( !RpcAuthorization.IsOwnerCaller() ) return;
		KnockOutHost();
	}

	private void ResolveFendHost()
	{
		if ( !EncounterActive || _activeWild is null || !_activeWild.IsValid() || _activeWild.Fled )
		{
			CancelEncounterHost();
			return;
		}

		var def = _activeWild.Definition;
		if ( def is null )
		{
			CancelEncounterHost();
			return;
		}

		var success = BarPosition >= GreenStart && BarPosition <= GreenEnd;
		if ( !success )
		{
			KnockOutHost();
			return;
		}

		HitsLanded++;
		if ( HitsLanded < HitsRequired )
		{
			AdvanceRound( def );
			ZooSoundEffects.PlayUiClick();
			return;
		}

		ZooState.Instance?.AddXp( GameConstants.XpCatchAnimal / 2 );
		ZooState.Instance?.Notify( $"You drove off the {def.DisplayName}!", "shield" );
		ZooSoundEffects.PlayDiscovery();
		_activeWild.Flee();
		CancelEncounterHost();
	}

	private void KnockOutHost()
	{
		if ( !EncounterActive )
			return;

		var def = _activeWild?.Definition;
		var speciesName = def?.DisplayName ?? "wild animal";
		var penalty = def is null ? GameConstants.WildAnimalAttackMinPenalty : CalculatePenalty( def );
		penalty = Math.Min( penalty, ZooState.Instance?.Money ?? 0 );

		if ( penalty > 0 )
		{
			ZooState.Instance?.ApplyOperatingExpense( penalty );
			SaveSystem.Instance?.RequestSave();
		}

		_targetPlayer ??= FindOwnerPlayer();
		_targetPlayer?.Components.Get<ZooPlayerController>()?.TeleportToSpawnPoint();

		ZooSoundEffects.PlayPlacementError();
		ZooState.Instance?.Notify( $"Knocked out by a {speciesName}. Lost ${penalty:n0}.", "local_hospital" );
		_activeWild?.Flee();
		CancelEncounterHost();
	}

	private void CancelEncounterHost()
	{
		EncounterActive = false;
		_activeWild = null;
		_targetPlayer = null;
		ActiveWildId = "";
		ThreatSpeciesName = "";
		ThreatLabel = "";
		HitsRequired = 0;
		HitsLanded = 0;
		PenaltyPreview = 0;
		UiState.CloseCatchMinigame();
	}

	private void AdvanceRound( AnimalDefinition def )
	{
		var difficulty = (DifficultyFor( def ) + HitsLanded * 0.06f).Clamp( 0.05f, 0.98f );
		var zoneWidth = GreenZoneWidth( difficulty );
		var bar = BarPosition;
		float greenStart;
		float greenEnd;
		TimingBarMinigame.AdvanceRound( ref bar, ref _barAscending, out greenStart, out greenEnd, zoneWidth, BarSpeedFor( difficulty ) );
		BarPosition = bar;
		GreenStart = greenStart;
		GreenEnd = greenEnd;
		BarSpeed = BarSpeedFor( difficulty );
	}

	/// <summary>
	/// AUDIT FIX B13: Cancel by connection / missing owner — same soft-lock race as CatchSystem.
	/// Does not apply knockout penalty on disconnect (leave = forfeit the encounter cleanly).
	/// </summary>
	public void CancelIfOwnerDisconnected( Connection channel )
	{
		if ( !EncounterActive || channel is null ) return;

		var leavingId = channel.SteamId.Value;
		var ownerStillPresent = false;
		foreach ( var ps in Game.ActiveScene.GetAllComponents<PlayerState>() )
		{
			if ( !ps.IsValid() || !ps.IsZooOwner ) continue;
			if ( ps.SteamId == leavingId )
			{
				CancelEncounterHost();
				return;
			}

			ownerStillPresent = true;
		}

		if ( !ownerStillPresent
			|| (Connection.Host is not null && channel == Connection.Host)
			|| (Networking.IsHost && channel == Connection.Local) )
		{
			CancelEncounterHost();
		}
	}

	private int CalculatePenalty( AnimalDefinition def )
	{
		var state = ZooState.Instance;
		var fraction = def.WildAttackPenaltyFraction > 0f
			? def.WildAttackPenaltyFraction
			: GameConstants.WildAnimalAttackBasePenaltyFraction + DifficultyFor( def ) * 0.08f;

		var money = state?.Money ?? 0;
		var penalty = (int)MathF.Ceiling( money * fraction );
		penalty = Math.Max( penalty, GameConstants.WildAnimalAttackMinPenalty );
		return penalty.Clamp( 0, GameConstants.WildAnimalAttackMaxPenalty );
	}

	private static float DifficultyFor( AnimalDefinition def ) =>
		MathF.Max( def.WildAttackDifficulty, CatchDifficulty.For( def ) ).Clamp( 0.05f, 0.98f );

	private static float GreenZoneWidth( float difficulty ) =>
		(0.36f - difficulty * 0.22f).Clamp( 0.08f, 0.34f );

	private static float BarSpeedFor( float difficulty ) =>
		(0.58f + difficulty * 1.15f).Clamp( 0.48f, 1.72f );

	private static int RequiredHits( float difficulty ) =>
		(2 + (int)MathF.Ceiling( difficulty * 3f )).Clamp( 2, 5 );

	private static PlayerState FindOwnerPlayer()
	{
		foreach ( var ps in Game.ActiveScene.GetAllComponents<PlayerState>() )
		{
			if ( ps.IsValid() && ps.IsZooOwner )
				return ps;
		}

		return PlayerState.Local;
	}
}
