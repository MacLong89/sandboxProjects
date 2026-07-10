namespace Terraingen.Animals;

using Sandbox.Network;
using Terraingen.GameData;
using Terraingen.Multiplayer;

public sealed partial class ThornsAnimalBrain
{
	[Sync( SyncFlags.FromHost )] public int TameLevel { get; private set; } = 1;
	[Sync( SyncFlags.FromHost )] public int TameExperience { get; private set; }
	[Sync( SyncFlags.FromHost )] public int UnspentStatPoints { get; private set; }
	[Sync( SyncFlags.FromHost )] public int StatStrength { get; private set; }
	[Sync( SyncFlags.FromHost )] public int StatDefense { get; private set; }
	[Sync( SyncFlags.FromHost )] public int StatStamina { get; private set; }
	[Sync( SyncFlags.FromHost )] public int StatAgility { get; private set; }
	[Sync( SyncFlags.FromHost )] public int StatIntelligence { get; private set; }

	public int TameExperienceToNextLevel => ThornsTameProgression.ExperienceForNextLevel( TameLevel );

	internal void HostRestoreTameProgression(
		int level,
		int experience,
		int unspentStatPoints,
		int strength,
		int defense,
		int stamina,
		int agility,
		int intelligence )
	{
		TameLevel = Math.Max( 1, level );
		TameExperience = Math.Max( 0, experience );
		UnspentStatPoints = Math.Max( 0, unspentStatPoints );
		StatStrength = Math.Max( 0, strength );
		StatDefense = Math.Max( 0, defense );
		StatStamina = Math.Max( 0, stamina );
		StatAgility = Math.Max( 0, agility );
		StatIntelligence = Math.Max( 0, intelligence );
		ClampExperienceToLevelCap();
	}

	internal void HostGrantTameExperience( int amount )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || !IsTamed || IsDead || amount <= 0 )
			return;

		TameExperience += amount;
		var leveled = false;
		while ( TameExperience >= TameExperienceToNextLevel )
		{
			TameExperience -= TameExperienceToNextLevel;
			TameLevel++;
			UnspentStatPoints += ThornsTameProgression.StatPointsPerLevel;
			leveled = true;
		}

		if ( leveled )
			LogAi( $"Tame leveled up to {TameLevel} ({UnspentStatPoints} stat points available)" );
	}

	internal bool HostTryUpgradeStat( ThornsTameStat stat )
	{
		if ( !ThornsMultiplayer.IsHostOrOffline || !IsTamed || IsDead || UnspentStatPoints <= 0 )
			return false;

		switch ( stat )
		{
			case ThornsTameStat.Strength:
				StatStrength++;
				_spawnDamage += ThornsTameProgression.StrengthDamagePerRank;
				break;
			case ThornsTameStat.Defense:
				StatDefense++;
				ApplyMaxHealthBonus( ThornsTameProgression.DefenseHealthPerRank );
				break;
			case ThornsTameStat.Stamina:
				StatStamina++;
				ApplyMaxHealthBonus( ThornsTameProgression.StaminaHealthPerRank );
				break;
			case ThornsTameStat.Agility:
				StatAgility++;
				_spawnSpeed += ThornsTameProgression.AgilitySpeedPerRank;
				SyncAgentMoveSpeed();
				break;
			case ThornsTameStat.Intelligence:
				StatIntelligence++;
				_breedDetectionRange = Math.Max( 0f, _breedDetectionRange ) + ThornsTameProgression.IntelligenceDetectionPerRank;
				break;
			default:
				return false;
		}

		UnspentStatPoints--;
		LogAi( $"Upgraded {ThornsTameProgression.StatLabel( stat )} on {TamedDisplayName}" );
		return true;
	}

	void ApplyMaxHealthBonus( float amount )
	{
		if ( amount <= 0f )
			return;

		_spawnHealth += amount;
		CurrentHealth = Math.Min( _spawnHealth, CurrentHealth + amount );
	}

	void ClampExperienceToLevelCap()
	{
		var cap = TameExperienceToNextLevel;
		if ( TameExperience >= cap )
			TameExperience = Math.Max( 0, cap - 1 );
	}
}
