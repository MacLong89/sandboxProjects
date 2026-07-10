using Dynasty.Domain.League;

namespace Dynasty.UI.ViewModels;

public sealed class NewsViewModel
{
	public IReadOnlyList<NewsRow> Articles { get; init; } = Array.Empty<NewsRow>();

	public static NewsViewModel From( LeagueState state )
	{
		if ( state == null )
			return new NewsViewModel();

		return new NewsViewModel
		{
			Articles = state.News.Select( NewsRow.From ).ToList()
		};
	}
}

public sealed class NewsDetailViewModel
{
	public Guid Id { get; init; }
	public string Headline { get; init; } = "";
	public string Category { get; init; } = "";
	public string Body { get; init; } = "";
	public string WeekLabel { get; init; } = "";
	public string PublishedLabel { get; init; } = "";

	public static NewsDetailViewModel From( LeagueState state, Guid articleId )
	{
		if ( state == null )
			return null;

		var item = state.News.FirstOrDefault( n => n.Id == articleId );
		if ( item == null )
			return null;

		return new NewsDetailViewModel
		{
			Id = item.Id,
			Headline = item.Headline,
			Category = item.Category.ToString(),
			Body = string.IsNullOrEmpty( item.Body ) ? "No additional details." : item.Body,
			WeekLabel = $"Season {item.Season} · Week {item.Week}",
			PublishedLabel = item.PublishedUtc.ToString( "MMM d, yyyy" )
		};
	}
}
