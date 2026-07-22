namespace FinalOutpost;

/// <summary>Survival night-gated unlocks. Fixed tables — not derived from scaled range/DPS.</summary>
public static class NightUnlocks
{
	/// <summary>Highest night reached this run (prep for night N means progress is at least N).</summary>
	public static int ProgressNight( SaveData save ) =>
		Math.Max( save?.CurrentNight ?? 1, save?.BestNight ?? 0 );

	public static bool IsUnlocked( SaveData save, int requiredNight ) =>
		ProgressNight( save ) >= requiredNight;

	/// <summary>Legacy helper for Cure season curves — design-unit range only.</summary>
	public static float CombatPower( float dps, float range, bool mobile = false )
	{
		const float rangePowerScale = 0.08f;
		const float mobileBonus = 3.5f;
		var scale = MathF.Max( 0.01f, GameConstants.RangeScale );
		var designRange = range / scale;
		var designRef = CombatEconomy.ReferenceRangeDesign;
		var power = dps + Math.Max( 0f, designRange - designRef ) * rangePowerScale;
		if ( mobile ) power += mobileBonus;
		return power;
	}

	public static int BuildingUnlockNight( BuildableDef def ) => def.Id switch
	{
		BuildableId.WallPiece => 1,
		BuildableId.GunTower => 1,
		BuildableId.CannonTower => 1,
		BuildableId.LongRangeTower => 6,
		BuildableId.AmmoDepot => 8,
		// N10+ cadence: one unlock every 2 nights (supports + late towers).
		BuildableId.Spotlight => 10,
		BuildableId.Hardpoint => 12,
		BuildableId.RadioMast => 14,
		BuildableId.Minefield => 16,
		BuildableId.OilSlick => 18,
		BuildableId.Artillery => 20,
		BuildableId.Barracks => 2,
		BuildableId.Lab => 1,
		_ => 1
	};

	public static int RecruitUnlockNight( RecruitWeaponDef def ) => def.Type switch
	{
		RecruitWeaponType.Pistol => 1,
		RecruitWeaponType.Smg => 3,
		RecruitWeaponType.AssaultRifle => 3,
		RecruitWeaponType.Shotgun => 4,
		RecruitWeaponType.Sniper => 6,
		_ => 1
	};

	public static int WorkerUnlockNight( WorkerRole role ) => role switch
	{
		WorkerRole.Forager => 1,
		WorkerRole.Craftsman => 99, // removed from Survival scrap-only economy
		WorkerRole.Repairman => 2,
		WorkerRole.Farmer => 1,
		WorkerRole.Scholar => 2,
		WorkerRole.Operator => 2,
		WorkerRole.Medic => 3,
		WorkerRole.Merchant => 3,
		_ => 1
	};

	/// <summary>Short scout trips — moderate payout, low risk.</summary>
	public static int ShortExpeditionNight => 3;

	/// <summary>Long expeditions — high payout, loss risk.</summary>
	public static int LongExpeditionNight => 6;

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

	public static string UnlockLabel( SaveData save, int requiredNight ) =>
		$"Night {requiredNight}";

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
