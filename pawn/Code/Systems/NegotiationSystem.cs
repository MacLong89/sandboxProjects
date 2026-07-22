namespace PawnShop;

public enum NegotiationKind { Sell, Pawn, BuyerOffer, Redeem }
public enum NegotiationResult { None, DealDone, Rejected, CustomerLeft }

/// <summary>
/// The active haggle at the counter. One at a time. Owns all hidden customer numbers
/// and finalizes money/inventory/reputation when a deal lands.
/// </summary>
public sealed class NegotiationSystem
{
	private GameManager Game => GameManager.Instance;

	public CustomerProfile Customer { get; private set; }
	public NegotiationKind Kind { get; private set; }
	public ItemInstance Item { get; private set; }

	public bool Active { get; private set; }
	public NegotiationResult Result { get; private set; }
	public int FinalPrice { get; private set; }

	/// <summary>Latest thing the customer said.</summary>
	public string CustomerLine { get; private set; } = "";
	/// <summary>Seller's current ask / buyer's current offer.</summary>
	public int CurrentAsk { get; private set; }
	public int LastPlayerOffer { get; private set; } = -1;
	public int Rounds { get; private set; }

	// Pawn-specific
	public int RequestedLoan { get; private set; }
	public string RiskEstimate { get; private set; } = "";

	// Defects the player has already used as leverage.
	public readonly List<string> CitedDefects = new();
	public bool StoryQuestioned { get; private set; }

	/// <summary>Fired when the session ends, so the customer actor can react.</summary>
	public Action<NegotiationResult> OnFinished;

	// Hidden state
	private float _apparentValue;
	private int _minAccept;
	private int _idealPrice;
	private float _maxFee;

	// ==================================================================== Session start

	public void StartSell( CustomerProfile customer )
	{
		Begin( customer, NegotiationKind.Sell, customer.Item );

		_apparentValue = ApparentValue( Item );
		var knowledgeNoise = KnowledgeNoise( customer );
		CurrentAsk = RoundMoney( _apparentValue * customer.Archetype.AskMult * knowledgeNoise );
		_idealPrice = RoundMoney( _apparentValue * (0.85f + customer.Archetype.Knowledge * 0.25f) * knowledgeNoise );
		_minAccept = RoundMoney( _apparentValue * customer.Archetype.FloorMult * (1f - customer.Archetype.Desperation * 0.25f) );
		_minAccept = Math.Min( _minAccept, CurrentAsk );
		_idealPrice = Math.Clamp( _idealPrice, _minAccept, CurrentAsk );

		customer.AskPrice = CurrentAsk;
		CustomerLine = Dialogue.SellOpen( customer, Item );
	}

	public void StartPawn( CustomerProfile customer )
	{
		Begin( customer, NegotiationKind.Pawn, customer.Item );

		_apparentValue = ApparentValue( Item );
		RequestedLoan = RoundMoney( _apparentValue * Sandbox.Game.Random.Float( 0.45f, 0.7f ) );
		RequestedLoan = Math.Max( 20, RequestedLoan );
		CurrentAsk = RequestedLoan;
		_minAccept = RoundMoney( RequestedLoan * (0.75f - customer.Archetype.Desperation * 0.3f) );
		_maxFee = GameConstants.PawnFeeMin + 0.12f + customer.Archetype.Desperation * 0.18f;

		var likelihood = Game.Pawns.ComputeRepayLikelihood( customer, Item, RequestedLoan, Game );
		RiskEstimate = PawnSystem.RiskLabel( likelihood );

		customer.AskPrice = RequestedLoan;
		CustomerLine = Dialogue.PawnOpen( customer, Item, RequestedLoan );
	}

