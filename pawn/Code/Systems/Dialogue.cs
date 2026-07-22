namespace PawnShop;

/// <summary>Canned customer dialogue, flavored by archetype.</summary>
public static class Dialogue
{
	private static string Pick( params string[] lines ) => lines[Game.Random.Int( 0, lines.Length - 1 )];

	public static string SellOpen( CustomerProfile c, ItemInstance item ) => c.Archetype.Id switch
	{
		Archetype.DesperateSeller => Pick(
			$"Look, I need cash today. This {item.Name} — what can you give me?",
			$"I hate to let this {item.Name} go, but rent won't wait. Make me an offer." ),
		Archetype.ToughNegotiator => Pick(
			$"I know exactly what this {item.Name} sells for, so let's skip the games.",
			$"One {item.Name}, excellent example. I've already turned down two offers this week." ),
		Archetype.CluelessSeller => Pick(
			$"Found this {item.Name} clearing out the attic. Is it worth anything?",
			$"My cousin says this {item.Name} is worth a fortune. He watches all the shows." ),
		Archetype.Scammer => Pick(
			$"Friend, this {item.Name} is a once-in-a-lifetime piece. I'm only selling because I'm moving.",
			$"Genuine {item.Name}, barely used. I'd keep it, but money's money, right?" ),
		Archetype.SuspiciousSeller => Pick(
			$"Got a {item.Name} here. Quick sale, good price. You want it or not?",
			$"This {item.Name}? Belonged to my, uh, uncle. Look, can we hurry this up?" ),
		_ => Pick(
			$"I'm looking to sell this {item.Name}. What's it worth to you?",
			$"Morning. Any interest in a {item.Name}?" ),
	};

	public static string PawnOpen( CustomerProfile c, ItemInstance item, int loan ) => Pick(
		$"I don't want to sell my {item.Name} — just need a loan against it. {GameConstants.FormatCash( loan )} should do it.",
		$"Can you hold my {item.Name} for a while? I need about {GameConstants.FormatCash( loan )}, and I WILL be back for it.",
		$"Pawn, not sale. {GameConstants.FormatCash( loan )} on the {item.Name} and I'll square up on payday." );

	public static string BuyerOpen( CustomerProfile c, ItemInstance item, int offer ) => c.Archetype.Id switch
	{
		Archetype.BargainHunter => Pick(
			$"That {item.Name} has been sitting a while, hasn't it? I'll do you a favor — {GameConstants.FormatCash( offer )}.",
			$"I've seen the {item.Name} cheaper online. {GameConstants.FormatCash( offer )}, cash, right now." ),
		Archetype.WealthyBuyer => Pick(
			$"The {item.Name} — charming. Shall we say {GameConstants.FormatCash( offer )}?",
			$"I'd like the {item.Name}. {GameConstants.FormatCash( offer )} feels right to me." ),
		Archetype.Collector => Pick(
			$"That {item.Name}... I've been hunting one of these. {GameConstants.FormatCash( offer )}?",
			$"Interesting {item.Name}. {GameConstants.FormatCash( offer )} — and I know what these go for." ),
		_ => Pick(
			$"How about {GameConstants.FormatCash( offer )} for the {item.Name}?",
			$"I like the {item.Name}, but not the sticker. {GameConstants.FormatCash( offer )}?" ),
	};

	public static string RedeemOpen( CustomerProfile c, PawnContract contract ) => Pick(
		$"Told you I'd be back! Here to pick up my item — {GameConstants.FormatCash( contract.RedemptionAmount )}, right?",
		$"I've got the money for my pawn. All {GameConstants.FormatCash( contract.RedemptionAmount )} of it." );

	public static string Counter( CustomerProfile c, int ask ) => Pick(
		$"I was hoping for more than that. {GameConstants.FormatCash( ask )} and we're talking.",
		$"Come on now. {GameConstants.FormatCash( ask )}.",
		$"You're squeezing me here. {GameConstants.FormatCash( ask )} is fair." );

	public static string FinalOffer( CustomerProfile c, int ask ) => Pick(
		$"Fine. {GameConstants.FormatCash( ask )} — but that's my lowest.",
		$"{GameConstants.FormatCash( ask )}, take it or leave it." );

	public static string Insulted( CustomerProfile c ) => c.Archetype.Id switch
	{
		Archetype.DesperateSeller => Pick(
			"That's... that's insulting. I'm desperate, not stupid.",
			"I NEED this money, but not that badly." ),
		Archetype.ToughNegotiator => Pick(
			"Ha! You're not serious. Try again with a real number.",
			"I know what this is worth. Don't waste my time." ),
		_ => Pick(
			"That's an insult and you know it.",
			"Wow. I expected better from this place." ),
	};

	public static string StormOut( CustomerProfile c ) => Pick(
		"Forget it. I'll take my business elsewhere.",
		"No deal. And I'll be telling people about this place.",
		"We're done here." );

	public static string OutOfPatience( CustomerProfile c ) => Pick(
		"I don't have all day. Forget it.",
		"You know what? Never mind.",
		"I've got somewhere to be. Deal's off." );

