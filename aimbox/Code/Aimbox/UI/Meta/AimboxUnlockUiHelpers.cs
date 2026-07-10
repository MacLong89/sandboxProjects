namespace Sandbox;

public static class AimboxUnlockUiHelpers
{
	public static string WeaponOptionLabel( AimboxPlayerData data, AimboxWeaponId id )
	{
		var name = AimboxWeapons.Get( id ).Name;
		if ( AimboxUnlockService.IsWeaponUnlocked( data, id ) )
			return name;

		return $"LOCKED — {name} (Rank {AimboxWeapons.Get( id ).UnlockLevel})";
	}

	public static string PerkOptionLabel( AimboxPlayerData data, AimboxPerkDefinition perk )
	{
		if ( AimboxUnlockService.IsPerkUnlocked( data, perk.Id ) )
			return perk.Name;

		return $"LOCKED — {perk.Name} (Rank {perk.UnlockLevel})";
	}

	public static string KillstreakOptionLabel( AimboxPlayerData data, AimboxKillstreakDefinition streak )
	{
		if ( AimboxUnlockService.IsKillstreakUnlocked( data, streak.Id ) )
			return streak.Name;

		return $"LOCKED — {streak.Name} (Rank {streak.UnlockLevel})";
	}

	public static string AttachmentOptionLabel( AimboxPlayerData data, AimboxWeaponId weapon, AimboxAttachmentId attachment )
	{
		var weaponData = data.GetWeapon( weapon );
		if ( AimboxUnlockService.IsAttachmentUnlocked( weapon, weaponData, attachment ) )
			return AimboxAttachmentCatalog.Label( attachment );

		AimboxAttachmentChallenge challenge = null;
		foreach ( var candidate in AimboxMw2Catalog.GetChallengesForWeapon( weapon ) )
		{
			if ( candidate.Attachment == attachment )
			{
				challenge = candidate;
				break;
			}
		}
		return challenge is null
			? $"LOCKED — {AimboxAttachmentCatalog.Label( attachment )}"
			: $"LOCKED — {AimboxAttachmentCatalog.Label( attachment )} ({AimboxWeaponProgressionSystem.UnlockRequirementText( challenge )})";
	}

	public static string ArmoryWeaponLine( AimboxPlayerData data, AimboxWeaponId id )
	{
		var def = AimboxWeapons.Get( id );
		return AimboxUnlockService.IsWeaponUnlocked( data, id )
			? $"{def.Name} — UNLOCKED"
			: $"LOCKED — {def.Name} (Rank {def.UnlockLevel})";
	}
}
