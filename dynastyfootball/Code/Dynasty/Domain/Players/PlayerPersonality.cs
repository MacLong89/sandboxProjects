namespace Dynasty.Domain.Players;

public sealed class PlayerPersonality
{
	public int Ambition { get; set; } = 50;
	public int Loyalty { get; set; } = 50;
	public int Leadership { get; set; } = 50;
	public int WorkEthic { get; set; } = 50;
	public int Temperament { get; set; } = 50;
	public int Ego { get; set; } = 50;
	public int Marketability { get; set; } = 50;
}

public sealed class PlayerMoraleState
{
	public int Morale { get; set; } = 70;
	public bool TradeRequested { get; set; }
	public bool Holdout { get; set; }
	public string LastConcern { get; set; } = "";
}