	public void StartBuyerOffer( CustomerProfile customer, ItemInstance target )
	{
		Begin( customer, NegotiationKind.BuyerOffer, target );

		var perceived = ItemValue.TrueValue( target, Game ) * KnowledgeNoise( customer );
		var willing = Math.Min( customer.Budget, (int)(perceived * 1.1f) );
		CurrentAsk = RoundMoney( Math.Min( willing * 0.8f, target.SalePrice * Sandbox.Game.Random.Float( 0.65f, 0.85f ) ) );
		CurrentAsk = Math.Max( 5, CurrentAsk );
		_idealPrice = willing; // most they'll ever pay
		customer.BuyerOffer = CurrentAsk;

		CustomerLine = Dialogue.BuyerOpen( customer, target, CurrentAsk );
	}

	public void StartRedeem( CustomerProfile customer )
	{
		Begin( customer, NegotiationKind.Redeem, Game.Inventory.Get( customer.Contract?.ItemId ?? -1 ) );

		// Most redeemers have the money; some come to beg for more time.
		RedeemerCanPay = customer.Contract is not null && Sandbox.Game.Random.Float() < 0.8f;
		CustomerLine = customer.Contract is null
			? "I... I was sure I had a ticket here somewhere."
			: RedeemerCanPay
				? Dialogue.RedeemOpen( customer, customer.Contract )
				: Dialogue.ExtensionAsk( customer, customer.Contract );
	}

	private void Begin( CustomerProfile customer, NegotiationKind kind, ItemInstance item )
	{
		Customer = customer;
		Kind = kind;
		Item = item;
		Active = true;
		Result = NegotiationResult.None;
		Rounds = 0;
		LastPlayerOffer = -1;
		CitedDefects.Clear();
		StoryQuestioned = false;
		FinalPrice = 0;

		customer.Patience = 45f + customer.Archetype.Patience * 75f;
		if ( Game.Save.OwnsUpgrade( UpgradeId.BetterCounter ) )
			customer.Patience *= 1.25f;
		customer.PatienceMax = customer.Patience;
		customer.Mood = 0.55f + (customer.Relationship?.Trust ?? 0.5f) * 0.3f;

		UiState.Bump();
	}

	// ==================================================================== Ticking

	public void Tick( float dt )
	{
		if ( !Active || Customer is null ) return;

		Customer.Patience -= dt;
		if ( Customer.Patience <= 0f )
		{
			CustomerLine = Dialogue.OutOfPatience( Customer );
			if ( Customer.Mood < 0.35f )
				Game.Reputation.Add( -2f );
			Finish( NegotiationResult.CustomerLeft );
		}
	}

	// ==================================================================== Player actions — SELL / common

	/// <summary>Player offers to buy the customer's item for this amount.</summary>
	public void PlayerOffer( int amount )
	{
		if ( !Active || Kind is not (NegotiationKind.Sell) ) return;
		if ( Game.Inventory.StorageFull )
		{
			CustomerLine = "Looks like your backroom is bursting. Come find me when you've got space.";
			Game.Toast( "Backroom is full — sell or scrap something first.", "inventory" );
			UiState.Bump();
			return;
		}
		amount = Math.Clamp( amount, 1, Game.Economy.Cash );
		LastPlayerOffer = amount;
		Rounds++;
		Customer.Patience -= 4f;

		// Insulting lowball?
		if ( amount < _apparentValue * 0.3f && amount < _minAccept )
		{
			Customer.Mood -= 0.22f;
			Customer.Patience -= 10f;
			if ( Customer.IsNamed )
				Game.Relationships.RecordDeal( Customer.Id, Customer.Name, 0, fair: false, lowball: true );

			if ( Customer.Mood <= 0.15f )
			{
				CustomerLine = Dialogue.StormOut( Customer );
				Game.Reputation.Add( -2.5f );
				Finish( NegotiationResult.CustomerLeft );
				return;
			}

			CustomerLine = Dialogue.Insulted( Customer );
			UiState.Bump();
			return;
		}

		if ( amount >= _minAccept )
		{
			// Close enough to ideal, or they're worn down → accept.
			var closeness = _idealPrice > _minAccept
				? (float)(amount - _minAccept) / (_idealPrice - _minAccept)
				: 1f;
			var acceptChance = 0.35f + closeness * 0.65f + Rounds * 0.12f + Customer.Archetype.Desperation * 0.2f;

			if ( amount >= _idealPrice || Sandbox.Game.Random.Float() < acceptChance )
			{
				CompletePurchase( amount );
				return;
			}
		}

		// Counter: drift ask down toward the floor; the floor also softens with desperation.
		var pull = 0.3f + Customer.Archetype.Desperation * 0.25f;
		CurrentAsk = RoundMoney( Math.Max( _minAccept, CurrentAsk - (CurrentAsk - Math.Max( amount, _minAccept )) * pull ) );
		_minAccept = RoundMoney( _minAccept * (1f - 0.04f - Customer.Archetype.Desperation * 0.05f) );
		Customer.Mood -= 0.05f;

		CustomerLine = Rounds >= 3 && CurrentAsk <= _minAccept * 1.1f
			? Dialogue.FinalOffer( Customer, CurrentAsk )
			: Dialogue.Counter( Customer, CurrentAsk );
		UiState.Bump();
	}

