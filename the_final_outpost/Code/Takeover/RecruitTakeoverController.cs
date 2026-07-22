namespace FinalOutpost;

public enum RecruitTakeoverMode
{
	None,
	Picking,
	Possessing,
	WatchingAfterDeath
}

public enum RecruitTakeoverPendingKind
{
	None,
	SurvivalNight,
	CureThreat,
	RivalAssault
}

/// <summary>
/// Gates night/threat start behind a recruit picker, then optionally possesses one recruit
/// in first person while the rest stay simulated.
/// </summary>
public sealed class RecruitTakeoverController : Component
{
	public static RecruitTakeoverController Instance { get; private set; }

	public RecruitTakeoverMode Mode { get; private set; }
	public RecruitTakeoverPendingKind PendingKind { get; private set; }
	public float PendingThreatMult { get; private set; } = 1f;
	public string PendingBossLabel { get; private set; }
	public bool PendingAdvanceProgress { get; private set; } = true;
	public int PossessedSaveIndex { get; private set; } = -1;
	public bool ShowDeathPopup { get; private set; }

	public TakeoverPawn Pawn { get; private set; }

	public bool IsPicking => Mode == RecruitTakeoverMode.Picking;
	public bool IsPossessing => Mode == RecruitTakeoverMode.Possessing;
	public bool IsTakeoverUiOpen => IsPicking || ShowDeathPopup;
	public bool BlocksIsometricCamera => IsPossessing;

	protected override void OnDestroy()
	{
		if ( Instance == this ) Instance = null;
		DestroyPawn();
	}

	protected override void OnAwake()
	{
		Instance = this;
		if ( Components.Get<TakeoverCursorGuard>( FindMode.EverythingInSelf ) is null )
			Components.Create<TakeoverCursorGuard>();
	}

	protected override void OnUpdate()
	{
		if ( !IsPossessing )
			return;

		TakeoverCursor.Sync();

		if ( Pawn is null || !Pawn.IsValid() )
			return;

		var unit = FindUnit( PossessedSaveIndex );
		if ( unit is null || !unit.IsAlive )
		{
			NotifyPossessedDied();
			return;
		}

		Pawn.SyncToUnit( unit );
	}

	/// <summary>Called by <see cref="GameCore"/> after it opens the picker gate.</summary>
	public void BeginPending(
		RecruitTakeoverPendingKind kind,
		float threatMult = 1f,
		string bossLabel = null,
		bool advanceProgress = true )
	{
		PendingKind = kind;
		PendingThreatMult = Math.Max( 1f, threatMult );
		PendingBossLabel = bossLabel;
		PendingAdvanceProgress = advanceProgress;
		Mode = RecruitTakeoverMode.Picking;
		ShowDeathPopup = false;
		PossessedSaveIndex = -1;
	}

	/// <summary>Begin recruit picker before Survival night combat. Returns true if gated (UI open).</summary>
	public bool TryBeginSurvivalNightPick() =>
		GameCore.Instance?.TryOpenTakeoverPicker( RecruitTakeoverPendingKind.SurvivalNight ) == true;

	/// <summary>Begin recruit picker before a Cure threat. Returns true if gated (UI open).</summary>
	public bool TryBeginCureThreatPick( float threatMult, string bossLabel, bool advanceProgress ) =>
		GameCore.Instance?.TryOpenTakeoverPicker(
			RecruitTakeoverPendingKind.CureThreat,
			threatMult,
			bossLabel,
			advanceProgress ) == true;

	/// <summary>Begin recruit picker before a rival plot assault.</summary>
	public bool TryBeginRivalAssaultPick() =>
		GameCore.Instance?.TryOpenTakeoverPicker( RecruitTakeoverPendingKind.RivalAssault ) == true;

