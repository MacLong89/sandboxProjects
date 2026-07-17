namespace RunGun;

/// <summary>
/// Live stats for the current run. Build stats are pumped by gates; meta upgrades and
/// character choice set starting values. Combo, overdrive, and scoring layer on top.
/// </summary>
public sealed class RunState
{
	private readonly UpgradeSystem _upgrades;
	private readonly CharacterSystem _characters;
	private readonly DailyChallengeSystem _daily;

	public RunState( UpgradeSystem upgrades, CharacterSystem characters, DailyChallengeSystem daily )
	{
		_upgrades = upgrades;
		_characters = characters;
		_daily = daily;
	}

	public bool Active { get; set; }

	// --- The crowd: your runners ARE your firepower and your life ---
	public float Squad { get; set; }
	public float SquadPeak { get; set; }
	public int SquadInt => (int)MathF.Round( Squad );

	// --- Per-runner quality ---
	public float Damage { get; set; }
	public float FireRateMult { get; set; } = 1f;
	public int Multishot { get; set; } = 1;
	public int Pierce { get; set; }
	public float CritChance { get; set; }
	public float Shield { get; set; }
	public float RunCoinMult { get; set; } = 1f;

	public double Coins { get; set; }
	public float StartX { get; set; }
	public float FurthestX { get; set; }

	// Pulse hooks so the HUD/VFX can react to the crowd changing size.
	public float SquadFlashTimer { get; set; }
	public int LastSquadDelta { get; set; }

	// --- Skill layer ---
	public int ComboCount { get; set; }
	public int PeakCombo { get; set; }
	public int NoHitStreak { get; set; }
	public float ComboDecayTimer { get; set; }

	public float OverdriveMeter { get; set; }
	public bool OverdriveActive { get; set; }
	public float OverdriveTimeLeft { get; set; }

	// --- Scoring / tracking ---
	public int KillCount { get; set; }
	public int EliteKillCount { get; set; }
	public int BossesKilled { get; set; }
	public int GatesCrossed { get; set; }
	public int MultGatesCrossed { get; set; }
	public double Score { get; set; }
	public float LastScoreMeters { get; set; }
	public int LastMilestoneShown { get; set; }
	public int PendingMilestone { get; set; }

	public float DistanceMeters => MathF.Max( 0f, (FurthestX - StartX) / GameConstants.UnitsPerMeter );
	public float SquadFraction => SquadPeak <= 0f ? 0f : MathF.Min( 1f, Squad / SquadPeak );
	public float ComboMultiplier => MathF.Min( GameConstants.ComboMaxMult, 1f + ComboCount * GameConstants.ComboMultPerKill );
	public float BuildBuffMult => OverdriveActive ? GameConstants.OverdriveBuildMult : 1f;
	public double EffectiveCoinMult => _upgrades.CoinMult * RunCoinMult
		* (_daily.ActiveModifier == DailyModifier.BonusCoins ? 2f : 1f)
		* (OverdriveActive ? GameConstants.OverdriveCoinMult : 1f) * ComboMultiplier;

	public void Begin( float startX )
	{
		var character = _characters.ActiveDef;

		Damage = GameConstants.BaseDamage + _upgrades.StartDamageBonus + character.DamageBonus;
		FireRateMult = _upgrades.FireRateMult * character.FireRateMult;
		Multishot = GameConstants.BaseMultishot;
		Pierce = _upgrades.PierceBonus + character.PierceBonus;
		CritChance = MathF.Min( GameConstants.CritChanceCap, _upgrades.CritChance + character.CritBonus );
		Shield = 0f;
		RunCoinMult = 1f + character.CoinMultBonus;

		// Character MultishotBonus now seeds extra starting runners.
		Squad = MathF.Max( 1f, GameConstants.StartSquad + _upgrades.StartSquadBonus + character.MultishotBonus );
		SquadPeak = Squad;
		SquadFlashTimer = 0f;
		LastSquadDelta = 0;
		Coins = 0;
		StartX = startX;
		FurthestX = startX;

		ComboCount = 0;
		PeakCombo = 0;
		NoHitStreak = 0;
		ComboDecayTimer = 0f;
		OverdriveMeter = 0f;
		OverdriveActive = false;
		OverdriveTimeLeft = 0f;

		KillCount = 0;
		EliteKillCount = 0;
		BossesKilled = 0;
		GatesCrossed = 0;
		MultGatesCrossed = 0;
		Score = 0;
		LastScoreMeters = 0;
		LastMilestoneShown = 0;
		PendingMilestone = 0;

		Active = true;
	}

	public float FireInterval
	{
		get
		{
			var raw = GameConstants.BaseFireInterval / FireRateMult;
			if ( OverdriveActive ) raw *= GameConstants.OverdriveFireMult;
			return MathF.Max( GameConstants.MinFireInterval, raw );
		}
	}