	/// <summary>Accept the customer's current ask outright.</summary>
	public void AcceptAsk()
	{
		if ( !Active || Kind != NegotiationKind.Sell ) return;
		if ( Game.Inventory.StorageFull )
		{
			CustomerLine = "Looks like your backroom is bursting. Come find me when you've got space.";
			Game.Toast( "Backroom is full — sell or scrap something first.", "inventory" );
			UiState.Bump();
			return;
		}
		if ( Game.Economy.Cash < CurrentAsk )
		{
			CustomerLine = "You don't have the cash for that, friend.";
			UiState.Bump();
			return;
		}
		CompletePurchase( CurrentAsk );
	}

	/// <summary>Use a discovered defect as negotiation leverage.</summary>
	public void CiteFlaw( string defectId )
	{
		if ( !Active || Item is null ) return;
		if ( CitedDefects.Contains( defectId ) ) return;
		if ( !Item.DiscoveredDefects.Contains( defectId ) ) return;

		var defect = DefectCatalog.Get( defectId );
		if ( defect is null || defect.IsPositive ) return;

		CitedDefects.Add( defectId );
		Customer.Patience -= 5f;

		if ( defect.CounterfeitSign )
		{
			// Caught red-handed (or an innocent seller is mortified).
			if ( Customer.Archetype.Honesty < 0.3f && Sandbox.Game.Random.Float() < 0.55f )
			{
				CustomerLine = Dialogue.ScammerBolts( Customer );
				Game.Save.FakesCaught++;
				Game.Goals.Notify( GoalMetric.FakesCaught );
				Game.Reputation.Add( 1.5f );
				Game.Toast( "You caught a fake before it cost you.", "verified" );
				Finish( NegotiationResult.CustomerLeft );
				return;
			}

			_minAccept = RoundMoney( Math.Max( 5, _apparentValue * 0.06f ) );
			CurrentAsk = RoundMoney( Math.Max( _minAccept, _apparentValue * 0.12f ) );
			Game.Save.FakesCaught++;
			Game.Goals.Notify( GoalMetric.FakesCaught );
			CustomerLine = Dialogue.FakeExposed( Customer );
			UiState.Bump();
			return;
		}

		if ( defect.StolenSign )
		{
			QuestionStoryInternal( fromCite: true );
			return;
		}

		// Regular flaw: knock the customer's numbers down.
		var factor = 1f - Math.Clamp( defect.ValuePenalty, 0f, 0.5f ) * 0.9f;
		CurrentAsk = RoundMoney( Math.Max( 5, CurrentAsk * factor ) );
		_minAccept = RoundMoney( Math.Max( 5, _minAccept * factor ) );
		_idealPrice = RoundMoney( Math.Max( _minAccept, _idealPrice * factor ) );
		Customer.Mood -= 0.06f;

		CustomerLine = Dialogue.FlawAcknowledged( Customer, defect );
		UiState.Bump();
	}