	public IReadOnlyList<TakeoverRecruitChoice> BuildChoices()
	{
		var list = new List<TakeoverRecruitChoice>();
		var core = GameCore.Instance;
		var defenders = DefenderManager.Instance;
		if ( core?.Save?.Recruits is null ) return list;

		for ( var i = 0; i < core.Save.Recruits.Count; i++ )
		{
			var type = RecruitWeapons.Parse( core.Save.Recruits[i] );
			var def = RecruitWeapons.Get( type );
			var hp = i < core.Save.RecruitHealth.Count
				? core.Save.RecruitHealth[i]
				: DefenderManager.MaxRecruitHealth();
			var maxHp = DefenderManager.MaxRecruitHealth();
			var train = defenders?.TrainLevelOf( type ) ?? 0;
			list.Add( new TakeoverRecruitChoice
			{
				SaveIndex = i,
				Type = type,
				Name = def.Name,
				ShortName = def.ShortName,
				Icon = def.Icon,
				Health = hp,
				MaxHealth = maxHp,
				TrainLevel = train
			} );
		}

		return list;
	}

	public void ConfirmSimulate()
	{
		Mode = RecruitTakeoverMode.None;
		ShowDeathPopup = false;
		PossessedSaveIndex = -1;
		GameCore.Instance?.ClearTakeoverPicker();
		CommitPendingCombat( possessIndex: -1 );
	}

	public void ConfirmPossess( int saveIndex )
	{
		var core = GameCore.Instance;
		if ( core is null ) return;
		if ( saveIndex < 0 || saveIndex >= (core.Save.Recruits?.Count ?? 0) )
		{
			ConfirmSimulate();
			return;
		}

		Mode = RecruitTakeoverMode.None;
		ShowDeathPopup = false;
		PossessedSaveIndex = saveIndex;
		GameCore.Instance?.ClearTakeoverPicker();
		CommitPendingCombat( possessIndex: saveIndex );
		BeginPossession( saveIndex );
	}

	public void NotifyPossessedDied()
	{
		if ( Mode != RecruitTakeoverMode.Possessing )
			return;

		RestorePossessedBodyFromPawn();
		DestroyPawn();
		ClearPossessedFlag();
		PossessedSaveIndex = -1;
		Mode = RecruitTakeoverMode.WatchingAfterDeath;
		ShowDeathPopup = true;
		RestoreIsometricCamera();
		UnitOrderController.Instance?.CancelNightCommandMode();
	}

	public void DismissDeathPopup()
	{
		ShowDeathPopup = false;
		Mode = RecruitTakeoverMode.None;
	}

	/// <summary>Force exit possession (night clear, core death, return to menu).</summary>
	public void ExitTakeoverFully()
	{
		RestorePossessedBodyFromPawn();
		DestroyPawn();
		ClearPossessedFlag();
		PossessedSaveIndex = -1;
		ShowDeathPopup = false;
		PendingKind = RecruitTakeoverPendingKind.None;
		Mode = RecruitTakeoverMode.None;
		GameCore.Instance?.ClearTakeoverPicker();
		RestoreIsometricCamera();
	}

	private void CommitPendingCombat( int possessIndex )
	{
		var core = GameCore.Instance;
		if ( core is null ) return;

		var kind = PendingKind;
		var mult = PendingThreatMult;
		var boss = PendingBossLabel;
		var advance = PendingAdvanceProgress;
		PendingKind = RecruitTakeoverPendingKind.None;

		if ( kind == RecruitTakeoverPendingKind.SurvivalNight )
			core.CommitSurvivalNightStart();
		else if ( kind == RecruitTakeoverPendingKind.CureThreat )
			core.CommitCureThreatStart( mult, boss, advance );
		else if ( kind == RecruitTakeoverPendingKind.RivalAssault )
			core.CommitRivalAssaultStart();

		_ = possessIndex;
	}

