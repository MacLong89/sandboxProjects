namespace RunGun;

/// <summary>Owns the player's persistent cash and tracks lifetime earnings.</summary>
public sealed class PlayerWallet
{
	private readonly SaveData _save;

	public PlayerWallet( SaveData save ) => _save = save;

	public double Cash => _save.Cash;
	public double LifetimeEarned => _save.LifetimeEarned;

	public event Action Changed;

	public void Earn( double amount )
	{
		if ( amount <= 0 ) return;
		_save.Cash += amount;
		_save.LifetimeEarned += amount;
		Changed?.Invoke();
	}

	public bool TrySpend( double amount )
	{
		if ( amount < 0 || _save.Cash < amount ) return false;
		_save.Cash -= amount;
		Changed?.Invoke();
		return true;
	}
}
