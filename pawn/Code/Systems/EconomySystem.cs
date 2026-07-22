namespace PawnShop;

/// <summary>Cash, debt, daily ledger, expenses, and failure-state bookkeeping.</summary>
public sealed class EconomySystem
{
	private readonly SaveData _save;

	public DayLedger Ledger { get; private set; } = new();

	public EconomySystem( SaveData save )
	{
		_save = save;
		Ledger = new DayLedger { Day = save.Day };
	}

	public int Cash => _save.Cash;
	public int Debt => _save.Debt;

	public bool CanAfford( int amount ) => _save.Cash >= amount;

	public void Spend( int amount )
	{
		_save.Cash -= amount;
	}

	public void Earn( int amount )
	{
		_save.Cash += amount;
	}

	public void ResetLedger( int day )
	{
		Ledger = new DayLedger { Day = day };
	}

	public void RecordDeal( string itemName, int profit )
	{
		if ( profit >= Ledger.BestDealProfit || string.IsNullOrEmpty( Ledger.BestDeal ) )
		{
			Ledger.BestDeal = itemName;
			Ledger.BestDealProfit = profit;
		}

		if ( profit <= Ledger.WorstDealProfit || string.IsNullOrEmpty( Ledger.WorstDeal ) )
		{
			Ledger.WorstDeal = itemName;
			Ledger.WorstDealProfit = profit;
		}
	}

	/// <summary>Total value of all player-owned stock at true market value.</summary>
	public int InventoryValue( GameManager game ) =>
		_save.Inventory.Where( i => i.Location is ItemLocation.Backroom or ItemLocation.OnDisplay or ItemLocation.RepairBench )
			.Sum( i => ItemValue.TrueValue( i, game ) );

	/// <summary>Cash tied up in outstanding pawn loans.</summary>
	public int PawnedCapital => _save.PawnContracts.Sum( c => c.Principal );

	/// <summary>Today's fixed operating costs. Grow with shop size.</summary>
	public int DailyExpenses()
	{
		var expenses = GameConstants.BaseRent + GameConstants.BaseUtilities + GameConstants.InsuranceCost;
		expenses += _save.Upgrades.Count * 8;           // bigger shop, bigger bills
		expenses += Math.Max( 0, _save.Day - 5 ) * 3;   // slow difficulty creep
		if ( _save.Debt > 0 )
			expenses += (int)Math.Ceiling( _save.Debt * GameConstants.EmergencyLoanDailyInterest );
		return expenses;
	}

	/// <summary>
	/// Close out the day's books. Returns true if the shop survives; false = bankruptcy.
	/// </summary>
	public bool ProcessDayEnd()
	{
		var expenses = DailyExpenses();
		Ledger.Expenses += expenses;
		_save.Cash -= expenses;

		if ( _save.Cash < 0 )
		{
			// Emergency loan automatically covers the hole, at a price.
			var needed = -_save.Cash + GameConstants.EmergencyLoanAmount / 4;
			_save.Debt += needed;
			_save.Cash += needed;
			_save.DaysInTrouble++;
			_save.Reputation = Math.Max( GameConstants.RepMin, _save.Reputation - 4f );
		}
		else if ( _save.Cash > 200 && _save.Debt > 0 )
		{
			// Auto-repay debt from healthy cash.
			var payment = Math.Min( _save.Debt, (_save.Cash - 200) / 2 );
			if ( payment > 0 )
			{
				_save.Debt -= payment;
				_save.Cash -= payment;
				Ledger.Expenses += payment;
			}
			_save.DaysInTrouble = 0;
		}
		else
		{
			_save.DaysInTrouble = 0;
		}

		_save.LifetimeProfit += Ledger.Profit;

		// Bankruptcy: crushing debt or too many consecutive underwater days.
		if ( _save.Debt >= GameConstants.BankruptcyDebtLimit )
			return false;
		if ( _save.DaysInTrouble >= GameConstants.BankruptcyGraceDays )
			return false;

		return true;
	}
}