	/// <summary>Press the customer about where the item came from.</summary>
	public void QuestionStory() => QuestionStoryInternal( fromCite: false );

	private void QuestionStoryInternal( bool fromCite )
	{
		if ( !Active || StoryQuestioned ) return;
		StoryQuestioned = true;
		Customer.Patience -= 8f;

		var shady = Item?.LegalStatus != LegalStatus.Clean;
		if ( shady && Customer.Archetype.Honesty < 0.4f )
		{
			if ( Sandbox.Game.Random.Float() < 0.6f )
			{
				CustomerLine = Dialogue.ShadyBolts( Customer );
				Game.Reputation.Add( 1f );
				Game.Toast( "That one couldn't leave fast enough. Probably for the best.", "policy" );
				Finish( NegotiationResult.CustomerLeft );
				return;
			}

			_minAccept = RoundMoney( _minAccept * 0.6f );
			CurrentAsk = RoundMoney( Math.Max( _minAccept, CurrentAsk * 0.7f ) );
			CustomerLine = Dialogue.ShadyDeflects( Customer );
		}
		else
		{
			Customer.Mood -= shady ? 0.05f : 0.15f;
			CustomerLine = Dialogue.StoryAnswer( Customer, shady );
		}
		UiState.Bump();
	}

	/// <summary>Walk away from the deal.</summary>
	public void Reject()
	{
		if ( !Active ) return;

		switch ( Kind )
		{
			case NegotiationKind.Redeem:
				RefuseExtension();
				return;
			default:
				CustomerLine = Dialogue.PlayerRejects( Customer );
				Customer.Mood -= 0.1f;
				Finish( NegotiationResult.Rejected );
				return;
		}
	}

	private void CompletePurchase( int price )
	{
		FinalPrice = price;
		var trueValue = ItemValue.TrueValue( Item, Game );

		Game.Economy.Spend( price );
		Game.Economy.Ledger.Purchases++;
		Game.Economy.Ledger.PurchaseSpend += price;
		Game.Inventory.Acquire( Item, price, Game.Save.Day );
		Game.Save.TotalDeals++;
		Game.Goals.Notify( GoalMetric.ItemsBought );
		Game.Goals.Notify( GoalMetric.DealsClosed );

		if ( Item.TrueAuthenticity != Authenticity.Genuine && !Item.AuthenticityKnown )
			Game.Save.FakesBought++;

		// Fair-deal social effects.
		var fair = price >= _apparentValue * 0.55f && price <= _apparentValue * 1.15f;
		var overpaid = price > trueValue * 1.15f;
		if ( fair ) Game.Reputation.Add( 0.8f );
		if ( price >= _idealPrice ) Customer.Mood = Math.Min( 1f, Customer.Mood + 0.3f );
		if ( Customer.IsNamed )
			Game.Relationships.RecordDeal( Customer.Id, Customer.Name, price, fair, lowball: false );

		Game.Economy.RecordDeal( Item.Name, trueValue - price );
		CustomerLine = Dialogue.DealClosed( Customer );
		Sfx.Play( Sfx.DealAccepted, 0.8f );
		Sfx.Play( Sfx.CashRegister, 0.7f );
		Game.Toast( overpaid ? $"Bought {Item.Name} for {GameConstants.FormatCash( price )}. Hope it's worth it." : $"Bought {Item.Name} for {GameConstants.FormatCash( price )}.", "shopping_bag" );
		Game.Tutorial.Notify( TutorialTrigger.BoughtItem );
		Finish( NegotiationResult.DealDone );
	}

	// ==================================================================== PAWN actions

