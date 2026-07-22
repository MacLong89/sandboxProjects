namespace FinalOutpost;

/// <summary>Result of a completed scout expedition, shown to the player on collect.</summary>
public struct ExpeditionReward
{
	public double Scrap;
	public double Wood;
	public double Stone;
	public double Water;
	public double Specimens;
	public bool RareFind;
	public int Lost;       // units that didn't make it back (long expeditions only)
	public int Returned;
	public bool Any => Scrap > 0 || Wood > 0 || Stone > 0 || Water > 0 || Specimens > 0;
}

/// <summary>
/// Sends a party of your actual people (soldiers + civilians) beyond the walls to scavenge. The units
/// are genuinely committed — they leave the base and are unavailable until they return. Runs on a real
/// timer so it resolves while the player is away. Short trips are safe with modest loot; long trips pay
/// far more but risk losing people (soldiers lower the odds, civilians boost the haul) — a clean
/// risk/reward decision.
///
/// AUDIT FIX H2 (2026-07): TryStart used to pull Recruits from the list end WITHOUT moving the
/// matching RecruitHealth slot. Returnees then inherited leftover HP indices → wrong health on rebuild.
/// ExpeditionSoldierHealth is now kept parallel to ExpeditionSoldiers.
/// </summary>
public sealed class ExpeditionManager : Component
{
	public static ExpeditionManager Instance { get; private set; }

	public ExpeditionReward LastReward { get; private set; }
	public bool HasResult { get; private set; }

	protected override void OnAwake() => Instance = this;

	protected override void OnDestroy()
	{
		if ( Instance == this ) Instance = null;
	}

	private SaveData Save => GameCore.Instance?.Save;

	public bool IsActive => Save?.ExpeditionActive == true;
	public int Party => Save?.ExpeditionParty ?? 0;
	public bool IsLong => Save?.ExpeditionLong == true;
	public int CommittedSoldiers => Save?.ExpeditionSoldiers.Count ?? 0;
	public int CommittedCivilians => Save?.ExpeditionWorkers.Count ?? 0;

	public int AvailableSoldiers => DefenderManager.Instance?.Count ?? 0;
	public int AvailableCivilians => WorkerManager.Instance?.Count ?? 0;

	public float SecondsRemaining
	{
		get
		{
			if ( !IsActive ) return 0f;
			var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
			return MathF.Max( 0f, Save.ExpeditionEndUnix - now );
		}
	}

	public bool IsReadyToCollect => IsActive && SecondsRemaining <= 0f;

	public double ProvisionCost( int party ) => party * GameConstants.ExpeditionProvisionPerScout;

	public bool CanStart( int soldiers, int civilians, bool isLong )
	{
		var core = GameCore.Instance;
		if ( core is null || core.Phase != GamePhase.Day ) return false;
		if ( IsActive ) return false;

		var party = soldiers + civilians;
		if ( party < GameConstants.ExpeditionMinParty || party > GameConstants.ExpeditionMaxParty ) return false;
		if ( soldiers < 0 || civilians < 0 ) return false;
		if ( soldiers > AvailableSoldiers || civilians > AvailableCivilians ) return false;
		if ( isLong ? !NightUnlocks.IsLongExpeditionUnlocked( core.Save ) : !NightUnlocks.IsShortExpeditionUnlocked( core.Save ) )
			return false;

		return SelectHelp.CanAfford( ProvisionCost( party ) );
	}

	public bool TryStart( int soldiers, int civilians, bool isLong )
	{
		var core = GameCore.Instance;
		if ( !CanStart( soldiers, civilians, isLong ) ) return false;

		var party = soldiers + civilians;
		if ( !core.Wallet.TrySpend( ProvisionCost( party ) ) ) return false;

		// Commit real units: pull them out of the base rosters and stash them on the expedition.
		Save.ExpeditionSoldiers.Clear();
		Save.ExpeditionSoldierHealth ??= new List<float>();
		Save.ExpeditionSoldierHealth.Clear();
		Save.ExpeditionWorkers.Clear();

		for ( var i = 0; i < soldiers && Save.Recruits.Count > 0; i++ )
		{
			var last = Save.Recruits.Count - 1;
			Save.ExpeditionSoldiers.Add( Save.Recruits[last] );

			// AUDIT FIX H2: move HP with the recruit so return/RespawnAll stay index-aligned.
			if ( last < Save.RecruitHealth.Count )
			{
				Save.ExpeditionSoldierHealth.Add( Save.RecruitHealth[last] );
				Save.RecruitHealth.RemoveAt( last );
			}
			else
			{
				// Defensive fallback — should not happen after Migrate alignment.
				Save.ExpeditionSoldierHealth.Add( DefenderManager.MaxRecruitHealth() );
			}

			Save.Recruits.RemoveAt( last );
		}

		for ( var i = 0; i < civilians && Save.Workers.Count > 0; i++ )
		{
			var last = Save.Workers.Count - 1;
			Save.ExpeditionWorkers.Add( Save.Workers[last] );
			Save.Workers.RemoveAt( last );
		}

		var seconds = isLong ? GameConstants.ExpeditionLongSeconds : GameConstants.ExpeditionShortSeconds;
		if ( core.IsCure )
			seconds = (long)(seconds * TeamBonuses.ExpeditionDurationMult( core ));
		Save.ExpeditionActive = true;
		Save.ExpeditionParty = Save.ExpeditionSoldiers.Count + Save.ExpeditionWorkers.Count;
		Save.ExpeditionLong = isLong;
		Save.ExpeditionEndUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + (long)seconds;
		Save.EverSentScouts = true;
		HasResult = false;

		DefenderManager.Instance?.RebuildFromSave();
		WorkerManager.Instance?.RebuildFromSave();

		Sfx.Play( Sfx.WaveStart );
		core.SaveManagerTouch();
		return true;
	}

