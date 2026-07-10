namespace Terraingen.GameData;

public sealed class ThornsRadioShopSnapshotDto
{
	public bool IsOpen { get; set; }
	public string StationId { get; set; } = "";
	public long Epoch { get; set; }
	public List<ThornsRadioShopOfferDto> Offers { get; set; } = new();
}

public sealed class ThornsRadioShopOfferDto
{
	public string ItemId { get; set; } = "";
	public int BuyPrice { get; set; }
	public int MaxBuy { get; set; }
}