	public float BulletDamage
	{
		get
		{
			// Runners beyond the on-screen/bullet cap still count — they pile on damage.
			var overflow = MathF.Max( 0f, Squad - GameConstants.MaxBulletLanes );
			var overflowMult = 1f + overflow * GameConstants.SquadOverflowDamagePer;
			return MathF.Max( 0.5f, Damage * _upgrades.DamageMult * ComboMultiplier * BuildBuffMult * overflowMult );
		}
	}

	/// <summary>Bullets fired per volley — one lane per runner, capped for performance.</summary>
	public int BulletsPerShot
	{
		get
		{
			var lanes = Math.Clamp( SquadInt + (Multishot - 1), 1, GameConstants.MaxBulletLanes );
			if ( OverdriveActive ) lanes = Math.Min( GameConstants.MaxBulletLanes, lanes + 3 );
			return lanes;
		}
	}

	public void ApplyGateEffect( BuildStat stat, GateOp op, float value )
	{
		switch ( stat )
		{
			case BuildStat.Squad:
				if ( op == GateOp.Mult )
					MultSquad( value );
				else if ( value >= 0f )
					AddSquad( (int)MathF.Round( value ) );
				else
					LoseSquadUnavoidable( (int)MathF.Round( -value ) );
				break;
			case BuildStat.Damage:
				Damage = op == GateOp.Add ? Damage + value : Damage * value;
				break;
			case BuildStat.FireRate:
				FireRateMult = op == GateOp.Add ? FireRateMult + value : FireRateMult * value;
				break;
			case BuildStat.Multishot:
				Multishot = Math.Clamp( Multishot + (int)value, 1, GameConstants.MaxBulletsPerShot );
				break;
			case BuildStat.Pierce:
				Pierce = Math.Clamp( Pierce + (int)value, 0, GameConstants.MaxPierce );
				break;
			case BuildStat.CritChance:
				CritChance = MathF.Min( GameConstants.CritChanceCap, CritChance + value );
				break;
			case BuildStat.Shield:
				Shield += value;
				break;
			case BuildStat.Heal:
				AddSquad( (int)MathF.Round( value * 0.4f ) );
				break;
			case BuildStat.CoinMult:
				RunCoinMult = op == GateOp.Add ? RunCoinMult + value : RunCoinMult * value;
				break;
		}
	}

	public float PumpGateValue( BuildStat stat, GateOp op, float value )
	{
		// Trap gates stay fixed — shooting them shouldn't "fix" a -8 into -2.
		if ( stat == BuildStat.Squad && op == GateOp.Add && value < 0f )
			return value;

		if ( stat == BuildStat.Squad && op == GateOp.Mult )
			return MathF.Min( GameConstants.GateSquadMultCap, value + GameConstants.GateSquadMultPump );

		if ( stat == BuildStat.Squad )
			return MathF.Min( GameConstants.GateSquadAddCap, value + GameConstants.GateSquadAddPump );

		if ( op == GateOp.Mult )
			return MathF.Min( GameConstants.GateMultCap, value + GameConstants.GateMultPumpPerHit );

		return stat switch
		{
			BuildStat.Damage => MathF.Min( GameConstants.GateDamageCap, value + GameConstants.GateDamagePump ),
			BuildStat.FireRate => MathF.Min( GameConstants.GateFireRateCap, value + GameConstants.GateFireRatePump ),
			BuildStat.Multishot => MathF.Min( GameConstants.GateMultishotCap, value + 1f ),
			BuildStat.Pierce => MathF.Min( GameConstants.GatePierceCap, value + 1f ),
			BuildStat.CritChance => MathF.Min( GameConstants.GateCritCap, value + GameConstants.GateCritPump ),
			BuildStat.Shield => MathF.Min( GameConstants.GateShieldCap, value + GameConstants.GateShieldPump ),
			BuildStat.Heal => MathF.Min( GameConstants.GateHealCap, value + GameConstants.GateHealPump ),
			BuildStat.CoinMult => MathF.Min( GameConstants.GateCoinCap, value + GameConstants.GateCoinPump ),
			_ => value + GameConstants.GateAddPumpPerHit,
		};
	}

	/// <summary>How much wider the mob is for hazard/enemy collisions — fat crowds clip more.</summary>
	public float CrowdFat =>
		MathF.Min( GameConstants.CrowdFatCap, Squad * GameConstants.CrowdFatPerRunner );

	public bool RollCrit() => Game.Random.Float( 0f, 1f ) < CritChance;

	public float ResolveDamage( float baseDamage, out bool crit )
	{
		crit = RollCrit();
		return crit ? baseDamage * GameConstants.CritDamageMult : baseDamage;
	}

