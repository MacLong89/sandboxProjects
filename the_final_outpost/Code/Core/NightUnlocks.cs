namespace FinalOutpost;

/// <summary>Night-gated unlocks derived from each asset's combat or utility strength.</summary>
public static class NightUnlocks
{
	private const float TowerOrigin = 30f;
	private const float TowerStep = 3.5f;
	private const float RecruitOrigin = 16f;
	private const float RecruitStep = 4f;
	private const float RangePowerScale = 0.08f;
	private const float MobileBonus = 3.5f;

	/// <summary>Highest night reached this run (prep for night N means progress is at least N).</summary>
	public static int ProgressNight( SaveData save ) =>
		Math.Max( save?.CurrentNight ?? 1, save?.BestNight ?? 0 );

	public static bool IsUnlocked( SaveData save, int requiredNight ) =>
		ProgressNight( save ) >= requiredNight;

	public static float CombatPower( float dps, float range, bool mobile = false )
	{
		var power = dps;
		power += Math.Max( 0f, range - CombatEconomy.ReferenceRange ) * RangePowerScale;
		if ( mobile ) power += MobileBonus;
		return power;
	}

	public static int UnlockFromPower( float power, float origin, float step ) =>
		Math.Clamp( 1 + (int)MathF.Floor( MathF.Max( 0f, power - origin ) / step ), 1, 25 );

	public static int TowerUnlockNight( float dps, float range ) =>
		UnlockFromPower( CombatPower( dps, range ), TowerOrigin, TowerStep );

	public static int RecruitUnlockNight( float dps, float range ) =>
		UnlockFromPower( CombatPower( dps, range, mobile: true ), RecruitOrigin, RecruitStep );

	public static int BuildingUnlockNight( BuildableDef def ) => def.Role switch
	{
		BuildingRole.Defense => TowerUnlockNight( def.Dps(), def.BaseRange ),
		BuildingRole.Wall => 1,
		BuildingRole.Management => TowerUnlockNight( 18f + def.BaseHp * 0.08f, CombatEconomy.ReferenceRange ),
		_ => 1
	};

	public static int RecruitUnlockNight( RecruitWeaponDef def ) =>
		RecruitUnlockNight( def.BaseDps, def.Range );

	public static int WorkerUnlockNight( WorkerRole role ) => role switch
	{
		WorkerRole.Forager => RecruitUnlockNight( 14f, 0f ),
		WorkerRole.Craftsman => RecruitUnlockNight( 22f, 0f ),
		WorkerRole.Repairman => RecruitUnlockNight( 28f, 0f ),
		WorkerRole.Farmer => RecruitUnlockNight( 16f, 0f ),
		WorkerRole.Scholar => RecruitUnlockNight( 20f, 0f ),
		WorkerRole.Operator => RecruitUnlockNight( 24f, 0f ),
		WorkerRole.Medic => RecruitUnlockNight( 26f, 0f ),
		WorkerRole.Merchant => RecruitUnlockNight( 22f, 0f ),
		_ => 1
	};

	/// <summary>Short scout trips — moderate payout, low risk.</summary>
	public static int ShortExpeditionNight => TowerUnlockNight( 34f, CombatEconomy.ReferenceRange );

	/// <summary>Long expeditions — high payout, loss risk.</summary>
	public static int LongExpeditionNight => TowerUnlockNight( 44f, 420f );

	public static bool IsBuildingUnlocked( SaveData save, BuildableId id )
	{
		if ( GameCore.Instance?.IsCure == true )
			return CureUnlocks.IsBuildingUnlocked( save, id );
		return IsUnlocked( save, BuildingUnlockNight( BuildableCatalog.Get( id ) ) );
	}

	public static bool IsRecruitUnlocked( SaveData save, RecruitWeaponType type )
	{
		if ( GameCore.Instance?.IsCure == true )
			return CureUnlocks.IsRecruitUnlocked( save, type );
		return IsUnlocked( save, RecruitUnlockNight( RecruitWeapons.Get( type ) ) );
	}

	public static bool IsWorkerUnlocked( SaveData save, WorkerRole role )
	{
		if ( GameCore.Instance?.IsCure == true )
			return CureUnlocks.IsWorkerUnlocked( save, role );
		return IsUnlocked( save, WorkerUnlockNight( role ) );
	}

	public static bool IsShortExpeditionUnlocked( SaveData save )
	{
		if ( GameCore.Instance?.IsCure == true )
			return CureUnlocks.IsShortExpeditionUnlocked( save );
		return IsUnlocked( save, ShortExpeditionNight );
	}

	public static bool IsLongExpeditionUnlocked( SaveData save )
	{
		if ( GameCore.Instance?.IsCure == true )
			return CureUnlocks.IsLongExpeditionUnlocked( save );
		return IsUnlocked( save, LongExpeditionNight );
	}

	public static string UnlockLabel( SaveData save, int requiredNight )
	{
		if ( GameCore.Instance?.IsCure == true )
			return CureUnlocks.UnlockLabel( requiredNight );
		return $"Night {requiredNight}";
	}

	public static bool IsBuildingUnlockedLegacy( SaveData save, BuildableId id ) =>
		IsUnlocked( save, BuildingUnlockNight( BuildableCatalog.Get( id ) ) );

	/// <summary>Names of items that became available since the previous progress night.</summary>
	public static string DescribeNewUnlocks( SaveData save, int previousProgress, int currentProgress )
	{
		if ( currentProgress <= previousProgress ) return null;

		var names = new List<string>();

		foreach ( var def in BuildableCatalog.All )
		{
			var night = BuildingUnlockNight( def );
			if ( night > previousProgress && night <= currentProgress )
				names.Add( def.Name );
		}

		foreach ( var type in RecruitWeapons.Order )
		{
			var night = RecruitUnlockNight( RecruitWeapons.Get( type ) );
			if ( night > previousProgress && night <= currentProgress )
				names.Add( RecruitWeapons.Get( type ).Name );
		}

		foreach ( var role in WorkerInfo.Order )
		{
			var night = WorkerUnlockNight( role );
			if ( night > previousProgress && night <= currentProgress )
				names.Add( WorkerInfo.Name( role ) );
		}

		if ( ShortExpeditionNight > previousProgress && ShortExpeditionNight <= currentProgress )
			names.Add( "Scout Expeditions" );

		if ( LongExpeditionNight > previousProgress && LongExpeditionNight <= currentProgress )
			names.Add( "Long Expeditions" );

		if ( names.Count == 0 ) return null;
		if ( names.Count == 1 ) return names[0];
		if ( names.Count == 2 ) return $"{names[0]} & {names[1]}";
		return $"{names[0]}, {names[1]} +{names.Count - 2} more";
	}
}
