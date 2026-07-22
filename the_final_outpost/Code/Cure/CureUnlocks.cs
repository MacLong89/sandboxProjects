namespace FinalOutpost;

/// <summary>
/// Road to a Cure unlocks — tech-gated except starter defenses (walls, gun tower, barracks, pistol).
/// </summary>
public static class CureUnlocks
{
	public static int ProgressSeason( SaveData save ) => CureConstants.ProgressSeason( save );

	public static bool IsStarterBuilding( BuildableId id ) => id is
		BuildableId.WallPiece or BuildableId.GunTower or BuildableId.Barracks;

	public static bool IsBuildingUnlocked( SaveData save, BuildableId id )
	{
		if ( IsStarterBuilding( id ) )
			return true;

		var def = BuildableCatalog.Get( id );
		return TechTreeCatalog.BuildingUnlockedByTech( save, def.Key );
	}

	public static string BuildingUnlockLabel( SaveData save, BuildableId id )
	{
		if ( IsStarterBuilding( id ) )
			return "Unlocked";

		var def = BuildableCatalog.Get( id );
		return TechTreeCatalog.GateLabelForBuilding( save, def.Key );
	}

	public static string RecruitRequiredTech( RecruitWeaponType type ) => type switch
	{
		RecruitWeaponType.Pistol => null,
		RecruitWeaponType.Smg or RecruitWeaponType.AssaultRifle => "tactics",
		RecruitWeaponType.Shotgun or RecruitWeaponType.Sniper => "marksmanship",
		_ => null
	};

	public static string WorkerRequiredTech( WorkerRole role ) => role switch
	{
		WorkerRole.Forager or WorkerRole.Farmer => "agriculture",
		WorkerRole.Craftsman or WorkerRole.Repairman or WorkerRole.Operator => "industry",
		WorkerRole.Scholar => "literacy",
		WorkerRole.Medic => "medicine",
		WorkerRole.Merchant => "commerce",
		_ => null
	};

	public static bool IsRecruitUnlocked( SaveData save, RecruitWeaponType type )
	{
		var tech = RecruitRequiredTech( type );
		if ( tech is null )
			return true;
		return TechTreeCatalog.IsUnlocked( save, tech );
	}

	public static string RecruitUnlockLabel( SaveData save, RecruitWeaponType type )
	{
		var tech = RecruitRequiredTech( type );
		if ( tech is null )
			return "Unlocked";

		var node = TechTreeCatalog.Get( tech );
		return node is not null ? $"Tech: {node.Name}" : "Requires tech";
	}

	public static bool IsWorkerUnlocked( SaveData save, WorkerRole role )
	{
		var tech = WorkerRequiredTech( role );
		if ( tech is null )
			return true;
		return TechTreeCatalog.IsUnlocked( save, tech );
	}

	public static string WorkerUnlockLabel( SaveData save, WorkerRole role )
	{
		var tech = WorkerRequiredTech( role );
		if ( tech is null )
			return "Unlocked";

		var node = TechTreeCatalog.Get( tech );
		return node is not null ? $"Tech: {node.Name}" : "Requires tech";
	}

	public static bool IsShortExpeditionUnlocked( SaveData save ) =>
		TechTreeCatalog.IsUnlocked( save, "tactics" );

	public static bool IsLongExpeditionUnlocked( SaveData save ) =>
		TechTreeCatalog.IsUnlocked( save, "diplomacy" );

	public static string ShortExpeditionUnlockLabel( SaveData save ) =>
		TechLabel( "tactics" );

	public static string LongExpeditionUnlockLabel( SaveData save ) =>
		TechLabel( "diplomacy" );

	static string TechLabel( string techId )
	{
		var node = TechTreeCatalog.Get( techId );
		return node is not null ? $"Tech: {node.Name}" : "Requires tech";
	}
}
