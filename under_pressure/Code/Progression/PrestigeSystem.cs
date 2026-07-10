namespace UnderPressure;

/// <summary>
/// The long-tail hook: reset spendable progress for a permanent, stacking earnings
/// multiplier. This is what turns a weekend toy into a months-long grind.
/// </summary>
public sealed class PrestigeSystem
{
	private readonly SaveData _save;

	public PrestigeSystem( SaveData save ) => _save = save;

	public event Action Changed;

	public int Level => _save.PrestigeLevel;

	/// <summary>Global earnings multiplier granted by prestige levels.</summary>
	public double Multiplier => 1.0 + _save.PrestigeLevel * GameConstants.PrestigeMultiplierPerLevel;

	/// <summary>Run earnings required to unlock the next prestige.</summary>
	public double Requirement => GameConstants.PrestigeBaseRequirement
		* Math.Pow( GameConstants.PrestigeRequirementGrowth, _save.PrestigeLevel );

	public bool CanPrestige( PlayerWallet wallet ) => wallet.RunEarned >= Requirement;

	public float Progress( PlayerWallet wallet ) =>
		(float)Math.Clamp( wallet.RunEarned / Requirement, 0.0, 1.0 );

	/// <summary>Next multiplier the player would have after prestiging.</summary>
	public double NextMultiplier => 1.0 + (_save.PrestigeLevel + 1) * GameConstants.PrestigeMultiplierPerLevel;

	public bool TryPrestige( PlayerWallet wallet, JobSiteManager jobs )
	{
		if ( !CanPrestige( wallet ) ) return false;

		_save.PrestigeLevel++;
		_save.Upgrades.Clear();
		_save.JobIndex = 0;
		wallet.ResetForPrestige();
		jobs?.LoadJob( 0 );
		Changed?.Invoke();
		return true;
	}
}
