using Dynasty.Core.Enums;

namespace Dynasty.Domain.News;

public sealed class NewsItem
{
	public Guid Id { get; set; } = Guid.NewGuid();
	public int Season { get; set; }
	public int Week { get; set; }
	public NewsCategory Category { get; set; }
	public string Headline { get; set; } = "";
	public string Body { get; set; } = "";
	public DateTime PublishedUtc { get; set; }
	public List<string> Tags { get; set; } = new();
}
