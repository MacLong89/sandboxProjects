namespace SkyEmpire;

public sealed class Toast
{
	public string Message { get; set; }
	public TimeSince Age { get; set; }
}

/// <summary>
/// The local player's entire progression. Purely client-side — every lobby
/// member grinds their own island — persisted to FileSystem.Data.
/// </summary>
public sealed class PlayerProgress : Component
{
	public static PlayerProgress Local { get; private set; }
	const string SaveFile = "sky_empire_save.json";

	public SaveData Data { get; private set; } = new();
	public double SessionEarned { get; private set; }
	public readonly List<Toast> Toasts = new();

	// Offline welcome payload for the UI.
	public double OfflineCashEarned { get; private set; }
	public double OfflineSecondsAway { get; private set; }
	public int StreakGemsGranted { get; private set; }

	// Session boosts.
	public TimeUntil FrenzyLeft { get; private set; }
	public bool FrenzyActive => FrenzyLeft > 0f;
	public bool OverdriveActive => Data.OverdriveUntilUnix > DateTimeOffset.UtcNow.ToUnixTimeSeconds();
	public double OverdriveSecondsLeft => Math.Max( 0, Data.OverdriveUntilUnix - DateTimeOffset.UtcNow.ToUnixTimeSeconds() );

	// Friend boost — set each frame by TycoonGame.
	public bool FriendBoostActive { get; set; }

	// Playtime chest.
	public TimeUntil ChestReady { get; private set; }

	TimeUntil _autoSave;
	TimeUntil _slowTick;
	bool _dirty;

	/// <summary>Bumped whenever the purchase set changes so visuals know to rebuild.</summary>
	public int PurchaseStamp { get; private set; }

	// ---------------- Lifecycle ----------------

	protected override void OnAwake()
	{
		Local = this;
		FrenzyLeft = 0f;
		LoadGame();
		// First-ever chest charges fast so the milestone chain never stalls.
		ChestReady = Data.ChestsClaimed == 0 ? 150f : Balance.ChestIntervalMinutes * 60f;
	}

	protected override void OnDestroy()
	{
		SaveNow();
		if ( Local == this ) Local = null;
	}

	protected override void OnUpdate()
	{
		if ( _slowTick )
		{
			_slowTick = 1f;
			Milestones.Tick( this );
			DailyQuestGen.EnsureToday( Data );
		}

		if ( Toasts.Count > 0 && Toasts[0].Age > 4.5f )
			Toasts.RemoveAt( 0 );

		if ( _dirty && _autoSave )
			SaveNow();
	}

	void LoadGame()
	{
		try
		{
			if ( FileSystem.Data.FileExists( SaveFile ) )
			{
				var loaded = Json.Deserialize<SaveData>( FileSystem.Data.ReadAllText( SaveFile ) );
				if ( loaded is not null ) Data = loaded;
			}
		}
		catch ( Exception e )
		{
			Log.Warning( $"[SkyEmpire] Save load failed, starting fresh: {e.Message}" );
			Data = new SaveData();
		}

		Data.Purchased ??= new List<string>();

		ApplyOfflineEarnings();
		ApplyLoginStreak();
		DailyQuestGen.EnsureToday( Data );
		PurchaseStamp++;
		RequestSave();
	}

	public void RequestSave() { _dirty = true; _autoSave = 2f; }

	public void SaveNow()
	{
		try
		{
			Data.LastSeenUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
			FileSystem.Data.WriteAllText( SaveFile, Json.Serialize( Data ) );
			_dirty = false;
		}
		catch ( Exception e )
		{
			Log.Warning( $"[SkyEmpire] Save failed: {e.Message}" );
		}
	}

	// ---------------- Derived ----------------

	public double RebirthMult => RebirthCatalog.IncomeMult( Data.Rebirths );

	public double BoostMult =>
		(OverdriveActive ? Balance.OverdriveMult : 1.0)
		* (FriendBoostActive ? Balance.FriendBoostMult : 1.0);

	/// <summary>Average earned per second right now — HUD rate and offline basis.</summary>
	public double IncomePerSecond =>
		PurchaseCatalog.IncomePerSecond( Data.Purchased, RebirthMult )
		* BoostMult
		* (FrenzyActive ? Balance.FrenzyRateMult : 1.0);

	public double RebirthCost => RebirthCatalog.Cost( Data.Rebirths );
	public bool CanRebirth => Data.Cash >= RebirthCost;
	public bool IslandComplete => Data.Purchased.Count >= PurchaseCatalog.All.Length;

	// ---------------- Core loop ----------------

	/// <summary>An orb reached the furnace. Value has arch + island mults baked in.</summary>
	public void CollectOrb( double bakedValue, bool golden )
	{
		var value = bakedValue * RebirthMult * BoostMult;
		Data.Cash += value;
		Data.LifetimeCash += value;
		SessionEarned += value;
		Data.OrbsCollected++;

		if ( golden )
		{
			Data.GoldenOrbs++;
			if ( Game.Random.Float() < Balance.GoldenGemChance )
			{
				Data.Gems++;
				AddToast( "Golden orb dropped +1 💎!" );
			}
		}

		DailyQuestGen.OnOrb( Data, value, golden );
		RequestSave();
	}