	public static string FlawAcknowledged( CustomerProfile c, DefectDef defect ) => c.Archetype.Honesty > 0.6f
		? Pick(
			$"The {defect.Name.ToLower()}? Yeah... okay, that's fair. I can come down.",
			$"Huh, I didn't even notice the {defect.Name.ToLower()}. Fine, adjust the price." )
		: Pick(
			$"That {defect.Name.ToLower()} is barely noticeable! ...But fine.",
			$"Everyone's a critic. Okay, okay, a little less." );

	public static string FakeExposed( CustomerProfile c ) => c.Archetype.Honesty > 0.6f
		? Pick(
			"Fake?! I had no idea, I swear. Oh, this is embarrassing...",
			"It can't be... my brother-in-law GAVE me this!" )
		: Pick(
			"...Alright, look. Maybe it's 'inspired by' the original. Still worth something!",
			"Fake is a strong word. Let's call it a tribute." );

	public static string ScammerBolts( CustomerProfile c ) => Pick(
		"You know what — wrong shop. Forget you saw me.",
		"That's slander! I don't have to stand here for this. Goodbye!" );

	public static string ShadyBolts( CustomerProfile c ) => Pick(
		"Whoa, what's with the questions? Forget it, I'm out.",
		"You a cop now? Deal's off." );

	public static string ShadyDeflects( CustomerProfile c ) => Pick(
		"It's from my uncle's estate, alright? Look, I'll go lower, just... no more questions.",
		"Long story. Boring story. How about I just drop the price?" );

	public static string StoryAnswer( CustomerProfile c, bool shady ) => shady
		? Pick( "I told you, it's mine. Mostly. Legally speaking it's complicated.", "Why does everyone ask that? It's MINE." )
		: Pick( "Bought it new, kept the box and everything. Anything else, detective?", "It was a gift from my late aunt, if you must know." );

	public static string PlayerRejects( CustomerProfile c ) => Pick(
		"Your loss. Someone else will want it.",
		"Suit yourself.",
		"Really? Well... alright then." );

	public static string DealClosed( CustomerProfile c ) => c.Archetype.Id switch
	{
		Archetype.DesperateSeller => Pick( "Oh, thank you. Seriously — thank you.", "You're a lifesaver. Really." ),
		Archetype.ToughNegotiator => Pick( "Pleasure doing business. You held your ground — I respect that.", "Deal. You drive a fair bargain." ),
		Archetype.Scammer => Pick( "Smart buy, friend. Real smart. Gotta run!", "Pleasure! No refunds. I mean — goodbye!" ),
		_ => Pick( "Deal! Pleasure doing business.", "Alright, it's yours. Take care of it." ),
	};

	public static string PawnClosed( CustomerProfile c, PawnContract contract ) => Pick(
		$"Deal. I'll be back before day {contract.DueDay}, count on it.",
		$"You'll see me again by day {contract.DueDay}. Keep it safe, alright?" );

	public static string FeeTooHigh( CustomerProfile c ) => Pick(
		"That fee is robbery! The loan's fine, but ease up on the interest.",
		"I need a loan, not a second mortgage. Lower the fee." );

	public static string LoanTooSmall( CustomerProfile c, int requested ) => Pick(
		$"That won't cover it. I need closer to {GameConstants.FormatCash( requested )}.",
		$"Can't do much with that. Something nearer {GameConstants.FormatCash( requested )}?" );

	public static string BuyerRaises( CustomerProfile c, int offer ) => Pick(
		$"Hmm. {GameConstants.FormatCash( offer )}. That's me stretching.",
		$"You drive a hard bargain. {GameConstants.FormatCash( offer )}?" );

	public static string BuyerTooRich( CustomerProfile c ) => Pick(
		"At that price I'll just buy new. Be reasonable.",
		"That's more than my budget and my patience combined." );

	public static string BuyerWalks( CustomerProfile c ) => Pick(
		"Never mind. Keep it.",
		"I'll think about it. (I won't.)" );

	public static string BuyerUnconvinced( CustomerProfile c ) => Pick(
		"Nice speech. The number stands.",
		"Uh-huh. And I'm sure every item here is 'museum quality'." );

	public static string BuyerConvinced( CustomerProfile c, int offer ) => Pick(
		$"Verified, you say? Alright... {GameConstants.FormatCash( offer )}.",
		$"Okay, that does change things. {GameConstants.FormatCash( offer )}." );

	public static string BuyerHappy( CustomerProfile c ) => Pick(
		"Excellent. I'll take good care of it.",
		"Pleasure doing business with you!" );

	public static string RedeemDone( CustomerProfile c ) => Pick(
		"There it is! Good as the day I left it. Thanks for holding onto it.",
		"You have no idea what this means. Thank you." );

	public static string ExtensionGranted( CustomerProfile c ) => Pick(
		"You're a good one, you know that? I won't forget this.",
		"Thank you. Just a few more days, I promise." );

	public static string ExtensionRefused( CustomerProfile c ) => Pick(
		"...Right. Rules are rules, I guess. I'll try to get the money.",
		"Come on, it's just a few days! Fine. FINE." );

	public static string ExtensionAsk( CustomerProfile c, PawnContract contract ) => Pick(
		$"So... I'm a little short this week. Any chance of a few more days on my pawn?",
		$"I don't have the full {GameConstants.FormatCash( contract.RedemptionAmount )} yet. Can we extend it? Please?" );
}
