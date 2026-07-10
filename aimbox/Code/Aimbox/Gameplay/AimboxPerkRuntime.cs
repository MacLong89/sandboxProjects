namespace Sandbox;

public sealed class AimboxPerkRuntime
{
	public float DamageMultiplier { get; private set; } = 1f;
	public float ReloadMultiplier { get; private set; } = 1f;
	public float PresentationSpeedMultiplier { get; private set; } = 1f;
	public float MoveSpeedMultiplier { get; private set; } = 1f;
	public float MovementNoiseMultiplier { get; private set; } = 1f;
	public bool RefillAmmoOnKill { get; private set; }
	public bool UnlimitedSprint { get; private set; }
	public bool QuietMovement => MovementNoiseMultiplier < 1f;

	public void ApplyFromLoadout( AimboxLoadoutData loadout, AimboxPlayerData data )
	{
		DamageMultiplier = 1f;
		ReloadMultiplier = 1f;
		PresentationSpeedMultiplier = 1f;
		MoveSpeedMultiplier = 1f;
		MovementNoiseMultiplier = 1f;
		RefillAmmoOnKill = false;
		UnlimitedSprint = false;

		ApplyPerk( loadout.Perk1, data );
		ApplyPerk( loadout.Perk2, data );
		ApplyPerk( loadout.Perk3, data );
	}

	void ApplyPerk( AimboxPerkId perk, AimboxPlayerData data )
	{
		if ( perk == AimboxPerkId.None || !AimboxUnlockService.IsPerkUnlocked( data, perk ) )
			return;

		switch ( perk )
		{
			case AimboxPerkId.StoppingPower:
				DamageMultiplier *= 1.25f;
				break;
			case AimboxPerkId.SleightOfHand:
				ReloadMultiplier *= 0.6f;
				PresentationSpeedMultiplier *= 0.6f;
				break;
			case AimboxPerkId.Scavenger:
				RefillAmmoOnKill = true;
				break;
			case AimboxPerkId.Lightweight:
				MoveSpeedMultiplier *= 1.07f;
				break;
			case AimboxPerkId.Marathon:
				UnlimitedSprint = true;
				break;
			case AimboxPerkId.Ninja:
				MovementNoiseMultiplier *= 0.25f;
				break;
		}
	}
}