	/// <summary>Player proposes a loan of this amount at this fee.</summary>
	public void OfferLoan( int amount, float fee )
	{
		if ( !Active || Kind != NegotiationKind.Pawn ) return;
		amount = Math.Clamp( amount, 1, Game.Economy.Cash );
		fee = Math.Clamp( fee, GameConstants.PawnFeeMin, GameConstants.PawnFeeMax );
		LastPlayerOffer = amount;
		Rounds++;
		Customer.Patience -= 4f;

		var feeOk = fee <= _maxFee + 0.001f;
		var amountOk = amount >= _minAccept;

		if ( amountOk && feeOk )
		{
			var contract = Game.Pawns.Issue( Customer, Item, amount, fee, Game );
			FinalPrice = amount;
			Game.Save.TotalDeals++;
			Game.Goals.Notify( GoalMetric.PawnsIssued );
			Game.Goals.Notify( GoalMetric.DealsClosed );
			if ( Customer.IsNamed )
				Game.Relationships.RecordDeal( Customer.Id, Customer.Name, amount, fair: true, lowball: false );

			CustomerLine = Dialogue.PawnClosed( Customer, contract );
			Sfx.Play( Sfx.DealAccepted, 0.8f );
			Sfx.Play( Sfx.CashRegister, 0.7f );
			Game.Toast( $"Pawn issued: {GameConstants.FormatCash( amount )} on {Item.Name}, due day {contract.DueDay}.", "handshake" );
			Finish( NegotiationResult.DealDone );
			return;
		}

		if ( !feeOk && amountOk )
		{
			// Fee too greedy — one warning, then they soften or leave.
			Customer.Mood -= 0.12f;
			if ( Rounds >= 3 )
			{
				CustomerLine = Dialogue.StormOut( Customer );
				Finish( NegotiationResult.CustomerLeft );
				return;
			}
			CustomerLine = Dialogue.FeeTooHigh( Customer );
			UiState.Bump();
			return;
		}

		// Loan too small.
		Customer.Mood -= 0.08f;
		_minAccept = RoundMoney( _minAccept * (1f - 0.05f - Customer.Archetype.Desperation * 0.06f) );
		if ( Rounds >= 4 || Customer.Mood <= 0.15f )
		{
			CustomerLine = Dialogue.StormOut( Customer );
			Finish( NegotiationResult.CustomerLeft );
			return;
		}

		CustomerLine = Dialogue.LoanTooSmall( Customer, RequestedLoan );
		UiState.Bump();
	}

	// ==================================================================== BUYER actions

	/// <summary>Accept the buyer's current offer on a displayed item.</summary>
	public void AcceptBuyerOffer()
	{
		if ( !Active || Kind != NegotiationKind.BuyerOffer ) return;
		CompleteBuyerSale( CurrentAsk );
	}

	/// <summary>Counter the buyer with a higher number.</summary>
	public void CounterBuyer( int amount )
	{
		if ( !Active || Kind != NegotiationKind.BuyerOffer ) return;
		amount = Math.Max( amount, 1 );
		LastPlayerOffer = amount;
		Rounds++;
		Customer.Patience -= 4f;

		if ( amount <= CurrentAsk )
		{
			// Selling below their own offer — done deal.
			CompleteBuyerSale( amount );
			return;
		}

		var willing = _idealPrice;
		if ( amount <= willing )
		{
			var closeness = 1f - (float)(amount - CurrentAsk) / Math.Max( 1, willing - CurrentAsk );
			var acceptChance = 0.3f + closeness * 0.5f + Rounds * 0.1f;
			if ( Sandbox.Game.Random.Float() < acceptChance )
			{
				CompleteBuyerSale( amount );
				return;
			}

			// They creep upward.
			CurrentAsk = RoundMoney( Math.Min( willing, CurrentAsk + (amount - CurrentAsk) * Sandbox.Game.Random.Float( 0.35f, 0.6f ) ) );
			Customer.BuyerOffer = CurrentAsk;
			CustomerLine = Dialogue.BuyerRaises( Customer, CurrentAsk );
			UiState.Bump();
			return;
		}

		// Above their ceiling.
		Customer.Mood -= 0.12f;
		if ( Rounds >= 3 || Customer.Mood <= 0.2f )
		{
			CustomerLine = Dialogue.BuyerWalks( Customer );
			Finish( NegotiationResult.CustomerLeft );
			return;
		}

		CustomerLine = Dialogue.BuyerTooRich( Customer );
		UiState.Bump();
	}

