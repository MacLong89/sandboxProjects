namespace CatchACritter;

public sealed class Toast
{
	public string Message { get; set; }
	public string Icon { get; set; }
	public TimeSince Age { get; set; }
}

/// <summary>
/// The local player's entire progression. Purely client-side (each lobby member
/// grinds their own save, Mow-the-Lawn style), persisted to FileSystem.Data.
/// </summary>
public sealed class PlayerProgress : Component
{
	public static PlayerProgress Local { get; private set; }
	const string SaveFile = "catch_a_critter_save.json";

	public SaveData Data { get; private set; } = new();
	public int SessionCatches { get; private set; }
	public readonly List<Toast> Toasts = new();

	// Offline welcome payload for the UI.
	public double OfflineCoinsEarned { get; private set; }
	public double OfflineSecondsAway { get; private set; }
	public int StreakGemsGranted { get; private set; }

	TimeUntil _autoSave;
	TimeUntil _incomeTick;
	bool _dirty;

	// ---------------- Lifecycle ----------------

	protected override void OnAwake()
	{
		Local = this;
		LoadGame();
	}

	protected override void OnDestroy()
	{
		SaveNow();
		if ( Local == this ) Local = null;
	}

	protected override void OnUpdate()
	{
		if ( _incomeTick )
		{
			_incomeTick = 1f;
			TickSanctuaryIncome( 1.0 );
			CheckEggs();
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
			Log.Warning( $"[Critter] Save load failed, starting fresh: {e.Message}" );
			Data = new SaveData();
		}

		Data.UnlockedZones ??= new List<string> { "Meadow" };
		if ( !Data.UnlockedZones.Contains( "Meadow" ) ) Data.UnlockedZones.Add( "Meadow" );

		ApplyOfflineEarnings();
		ApplyLoginStreak();
		DailyQuestGen.EnsureToday( Data );
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
			Log.Warning( $"[Critter] Save failed: {e.Message}" );
		}
	}

	// ---------------- Derived stats ----------------

	public int TalentRank( string id ) => Data.Talents.GetValueOrDefault( id );

	float FollowerBuff( int channel )
	{
		// channel: 0 speed, 1 luck, 2 sell
		var mult = 1f + TalentRank( "k_followbuff" ) * 0.15f;
		var total = 0f;
		foreach ( var id in Data.FollowerIds )
		{
			var critter = Data.Sanctuary.FirstOrDefault( c => c.Id == id );
			var def = critter?.Def;
			if ( def is null ) continue;
			var strength = ((int)def.Rarity + 1) * (critter.Shiny ? 1.6f : 1f);
			total += channel switch
			{
				0 => 0.015f * strength,
				1 => 0.02f * strength,
				_ => 0.02f * strength,
			};
		}
		return total * mult;
	}

	public int NetPower => Data.NetPower;
	public NetDef Net => NetCatalog.Get( Data.NetPower );

	public float MoveSpeed => (Balance.BaseWalkSpeed + Data.SpeedLevel * Balance.SpeedPerLevel)
		* (1f + TalentRank( "h_speed" ) * 0.05f + FollowerBuff( 0 ));

	public float CatchRadius => Net.Radius * (1f + TalentRank( "h_radius" ) * 0.06f)
		* (1f + TalentRank( "h_magnet" ) * 0.25f);

	public float SwingCooldown => Net.Cooldown * (1f - TalentRank( "h_cooldown" ) * 0.05f).Clamp( 0.3f, 1f );

	public float Luck01 => ((Data.LuckLevel / (float)Balance.LuckMaxLevel) * 0.6f
		+ TalentRank( "f_luck" ) * 0.1f + FollowerBuff( 1 )).Clamp( 0f, 1f );

	public float ShinyChance => (Balance.ShinyBaseChance
		+ Data.LuckLevel * Balance.ShinyLuckBonusPerLevel)
		* (1f + TalentRank( "f_shiny" ) * 0.15f);

	public int BackpackCapacity => Balance.BaseBackpack + Data.BackpackLevel * Balance.BackpackPerLevel;
	public int BackpackCount => Data.Backpack.Count;

	public double SellMultiplier => (1.0 + Data.Crowns * Balance.AscendSellBonus)
		* (1.0 + TalentRank( "f_sell" ) * 0.08)
		* (1.0 + FollowerBuff( 2 ))
		* (1.0 + CompletedBiomeCount() * 0.10);

	public int SanctuarySlots => Balance.BaseSanctuarySlots + TalentRank( "k_slots" ) * 2 + Data.Crowns;
	public int FollowerSlots => 1 + TalentRank( "k_followers" );
	public int EggSlots => 2;

	public double SanctuaryIncomePerSecond
	{
		get
		{
			double total = 0;
			foreach ( var c in Data.Sanctuary )
				total += c.IncomePerSecond;
			return total * (1.0 + TalentRank( "k_income" ) * 0.10);
		}
	}

	public double AscendCost => Balance.AscendBaseCost * Math.Pow( Balance.AscendCostGrowth, Data.Crowns );

	public double Coins => Data.Coins;

	public int CompletedBiomeCount()
	{
		var count = 0;
		foreach ( var biome in BiomeCatalog.All )
		{
			var complete = SpeciesCatalog.InBiome( biome.Id )
				.All( s => Data.Codex.TryGetValue( s.Id, out var e ) && e.Caught > 0 );
			if ( complete ) count++;
		}
		return count;
	}

	// ---------------- Core loop ----------------

	public void OnCaught( SpeciesDef def, bool shiny )
	{
		if ( def is null ) return;

		Data.Backpack.Add( new BackpackItem { SpeciesId = def.Id, Shiny = shiny } );

		var entry = Data.Codex.TryGetValue( def.Id, out var e ) ? e : Data.Codex[def.Id] = new CodexEntry();
		var firstCatch = entry.Caught == 0;
		entry.Caught++;
		if ( shiny ) { entry.ShinyCaught++; Data.LifetimeShinies++; }
		Data.LifetimeCatches++;
		SessionCatches++;

		if ( firstCatch )
			AddToast( $"New species discovered: {def.Name}!", "📖" );

		// Gem drops on rare+ catches.
		var gemChance = (int)def.Rarity >= (int)Rarity.Epic ? Balance.GemChanceEpicPlus
			: def.Rarity == Rarity.Rare ? Balance.GemChanceRare : 0f;
		gemChance *= 1f + TalentRank( "f_gems" ) * 0.12f;
		if ( shiny ) gemChance += 0.35f;
		if ( gemChance > 0 && Game.Random.Float() < gemChance )
		{
			var gems = 1 + (int)def.Rarity / 2;
			Data.Gems += gems;
			AddToast( $"+{gems} 💎", "💎" );
		}

		// Double scoop talent.
		if ( Game.Random.Float() < TalentRank( "h_double" ) * 0.07f && Data.Backpack.Count < BackpackCapacity )
		{
			Data.Backpack.Add( new BackpackItem { SpeciesId = def.Id, Shiny = shiny } );
			AddToast( "Double Scoop! Caught two!", "🍀" );
		}

		DailyQuestGen.OnCatch( Data, def, shiny );
		CritterPlayer.Local?.RecordCatch();
		RequestSave();
	}

	public double BackpackValue()
	{
		double total = 0;
		foreach ( var item in Data.Backpack )
		{
			var def = SpeciesCatalog.Get( item.SpeciesId );
			if ( def is null ) continue;
			total += def.BaseValue * (item.Shiny ? Balance.ShinyValueMult : 1.0);
		}
		return total * SellMultiplier;
	}

	public void SellBackpack()
	{
		if ( Data.Backpack.Count == 0 )
		{
			AddToast( "Backpack is empty — go catch critters!", "🎒" );
			return;
		}

		var value = BackpackValue();

		// Jackpot talent.
		if ( Game.Random.Float() < TalentRank( "f_jackpot" ) * 0.005f )
		{
			value *= 100;
			AddToast( "JACKPOT! This sale pays 100x!", "🎰" );
			Sfx.Play( "shiny" );
		}

		var count = Data.Backpack.Count;
		Data.Backpack.Clear();
		Data.SellCount++;
		AddCoins( value );
		DailyQuestGen.OnSell( Data, value );
		AddToast( $"Sold {count} critters for {Balance.Fmt( value )} coins!", "💰" );
		Sfx.Play( "sell" );

		var player = CritterPlayer.Local;
		if ( player.IsValid() )
			CatchEffects.FloatText( player.WorldPosition + Vector3.Up * 100f, $"+{Balance.Fmt( value )} coins", new Color( 1f, 0.9f, 0.4f ), 13f );

		RequestSave();
	}

	public void AddCoins( double amount )
	{
		Data.Coins += amount;
		if ( amount > 0 ) Data.LifetimeCoins += amount;
		RequestSave();
	}

	// ---------------- Shop ----------------

	public bool TryBuyNet()
	{
		if ( Data.NetPower >= NetCatalog.All.Length - 1 ) return false;
		var next = NetCatalog.All[Data.NetPower + 1];
		if ( Data.Coins < next.Cost ) return false;
		Data.Coins -= next.Cost;
		Data.NetPower++;
		AddToast( $"Equipped {next.Name}!", "🥅" );
		Sfx.Play( "buy" );
		RequestSave();
		return true;
	}

	public bool TryBuySpeed()
	{
		if ( Data.SpeedLevel >= Balance.SpeedMaxLevel ) return false;
		var cost = Balance.SpeedCost( Data.SpeedLevel );
		if ( Data.Coins < cost ) return false;
		Data.Coins -= cost; Data.SpeedLevel++;
		Sfx.Play( "buy" ); RequestSave();
		return true;
	}

	public bool TryBuyBackpack()
	{
		if ( Data.BackpackLevel >= Balance.BackpackMaxLevel ) return false;
		var cost = Balance.BackpackCost( Data.BackpackLevel );
		if ( Data.Coins < cost ) return false;
		Data.Coins -= cost; Data.BackpackLevel++;
		Sfx.Play( "buy" ); RequestSave();
		return true;
	}

	public bool TryBuyLuck()
	{
		if ( Data.LuckLevel >= Balance.LuckMaxLevel ) return false;
		var cost = Balance.LuckCost( Data.LuckLevel );
		if ( Data.Coins < cost ) return false;
		Data.Coins -= cost; Data.LuckLevel++;
		Sfx.Play( "buy" ); RequestSave();
		return true;
	}

	// ---------------- Zones ----------------

	public bool IsZoneUnlocked( Biome b ) => Data.UnlockedZones.Contains( b.ToString() );

	public void TryUnlockZone( Biome b )
	{
		if ( IsZoneUnlocked( b ) ) return;
		var def = BiomeCatalog.Get( b );
		if ( Data.Coins < def.GateCost )
		{
			AddToast( $"Need {Balance.Fmt( def.GateCost )} coins for {def.Name}.", "🔒" );
			return;
		}

		Data.Coins -= def.GateCost;
		Data.UnlockedZones.Add( b.ToString() );
		AddToast( $"Unlocked {def.Name}! New critters await!", "🌍" );
		Sfx.Play( "unlock" );
		RequestSave();
	}

	// ---------------- Sanctuary / breeding / followers ----------------

	public bool KeepFromBackpack( int index )
	{
		if ( index < 0 || index >= Data.Backpack.Count ) return false;
		if ( Data.Sanctuary.Count >= SanctuarySlots )
		{
			AddToast( "Sanctuary is full! Expand with talents or Ascend.", "🏡" );
			return false;
		}

		var item = Data.Backpack[index];
		Data.Backpack.RemoveAt( index );
		Data.Sanctuary.Add( new OwnedCritter { SpeciesId = item.SpeciesId, Shiny = item.Shiny } );
		AddToast( $"{SpeciesCatalog.Get( item.SpeciesId )?.Name} joined your sanctuary!", "🏡" );
		RequestSave();
		return true;
	}

	public void ReleaseFromSanctuary( string id )
	{
		var critter = Data.Sanctuary.FirstOrDefault( c => c.Id == id );
		if ( critter is null ) return;
		var def = critter.Def;
		var refund = (def?.BaseValue ?? 0) * (critter.Shiny ? Balance.ShinyValueMult : 1.0) * SellMultiplier;
		Data.Sanctuary.Remove( critter );
		Data.FollowerIds.Remove( id );
		AddCoins( refund );
		AddToast( $"Released {def?.Name} for {Balance.Fmt( refund )} coins.", "💰" );
	}

	public void ToggleFollower( string id )
	{
		if ( Data.FollowerIds.Contains( id ) )
		{
			Data.FollowerIds.Remove( id );
		}
		else
		{
			if ( Data.FollowerIds.Count >= FollowerSlots )
			{
				AddToast( FollowerSlots == 1
					? "Only 1 follower slot. Unlock more in Talents."
					: $"Only {FollowerSlots} follower slots. Unlock more in Talents.", "🐾" );
				return;
			}
			Data.FollowerIds.Add( id );
			Sfx.Play( "catch" );
		}
		RequestSave();
	}

	public string FollowerCsv()
	{
		var parts = new List<string>();
		foreach ( var id in Data.FollowerIds )
		{
			var critter = Data.Sanctuary.FirstOrDefault( c => c.Id == id );
			if ( critter?.Def is null ) continue;
			parts.Add( $"{critter.SpeciesId}:{(critter.Shiny ? 1 : 0)}" );
		}
		return string.Join( ",", parts );
	}

	public bool CanBreed( string idA, string idB, out string reason )
	{
		reason = "";
		var a = Data.Sanctuary.FirstOrDefault( c => c.Id == idA );
		var b = Data.Sanctuary.FirstOrDefault( c => c.Id == idB );
		if ( a is null || b is null || idA == idB ) { reason = "Pick two critters."; return false; }
		if ( a.SpeciesId != b.SpeciesId ) { reason = "Same species only."; return false; }
		if ( Data.Eggs.Count >= EggSlots ) { reason = "Egg nest is full."; return false; }
		var cost = BreedCost( a.Def );
		if ( Data.Coins < cost ) { reason = $"Costs {Balance.Fmt( cost )} coins."; return false; }
		return true;
	}

	public double BreedCost( SpeciesDef def ) => (def?.BaseValue ?? 0) * 6.0;

	public void Breed( string idA, string idB )
	{
		if ( !CanBreed( idA, idB, out var reason ) ) { AddToast( reason, "🥚" ); return; }
		var a = Data.Sanctuary.First( c => c.Id == idA );
		var b = Data.Sanctuary.First( c => c.Id == idB );
		var def = a.Def;

		Data.Coins -= BreedCost( def );
		var minutes = (Balance.EggMinutesBase + (int)def.Rarity * Balance.EggMinutesPerRarity)
			* (1f - TalentRank( "k_egg" ) * 0.08f).Clamp( 0.3f, 1f );

		Data.Eggs.Add( new EggData
		{
			SpeciesId = def.Id,
			ShinyParent = a.Shiny || b.Shiny,
			Generation = Math.Max( a.Generation, b.Generation ) + 1,
			StartUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
			DurationSeconds = minutes * 60,
		} );

		AddToast( $"An egg! Hatches in {Balance.FmtTime( minutes * 60 )}.", "🥚" );
		Sfx.Play( "buy" );
		RequestSave();
	}

	public void InstantHatch( int eggIndex )
	{
		if ( eggIndex < 0 || eggIndex >= Data.Eggs.Count ) return;
		if ( Data.Gems < Balance.GemInstantHatchCost )
		{
			AddToast( $"Needs {Balance.GemInstantHatchCost} 💎 (catch rare critters!).", "💎" );
			return;
		}
		Data.Gems -= Balance.GemInstantHatchCost;
		Data.Eggs[eggIndex].DurationSeconds = 0;
		CheckEggs();
	}

	void CheckEggs()
	{
		for ( int i = Data.Eggs.Count - 1; i >= 0; i-- )
		{
			var egg = Data.Eggs[i];
			if ( !egg.Ready ) continue;
			Data.Eggs.RemoveAt( i );

			var def = SpeciesCatalog.Get( egg.SpeciesId );
			if ( def is null ) continue;

			var shinyChance = ShinyChance * Balance.BredShinyBonus * (egg.ShinyParent ? 2f : 1f);
			var shiny = Game.Random.Float() < shinyChance;

			if ( Data.Sanctuary.Count >= SanctuarySlots )
			{
				// No room — hatchling is sold on arrival.
				var value = def.BaseValue * (shiny ? Balance.ShinyValueMult : 1.0) * SellMultiplier * 2;
				AddCoins( value );
				AddToast( $"Egg hatched, sanctuary full — sold for {Balance.Fmt( value )}!", "🐣" );
			}
			else
			{
				Data.Sanctuary.Add( new OwnedCritter { SpeciesId = def.Id, Shiny = shiny, Generation = egg.Generation } );
				var entry = Data.Codex.TryGetValue( def.Id, out var e ) ? e : Data.Codex[def.Id] = new CodexEntry();
				entry.Bred++;
				if ( shiny ) AddToast( $"A SHINY {def.Name} hatched!", "✨" );
				else AddToast( $"A Gen {egg.Generation} {def.Name} hatched!", "🐣" );
				Sfx.Play( "shiny" );
			}

			DailyQuestGen.OnHatch( Data );
			RequestSave();
		}
	}

	// ---------------- Talents / Prestige ----------------

	public bool TryBuyTalent( string id )
	{
		var def = TalentCatalog.Get( id );
		if ( def is null ) return false;
		var rank = TalentRank( id );
		if ( rank >= def.MaxRank ) return false;
		if ( Data.TalentPoints < def.CostPerRank ) return false;
		Data.TalentPoints -= def.CostPerRank;
		Data.Talents[id] = rank + 1;
		Sfx.Play( "buy" );
		RequestSave();
		return true;
	}

	public bool CanAscend => Data.Coins >= AscendCost;

	public void Ascend()
	{
		if ( !CanAscend ) return;

		Data.Coins = 0;
		Data.NetPower = 0;
		Data.SpeedLevel = 0;
		Data.BackpackLevel = 0;
		Data.LuckLevel = 0;
		Data.Backpack.Clear();
		Data.UnlockedZones = new List<string> { "Meadow" };

		Data.Crowns++;
		Data.TalentPoints += Balance.TalentPointsPerAscend;

		AddToast( $"Ascended! Crown {Data.Crowns}: +{Balance.AscendSellBonus * 100:0}% sell forever, +{Balance.TalentPointsPerAscend} talent points!", "👑" );
		Sfx.Play( "unlock" );

		var player = CritterPlayer.Local;
		if ( player.IsValid() )
			player.WorldPosition = Vector3.Up * 10f;

		RequestSave();
	}

	// ---------------- Passive / retention ----------------

	void TickSanctuaryIncome( double seconds )
	{
		var income = SanctuaryIncomePerSecond * seconds;
		if ( income <= 0 ) return;
		Data.Coins += income;
		Data.LifetimeCoins += income;
	}

	void ApplyOfflineEarnings()
	{
		if ( Data.LastSeenUnix <= 0 ) return;
		var away = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - Data.LastSeenUnix;
		if ( away < 60 ) return;

		var capped = Math.Min( away, Balance.OfflineMaxHours * 3600 );
		var rate = Balance.OfflineEarningsRate * (1.0 + TalentRank( "k_offline" ) * 0.10);
		var earned = SanctuaryIncomePerSecond * capped * rate;
		if ( earned <= 0 ) return;

		Data.Coins += earned;
		Data.LifetimeCoins += earned;
		OfflineCoinsEarned = earned;
		OfflineSecondsAway = away;
	}

	void ApplyLoginStreak()
	{
		var today = DateTime.UtcNow.ToString( "yyyy-MM-dd" );
		if ( Data.LastLoginDay == today ) return;

		var yesterday = DateTime.UtcNow.AddDays( -1 ).ToString( "yyyy-MM-dd" );
		var dayBefore = DateTime.UtcNow.AddDays( -2 ).ToString( "yyyy-MM-dd" );
		var graceDays = TalentRank( "f_streaksave" );

		if ( Data.LastLoginDay == yesterday || (graceDays >= 1 && Data.LastLoginDay == dayBefore) || string.IsNullOrEmpty( Data.LastLoginDay ) && Data.StreakCount == 0 )
			Data.StreakCount = string.IsNullOrEmpty( Data.LastLoginDay ) ? 1 : Data.StreakCount + 1;
		else
			Data.StreakCount = 1;

		Data.LastLoginDay = today;

		var gems = Balance.StreakGems[Math.Min( Data.StreakCount - 1, Balance.StreakGems.Length - 1 )];
		Data.Gems += gems;
		StreakGemsGranted = gems;
	}

	public void AddToast( string message, string icon )
	{
		Toasts.Add( new Toast { Message = message, Icon = icon, Age = 0 } );
		// Keep the HUD toast band short so stacked notices stay fully on-screen.
		while ( Toasts.Count > 3 ) Toasts.RemoveAt( 0 );
	}
}