	public void OnKill( Enemy enemy )
	{
		ComboCount++;
		PeakCombo = Math.Max( PeakCombo, ComboCount );
		if ( ComboCount % 5 == 0 ) Sfx.Play( Sfx.Combo );
		ComboDecayTimer = GameConstants.ComboDecayTime;
		KillCount++;

		if ( enemy.IsElite ) EliteKillCount++;
		if ( enemy.Type == EnemyType.Boss ) BossesKilled++;

		OverdriveMeter = MathF.Min( GameConstants.OverdriveMax, OverdriveMeter + GameConstants.OverdrivePerKill * _upgrades.OverdriveChargeMult );
		Score += GameConstants.ScorePerKill * ComboMultiplier;

		// "Recruiter" meta: a chance each kill drafts a fallen enemy into your crowd.
		if ( _upgrades.LifestealFraction > 0f && Game.Random.Float( 0f, 1f ) < _upgrades.LifestealFraction )
			AddSquad( 1 );
	}

	public void OnGateCross( bool isMult )
	{
		GatesCrossed++;
		if ( isMult ) MultGatesCrossed++;
		OverdriveMeter = MathF.Min( GameConstants.OverdriveMax, OverdriveMeter + GameConstants.OverdrivePerGate * _upgrades.OverdriveChargeMult );
	}

	public void OnGateHit() =>
		OverdriveMeter = MathF.Min( GameConstants.OverdriveMax, OverdriveMeter + GameConstants.OverdrivePerGateHit * _upgrades.OverdriveChargeMult );

	public void AddSquad( int count )
	{
		if ( count <= 0 ) return;
		Squad = MathF.Min( GameConstants.MaxSquad, Squad + count );
		SquadPeak = MathF.Max( SquadPeak, Squad );
		LastSquadDelta = count;
		SquadFlashTimer = 0.4f;
	}

	public void MultSquad( float mult )
	{
		if ( mult <= 1f ) { AddSquad( 0 ); return; }
		var before = SquadInt;
		Squad = MathF.Min( GameConstants.MaxSquad, Squad * mult );
		SquadPeak = MathF.Max( SquadPeak, Squad );
		LastSquadDelta = SquadInt - before;
		SquadFlashTimer = 0.5f;
	}

	/// <summary>A threat kills part of your crowd. Shield absorbs runner-for-runner first.</summary>
	public void LoseSquad( int count )
	{
		if ( OverdriveActive || count <= 0 ) return;

		if ( Shield > 0f )
		{
			var absorbed = MathF.Min( Shield, count );
			Shield -= absorbed;
			count -= (int)absorbed;
		}

		if ( count <= 0 ) return;

		Squad = MathF.Max( 0f, Squad - count );
		LastSquadDelta = -count;
		SquadFlashTimer = 0.35f;
		ComboCount = 0;
		NoHitStreak = 0;
		ComboDecayTimer = 0f;
	}

	/// <summary>Hazards and red gates bypass shield and Surge. Bad movement must always cost crew.</summary>
	public void LoseSquadUnavoidable( int count )
	{
		if ( count <= 0 ) return;

		var before = SquadInt;
		Squad = MathF.Max( 0f, Squad - count );
		LastSquadDelta = SquadInt - before;
		SquadFlashTimer = 0.45f;
		ComboCount = 0;
		NoHitStreak = 0;
		ComboDecayTimer = 0f;
	}

	/// <summary>Hazards remove most of the current mob and can wipe a small group outright.</summary>
	public int HazardSquadCost() =>
		Math.Max( GameConstants.HazardSquadLossMin, (int)MathF.Ceiling( Squad * GameConstants.HazardSquadLossFraction ) );

	public void Tick( float dt )
	{
		if ( OverdriveActive )
		{
			OverdriveTimeLeft -= dt;
			if ( OverdriveTimeLeft <= 0f )
				OverdriveActive = false;
		}

		if ( SquadFlashTimer > 0f ) SquadFlashTimer = MathF.Max( 0f, SquadFlashTimer - dt );

		ComboDecayTimer -= dt;
		if ( ComboDecayTimer <= 0f && ComboCount > 0 )
		{
			ComboCount = Math.Max( 0, ComboCount - 1 );
			if ( ComboCount > 0 )
				ComboDecayTimer = GameConstants.ComboDecayTime * 0.5f;
		}

		UpdateScore();
		CheckMilestones();
	}

	public bool TryActivateOverdrive()
	{
		if ( OverdriveActive || OverdriveMeter < GameConstants.OverdriveCost ) return false;
		OverdriveActive = true;
		OverdriveTimeLeft = GameConstants.OverdriveDuration;
		OverdriveMeter -= GameConstants.OverdriveCost;
		return true;
	}

	private void UpdateScore()
	{
		var meters = DistanceMeters;
		var delta = meters - LastScoreMeters;
		if ( delta > 0f )
		{
			Score += delta * GameConstants.ScorePerMeter * ComboMultiplier;
			LastScoreMeters = meters;
		}

		NoHitStreak = (int)meters;
	}

	private void CheckMilestones()
	{
		var milestone = (int)(DistanceMeters / GameConstants.MilestoneIntervalMeters);
		if ( milestone > LastMilestoneShown )
		{
			LastMilestoneShown = milestone;
			PendingMilestone = milestone * (int)GameConstants.MilestoneIntervalMeters;
		}
	}
}
