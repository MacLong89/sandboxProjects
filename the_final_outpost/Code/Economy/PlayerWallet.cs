namespace FinalOutpost;

public sealed class PlayerWallet
{
	private readonly SaveData _save;

	public PlayerWallet( SaveData save ) => _save = save;

	public double Scrap => _save.Scrap;
	public event Action Changed;

	public void Earn( double amount, bool applyIncomeScale = true )
	{
		if ( amount <= 0 ) return;
		if ( applyIncomeScale )
			amount *= GameConstants.ScrapIncomeMult;
		_save.Scrap += amount;
		_save.LifetimeEarned += amount;
		Changed?.Invoke();
	}

	public bool TrySpend( double amount )
	{
		if ( amount < 0 || _save.Scrap < amount ) return false;
		_save.Scrap -= amount;
		Changed?.Invoke();
		return true;
	}
}
