namespace PawnShop;

/// <summary>Shop reputation 0-100 with reasons tracked for the daily summary.</summary>
public sealed class ReputationSystem
{
	private readonly SaveData _save;
	private readonly EconomySystem _economy;

	public ReputationSystem( SaveData save, EconomySystem economy )
	{
		_save = save;
		_economy = economy;
	}

	public float Value => _save.Reputation;

	public string Label => _save.Reputation switch
	{
		>= 85f => "Excellent",
		>= 65f => "Good",
		>= 45f => "Decent",
		>= 25f => "Poor",
		_ => "Terrible",
	};

	public string Stars
	{
		get
		{
			var n = Math.Clamp( (int)Math.Round( _save.Reputation / 20f ), 0, 5 );
			return new string( '★', n ) + new string( '☆', 5 - n );
		}
	}

	public void Add( float amount, bool track = true )
	{
		var before = _save.Reputation;
		_save.Reputation = Math.Clamp( _save.Reputation + amount, GameConstants.RepMin, GameConstants.RepMax );
		if ( track )
			_economy.Ledger.RepChange += _save.Reputation - before;

		if ( amount >= 2f ) Sfx.Play( Sfx.RepUp, 0.5f );
	}

	/// <summary>Traffic and customer-quality multiplier from reputation (0.7 .. 1.3).</summary>
	public float TrafficMult => 0.7f + (_save.Reputation / 100f) * 0.6f;

	/// <summary>How much buyers trust sticker prices (higher rep = pay closer to asking).</summary>
	public float BuyerConfidence => 0.85f + (_save.Reputation / 100f) * 0.25f;
}