	/// <summary>Talk up the item: verified/researched items pull the buyer's ceiling up.</summary>
	public void ExplainValue()
	{
		if ( !Active || Kind != NegotiationKind.BuyerOffer || Item is null ) return;
		Customer.Patience -= 5f;

		var persuasion = 0f;
		if ( Item.Researched ) persuasion += 0.12f;
		if ( ItemValue.InspectionCoverage( Item ) >= 0.99f ) persuasion += 0.08f;
		if ( Item.AuthenticityKnown && Item.TrueAuthenticity == Authenticity.Genuine ) persuasion += 0.06f;
		if ( Item.Cleaned ) persuasion += 0.04f;

		if ( persuasion <= 0.05f )
		{
			CustomerLine = Dialogue.BuyerUnconvinced( Customer );
			Customer.Mood -= 0.05f;
			UiState.Bump();
			return;
		}

		_idealPrice = RoundMoney( Math.Min( Customer.Budget, _idealPrice * (1f + persuasion) ) );
		CurrentAsk = RoundMoney( Math.Min( _idealPrice, CurrentAsk * (1f + persuasion * 0.6f) ) );
		Customer.BuyerOffer = CurrentAsk;
		CustomerLine = Dialogue.BuyerConvinced( Customer, CurrentAsk );
		UiState.Bump();
	}

	private void CompleteBuyerSale( int price )
	{
		FinalPrice = price;
		var profit = price - Item.TotalInvested;

		Game.Economy.Earn( price );
		Game.Economy.Ledger.Sales++;
		Game.Economy.Ledger.SaleRevenue += price;
		Game.Economy.RecordDeal( Item.Name, profit );
		Game.Inventory.MarkSold( Item, price );
		Game.Save.ItemsSold++;
		Game.Save.TotalDeals++;
		Game.Save.BestFlip = Math.Max( Game.Save.BestFlip, profit );
		Game.Goals.Notify( GoalMetric.ItemsSold );
		Game.Goals.Notify( GoalMetric.SaleRevenue, price );
		Game.Goals.Notify( GoalMetric.DealsClosed );
		Game.Collection.RecordFlip( Item, Game );

		if ( Item.TrueAuthenticity != Authenticity.Genuine )
		{
			Game.Reputation.Add( -6f );
			Game.Toast( "Word spreads that you sold a fake. Reputation takes a hit.", "gpp_bad" );
		}
		else if ( profit > 0 )
		{
			Game.Reputation.Add( 1f );
		}

		if ( Customer.IsNamed )
			Game.Relationships.RecordDeal( Customer.Id, Customer.Name, price, fair: true, lowball: false );

		CustomerLine = Dialogue.BuyerHappy( Customer );
		Sfx.Play( Sfx.DealAccepted, 0.8f );
		Sfx.Play( Sfx.CashRegister, 0.8f );
		Game.Toast( $"Sold {Item.Name} for {GameConstants.FormatCash( price )} ({(profit >= 0 ? "+" : "")}{GameConstants.FormatCash( profit )} profit).", "sell" );
		Game.Tutorial.Notify( TutorialTrigger.SoldItem );
		Finish( NegotiationResult.DealDone );
	}

	// ==================================================================== REDEEM actions

