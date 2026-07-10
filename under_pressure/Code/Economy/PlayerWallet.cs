namespace UnderPressure;

/// <summary>Owns the player's cash and tracks run/lifetime earnings for prestige gating.</summary>
public sealed class PlayerWallet
{
	private readonly SaveData _save;

	public PlayerWallet( SaveData save ) => _save = save;

	public double Cash => _save.Cash;
	public double RunEarned => _save.RunEarned;
	public double LifetimeEarned => _save.LifetimeEarned;

	/// <summary>Fires whenever the balance changes so UI/juice can react.</summary>
	public event Action Changed;

	public void Earn( double amount )
	{
		if ( amount <= 0 ) return;
		_save.Cash += amount;
		_save.RunEarned += amount;
		_save.LifetimeEarned += amount;
		LeaderboardService.SubmitEarned( amount );
		Changed?.Invoke();
	}

	public bool TrySpend( double amount )
	{
		if ( amount < 0 || _save.Cash < amount ) return false;
		_save.Cash -= amount;
		Changed?.Invoke();
		return true;
	}

	/// <summary>Take cash from the player (robbery). Does not reduce lifetime/run earnings.</summary>
	public double Steal( double amount )
	{
		if ( amount <= 0 || _save.Cash <= 0 ) return 0;
		var taken = Math.Min( _save.Cash, amount );
		_save.Cash -= taken;
		Changed?.Invoke();
		return taken;
	}

	/// <summary>Wipe spendable + run progress on prestige while keeping lifetime totals.</summary>
	public void ResetForPrestige()
	{
		_save.Cash = 0;
		_save.RunEarned = 0;
		Changed?.Invoke();
	}
}