	public bool TryCollect()
	{
		var core = GameCore.Instance;
		if ( core is null || !IsReadyToCollect ) return false;

		var soldiers = Save.ExpeditionSoldiers.Count;
		var civilians = Save.ExpeditionWorkers.Count;
		var reward = RollReward( soldiers, civilians, Save.ExpeditionLong, core.CombatProgressionNight );

		// Resolve survivors: on long trips each committed unit rolls against a loss chance
		// (reduced by how many soldiers came along).
		var lossChance = 0.0;
		if ( Save.ExpeditionLong )
			lossChance = Math.Max( 0.02, GameConstants.ExpeditionLongLossChance - soldiers * GameConstants.ExpeditionSafetyPerSoldier );

		Save.ExpeditionSoldierHealth ??= new List<float>();

		var lost = 0;
		for ( var i = 0; i < Save.ExpeditionSoldiers.Count; i++ )
		{
			var type = Save.ExpeditionSoldiers[i];
			var hp = i < Save.ExpeditionSoldierHealth.Count
				? Save.ExpeditionSoldierHealth[i]
				: DefenderManager.MaxRecruitHealth();

			if ( lossChance > 0 && Game.Random.Float( 0f, 1f ) < lossChance )
			{
				lost++;
				continue;
			}

			// AUDIT FIX H2: restore type + HP together (same order as Recruits list).
			Save.Recruits.Add( type );
			Save.RecruitHealth.Add( hp );
		}

		foreach ( var w in Save.ExpeditionWorkers )
		{
			if ( lossChance > 0 && Game.Random.Float( 0f, 1f ) < lossChance ) { lost++; continue; }
			Save.Workers.Add( w );
		}

		reward.Lost = lost;
		reward.Returned = (soldiers + civilians) - lost;

		core.Wallet.Earn( reward.Scrap );
		core.Resources.Add( ResourceKind.Wood, reward.Wood );
		core.Resources.Add( ResourceKind.Stone, reward.Stone );
		core.Resources.Add( ResourceKind.Water, reward.Water );

		if ( core.IsCure )
		{
			var specimenRoll = Save.ExpeditionLong ? 2 : 1;
			if ( Game.Random.Float( 0f, 1f ) < 0.55f )
			{
				reward.Specimens = Math.Round( specimenRoll * TeamBonuses.ExpeditionRewardMult( core ) );
				core.Resources.Add( ResourceKind.Specimens, reward.Specimens );
			}
		}

		LastReward = reward;
		HasResult = true;

		Save.ExpeditionActive = false;
		Save.ExpeditionParty = 0;
		Save.ExpeditionLong = false;
		Save.ExpeditionEndUnix = 0;
		Save.ExpeditionSoldiers.Clear();
		Save.ExpeditionSoldierHealth.Clear();
		Save.ExpeditionWorkers.Clear();

		DefenderManager.Instance?.RebuildFromSave();
		WorkerManager.Instance?.RebuildFromSave();

		Sfx.Play( Sfx.WaveClear );
		core.SaveManagerTouch();
		return true;
	}

	public void ClearResult() => HasResult = false;

	private static ExpeditionReward RollReward( int soldiers, int civilians, bool isLong, int night )
	{
		var party = soldiers + civilians;
		var scrapPer = isLong ? GameConstants.ExpeditionScrapPerScoutLong : GameConstants.ExpeditionScrapPerScoutShort;
		var scrap = party * scrapPer * (1.0 + night * 0.04) * Game.Random.Float( 0.85f, 1.15f );

		var reward = new ExpeditionReward();

		if ( isLong )
		{
			var lootBoost = 1.0 + civilians * GameConstants.ExpeditionLootBoostPerCivilian;
			var res = party * GameConstants.ExpeditionResourcePerScoutLong * lootBoost * Game.Random.Float( 0.8f, 1.2f );
			var core = GameCore.Instance;
			if ( core?.IsCure == true )
			{
				reward.Wood = Math.Round( res * 0.55 );
				reward.Stone = Math.Round( res * 0.45 );
				reward.Water = 0;
			}
			else
			{
				// Survival: fold material haul into scrap.
				scrap += res * GameConstants.CraftsmanScrapPerResource;
			}
		}

		reward.Scrap = Math.Round( scrap );
		reward.RareFind = isLong && Game.Random.Float( 0f, 1f ) < 0.12f;
		if ( reward.RareFind )
			reward.Scrap += Math.Round( scrap * 0.5 );

		var game = GameCore.Instance;
		if ( game?.IsCure == true )
		{
			var mult = TeamBonuses.ExpeditionRewardMult( game );
			reward.Scrap = Math.Round( reward.Scrap * mult );
			reward.Wood = Math.Round( reward.Wood * mult );
			reward.Stone = Math.Round( reward.Stone * mult );
		}

		return reward;
	}
}