	private void BeginPossession( int saveIndex )
	{
		var unit = FindUnit( saveIndex );
		if ( unit is null || !unit.IsAlive )
		{
			Mode = RecruitTakeoverMode.None;
			return;
		}

		var spawnPos = unit.WorldPos;
		var spawnAim = unit.Aim;

		PossessedSaveIndex = saveIndex;
		unit.IsPossessed = true;
		unit.Character?.SetVisible( false );
		// Ghost other allies for walk-through; keep YOUR body present so zombies can target/hit you.
		OutpostHitboxes.SetHierarchyCollisionEnabled( unit.Go, true );
		SetOtherRecruitCollisionEnabled( false );

		DestroyPawn();
		var go = new GameObject( true, "TakeoverPawn" );
		go.WorldPosition = spawnPos;
		go.WorldRotation = spawnAim;
		Pawn = go.Components.Create<TakeoverPawn>();
		Pawn.Possess( unit );

		Mode = RecruitTakeoverMode.Possessing;
		ShowDeathPopup = false;

		var core = GameCore.Instance;
		core?.CloseShop();
		core?.CloseSettings();
		core?.CloseCredits();
		core?.CloseLeaderboard();
		core?.CloseRecruit();
		core?.CloseWorkers();
		core?.CloseExpeditions();
		core?.CloseMilestones();
		core?.CloseCatalog();
		core?.CloseLegacy();
		core?.CloseTechTree();
		core?.CloseCureProgress();

		if ( OutpostCamera.Instance is not null )
			OutpostCamera.Instance.GameObject.Enabled = false;

		UnitOrderController.Instance?.CancelNightCommandMode();
		Log.Info( $"[FinalOutpost][Takeover] BeginPossession saveIndex={saveIndex} unit={unit.Type} at {spawnPos}" );
		TakeoverCursor.EnterFps();
	}

	void RestorePossessedBodyFromPawn()
	{
		var unit = FindUnit( PossessedSaveIndex );
		if ( unit is null ) return;

		if ( Pawn is not null && Pawn.IsValid() )
			Pawn.RestoreUnitBody( unit );
		else if ( unit.Go.IsValid() )
			OutpostHitboxes.SetHierarchyCollisionEnabled( unit.Go, true );

		SetOtherRecruitCollisionEnabled( true );
	}

	/// <summary>Disable/enable colliders on non-possessed recruits only (FP walk-through).</summary>
	static void SetOtherRecruitCollisionEnabled( bool enabled )
	{
		foreach ( var u in DefenderManager.Instance?.Units ?? Array.Empty<DefenderManager.DefenderUnit>() )
		{
			if ( u?.Go is null || !u.Go.IsValid() ) continue;
			if ( u.IsPossessed ) continue;
			OutpostHitboxes.SetHierarchyCollisionEnabled( u.Go, enabled );
		}
	}

	private void DestroyPawn()
	{
		if ( Pawn is not null && Pawn.IsValid() )
			Pawn.GameObject.Destroy();
		Pawn = null;
	}

	private void ClearPossessedFlag()
	{
		foreach ( var u in DefenderManager.Instance?.Units ?? Array.Empty<DefenderManager.DefenderUnit>() )
		{
			if ( !u.IsPossessed ) continue;
			u.IsPossessed = false;
			u.Character?.SetVisible( true );
			if ( u.Go.IsValid() )
				OutpostHitboxes.SetHierarchyCollisionEnabled( u.Go, true );
		}

		SetOtherRecruitCollisionEnabled( true );
	}

	private static void RestoreIsometricCamera()
	{
		Log.Info( "[FinalOutpost][Takeover] RestoreIsometricCamera" );
		TakeoverCursor.ExitFps( "RestoreIsometricCamera" );

		if ( OutpostCamera.Instance is not null )
		{
			OutpostCamera.Instance.GameObject.Enabled = true;
			OutpostCamera.Instance.MakeMainCamera();
		}
	}

	private static DefenderManager.DefenderUnit FindUnit( int saveIndex )
	{
		if ( saveIndex < 0 ) return null;
		foreach ( var u in DefenderManager.Instance?.Units ?? Array.Empty<DefenderManager.DefenderUnit>() )
		{
			if ( u.SaveIndex == saveIndex && u.IsAlive )
				return u;
		}

		return null;
	}
}

public sealed class TakeoverRecruitChoice
{
	public int SaveIndex { get; init; }
	public RecruitWeaponType Type { get; init; }
	public string Name { get; init; }
	public string ShortName { get; init; }
	public string Icon { get; init; }
	public float Health { get; init; }
	public float MaxHealth { get; init; }
	public int TrainLevel { get; init; }
}
