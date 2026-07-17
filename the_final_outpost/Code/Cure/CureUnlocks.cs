namespace FinalOutpost;

/// <summary>Season/year-gated unlocks for Road to a Cure (replaces night gates).</summary>
public static class CureUnlocks
{
	public static int ProgressSeason( SaveData save ) => CureConstants.ProgressSeason( save );

	public static bool IsUnlocked( SaveData save, int requiredSeason ) =>
		ProgressSeason( save ) >= requiredSeason;

	public static bool IsBuildingUnlocked( SaveData save, BuildableId id )
	{
		var def = BuildableCatalog.Get( id );
		if ( def.Role == BuildingRole.Civic )
			return TechTreeCatalog.BuildingUnlockedByTech( save, def.Key );

		if ( id == BuildableId.Lab ) return ProgressSeason( save ) >= 1;
		return IsUnlocked( save, BuildingUnlockSeason( def ) );
	}

	public static int BuildingUnlockSeason( BuildableDef def ) => def.Role switch
	{
		BuildingRole.Defense => TowerUnlockSeason( def.Dps(), def.BaseRange ),
		BuildingRole.Wall => 1,
		BuildingRole.Civic => 99,
		BuildingRole.Management when def.Id == BuildableId.Barracks => 1,
		BuildingRole.Management => 2,
		_ => 1
	};

	public static int TowerUnlockSeason( float dps, float range )
	{
		var power = NightUnlocks.CombatPower( dps, range );
		return Math.Clamp( 1 + (int)MathF.Floor( MathF.Max( 0f, power - 30f ) / 12f ), 1, 12 );
	}

	public static int RecruitUnlockSeason( RecruitWeaponType type ) =>
		TowerUnlockSeason( RecruitWeapons.Get( type ).BaseDps, RecruitWeapons.Get( type ).Range );

	public static int WorkerUnlockSeason( WorkerRole role ) => role switch
	{
		WorkerRole.Forager => 1,
		WorkerRole.Craftsman => 2,
		WorkerRole.Repairman => 3,
		WorkerRole.Farmer => 1,
		WorkerRole.Scholar => 2,
		WorkerRole.Operator => 2,
		WorkerRole.Medic => 3,
		WorkerRole.Merchant => 3,
		_ => 1
	};

	public static string WorkerRequiredTech( WorkerRole role ) => role switch
	{
		WorkerRole.Farmer => "agriculture",
		WorkerRole.Operator => "industry",
		WorkerRole.Scholar => "literacy",
		WorkerRole.Medic => "medicine",
		WorkerRole.Merchant => "commerce",
		_ => null
	};

	public static bool IsRecruitUnlocked( SaveData save, RecruitWeaponType type ) =>
		IsUnlocked( save, RecruitUnlockSeason( type ) );

	/// <summary>AUDIT FIX M3: HUD used Survival "Night N" even in Cure — use season labels here.</summary>
	public static string RecruitUnlockLabel( SaveData save, RecruitWeaponType type ) =>
		UnlockLabel( RecruitUnlockSeason( type ) );

	public static string ShortExpeditionUnlockLabel( SaveData save ) => UnlockLabel( 2 );
	public static string LongExpeditionUnlockLabel( SaveData save ) => UnlockLabel( 4 );

	public static bool IsWorkerUnlocked( SaveData save, WorkerRole role )
	{
		var tech = WorkerRequiredTech( role );
		if ( tech is not null )
			return TechTreeCatalog.IsUnlocked( save, tech );

		return IsUnlocked( save, WorkerUnlockSeason( role ) );
	}

	public static string WorkerUnlockLabel( SaveData save, WorkerRole role )
	{
		var tech = WorkerRequiredTech( role );
		if ( tech is not null )
		{
			var node = TechTreeCatalog.Get( tech );
			return node is not null ? $"Tech: {node.Name}" : "Requires tech";
		}

		return UnlockLabel( WorkerUnlockSeason( role ) );
	}

	public static bool IsShortExpeditionUnlocked( SaveData save ) => ProgressSeason( save ) >= 2;
	public static bool IsLongExpeditionUnlocked( SaveData save ) => ProgressSeason( save ) >= 4;

	public static string UnlockLabel( int season ) => $"Season {season}";
}