	/// <summary>Customer pays loan + fee and takes their item back.</summary>
	public void AcceptRedemption()
	{
		if ( !Active || Kind != NegotiationKind.Redeem || Customer.Contract is null ) return;

		var ok = Game.Pawns.Redeem( Customer.Contract );
		if ( ok )
		{
			CustomerLine = Dialogue.RedeemDone( Customer );
			Sfx.Play( Sfx.CashRegister, 0.8f );
			Game.Goals.Notify( GoalMetric.Redemptions );
			Game.Goals.Notify( GoalMetric.DealsClosed );
			Game.Reputation.Add( 1.5f );
			Game.Toast( $"Pawn redeemed: +{GameConstants.FormatCash( Customer.Contract.RedemptionAmount )}.", "handshake" );
		}
		else
		{
			CustomerLine = "Wait — you LOST it? ...Fine. Forget the whole thing.";
			Game.Reputation.Add( -4f );
		}
		Finish( NegotiationResult.DealDone );
	}

	/// <summary>Grant a deadline extension (goodwill).</summary>
	public void GrantExtension()
	{
		if ( !Active || Kind != NegotiationKind.Redeem || Customer.Contract is null ) return;

		Game.Pawns.Extend( Customer.Contract );
		Game.Reputation.Add( 1f );
		if ( Customer.IsNamed )
		{
			var rel = Game.Relationships.Get( Customer.Id, Customer.Name );
			if ( rel is not null ) rel.Trust = Math.Min( 1f, rel.Trust + 0.1f );
		}
		CustomerLine = Dialogue.ExtensionGranted( Customer );
		Game.Toast( $"Extended {Customer.Name}'s pawn to day {Customer.Contract.DueDay}.", "more_time" );
		Finish( NegotiationResult.DealDone );
	}

	public void RefuseExtension()
	{
		if ( !Active || Kind != NegotiationKind.Redeem ) return;

		Customer.Mood -= 0.2f;
		Game.Reputation.Add( -0.5f );
		CustomerLine = Dialogue.ExtensionRefused( Customer );
		Finish( NegotiationResult.Rejected );
	}

	/// <summary>Whether this redeemer actually has the money (rolled once per session).</summary>
	public bool RedeemerCanPay { get; private set; }

	// ==================================================================== Finish

	private void Finish( NegotiationResult result )
	{
		Result = result;
		Active = false;
		var cb = OnFinished;
		OnFinished = null;
		UiState.Bump();
		cb?.Invoke( result );
	}

	/// <summary>Hard-abort (day ended, customer despawned mid-talk).</summary>
	public void Abort()
	{
		if ( !Active ) return;
		Finish( NegotiationResult.CustomerLeft );
	}

	// ==================================================================== Helpers

	/// <summary>
	/// What the item LOOKS like it's worth (genuine, hidden problems invisible).
	/// This is the anchor both parties haggle around.
	/// </summary>
	public float ApparentValue( ItemInstance item )
	{
		var def = item.Def;
		if ( def is null ) return 10f;

		double v = def.BaseValue;
		v *= item.Condition.ConditionMult();
		v *= item.Rarity.RarityMult();

		// Visible (eyes-tier) defects are known to both parties.
		var penalty = 0f;
		foreach ( var id in item.Defects )
		{
			var d = DefectCatalog.Get( id );
			if ( d is not null && d.RevealTool == InspectTool.Eyes && !d.CounterfeitSign && !d.StolenSign )
				penalty += d.ValuePenalty;
		}
		v *= Math.Clamp( 1f - penalty, 0.25f, 2.2f );
		v *= 1f - item.Dirtiness * 0.1f;
		v *= Game?.Events.DemandFor( def.Category ) ?? 1f;

		return (float)Math.Max( 10, v );
	}

	private static float KnowledgeNoise( CustomerProfile customer )
	{
		// Low knowledge = wildly wrong self-appraisals (in either direction).
		var swing = 1f - customer.Archetype.Knowledge;
		return Sandbox.Game.Random.Float( 1f - swing * 0.55f, 1f + swing * 0.75f );
	}

	private static int RoundMoney( double v ) => Math.Max( 5, (int)Math.Round( v / 5.0 ) * 5 );
	private static int RoundMoney( float v ) => RoundMoney( (double)v );
}