	public bool TryPurchase( PurchaseDef def )
	{
		if ( def is null || Data.Purchased.Contains( def.Id ) ) return false;
		if ( def.Requires != "" && !Data.Purchased.Contains( def.Requires ) ) return false;
		if ( Data.Cash < def.Cost ) return false;

		Data.Cash -= def.Cost;
		Data.Purchased.Add( def.Id );
		Data.LifetimePurchases++;
		PurchaseStamp++;

		Sfx.Play( def.Kind == PurchaseKind.Floor ? "unlock" : "buy" );
		if ( def.Kind == PurchaseKind.Floor )
			AddToast( $"🏗️ {def.Name} unlocked!" );

		DailyQuestGen.OnBuy( Data );
		RequestSave();
		return true;
	}

	public void Rebirth()
	{
		if ( !CanRebirth ) return;

		Data.Cash = 0;
		Data.Purchased.Clear();
		Data.Rebirths++;
		Data.OverdriveUntilUnix = 0;
		PurchaseStamp++;

		var tier = RebirthCatalog.Tier( Data.Rebirths );
		AddToast( $"🌟 REBIRTH #{Data.Rebirths}! +{Balance.RebirthIncomeBonus:P0} income forever. Welcome to {tier.Name}!" );
		Sfx.Play( "rebirth" );

		var player = TycoonPlayer.Local;
		if ( player.IsValid() )
			player.RespawnAtPlot();

		RequestSave();
	}

	// ---------------- Gem boosts ----------------

	public bool TryBuyOverdrive()
	{
		if ( OverdriveActive ) { AddToast( "Overdrive is already running!" ); return false; }
		if ( Data.Gems < Balance.OverdriveGemCost ) { AddToast( $"Overdrive needs {Balance.OverdriveGemCost} 💎." ); return false; }
		Data.Gems -= Balance.OverdriveGemCost;
		Data.OverdriveUntilUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + (long)(Balance.OverdriveMinutes * 60);
		AddToast( $"⚡ OVERDRIVE! ×{Balance.OverdriveMult:0} income for {Balance.OverdriveMinutes:0} minutes!" );
		Sfx.Play( "unlock" );
		RequestSave();
		return true;
	}

	public bool TryBuyFrenzy()
	{
		if ( FrenzyActive ) { AddToast( "Orb Frenzy is already running!" ); return false; }
		if ( Data.Gems < Balance.FrenzyGemCost ) { AddToast( $"Orb Frenzy needs {Balance.FrenzyGemCost} 💎." ); return false; }
		Data.Gems -= Balance.FrenzyGemCost;
		FrenzyLeft = Balance.FrenzySeconds;
		AddToast( $"🌧️ ORB FRENZY! Droppers go ×{Balance.FrenzyRateMult:0} speed for {Balance.FrenzySeconds:0}s!" );
		Sfx.Play( "unlock" );
		RequestSave();
		return true;
	}

	// ---------------- Playtime chest ----------------

	public bool ChestClaimable => ChestReady <= 0f;

	public void ClaimChest()
	{
		if ( !ChestClaimable ) return;
		ChestReady = Balance.ChestIntervalMinutes * 60f;
		Data.ChestsClaimed++;

		var cash = Math.Max( 120, PurchaseCatalog.IncomePerSecond( Data.Purchased, RebirthMult ) * 90 );
		Data.Cash += cash;
		Data.LifetimeCash += cash;

		var msg = $"🎁 Sky Chest: +{Balance.Fmt( cash )} cash";
		if ( Game.Random.Float() < Balance.ChestGemChance )
		{
			Data.Gems += 2;
			msg += " +2 💎";
		}
		AddToast( msg + "!" );
		Sfx.Play( "chest" );
		RequestSave();
	}

	// ---------------- Offline / streak ----------------

	void ApplyOfflineEarnings()
	{
		if ( Data.LastSeenUnix <= 0 ) return;
		var away = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - Data.LastSeenUnix;
		if ( away < 60 ) return;

		var capped = Math.Min( away, Balance.OfflineMaxHours * 3600 );
		var earned = PurchaseCatalog.IncomePerSecond( Data.Purchased, RebirthMult ) * capped * Balance.OfflineEarningsRate;
		if ( earned <= 0 ) return;

		Data.Cash += earned;
		Data.LifetimeCash += earned;
		OfflineCashEarned = earned;
		OfflineSecondsAway = away;
	}

	void ApplyLoginStreak()
	{
		var today = DateTime.UtcNow.ToString( "yyyy-MM-dd" );
		if ( Data.LastLoginDay == today ) return;

		var yesterday = DateTime.UtcNow.AddDays( -1 ).ToString( "yyyy-MM-dd" );
		if ( Data.LastLoginDay == yesterday || string.IsNullOrEmpty( Data.LastLoginDay ) )
			Data.StreakCount = string.IsNullOrEmpty( Data.LastLoginDay ) ? 1 : Data.StreakCount + 1;
		else
			Data.StreakCount = 1;

		Data.LastLoginDay = today;

		var gems = Balance.StreakGems[Math.Min( Data.StreakCount - 1, Balance.StreakGems.Length - 1 )];
		Data.Gems += gems;
		StreakGemsGranted = gems;
	}

	public void AddToast( string message )
	{
		Toasts.Add( new Toast { Message = message, Age = 0 } );
		if ( Toasts.Count > 5 ) Toasts.RemoveAt( 0 );
	}
}
