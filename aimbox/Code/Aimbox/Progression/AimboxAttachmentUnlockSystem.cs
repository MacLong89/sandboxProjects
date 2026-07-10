namespace Sandbox;

public sealed class AimboxAttachmentUnlockSystem
{
	public List<AimboxUnlock> EvaluateMasteryUnlocks( AimboxPlayerData data, AimboxWeaponId weaponId )
	{
		var unlocks = new List<AimboxUnlock>();
		var weaponData = data.GetWeapon( weaponId );
		var def = AimboxWeapons.Get( weaponId );

		foreach ( var challenge in AimboxMw2Catalog.GetChallengesForWeapon( weaponId ) )
		{
			if ( !AimboxAttachmentCatalog.IsCompatible( weaponId, challenge.Attachment ) )
				continue;

			if ( weaponData.UnlockedAttachments.Contains( challenge.Attachment ) )
				continue;

			if ( weaponData.Level < challenge.RequiredMasteryLevel )
				continue;

			if ( weaponData.UnlockedAttachments.Add( challenge.Attachment ) )
				unlocks.Add( new AimboxUnlock( $"{def.Name}: {AimboxAttachmentCatalog.Label( challenge.Attachment )}" ) );
		}

		return unlocks;
	}

	public float GetAttachmentProgress( AimboxWeaponData weaponData, AimboxAttachmentChallenge challenge ) =>
		AimboxWeaponProgressionSystem.AttachmentUnlockProgress( weaponData, challenge );
}
