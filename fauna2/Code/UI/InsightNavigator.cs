using Fauna2;

namespace Fauna2.UI;

/// <summary>Deep-links guest problems and suggestions to the tool that fixes them.</summary>
public static class InsightNavigator
{
	public static string ActionLabel( GuestInsight insight )
	{
		try
		{
			return Resolve( insight ).Label;
		}
		catch ( Exception e )
		{
			Log.Warning( $"[Fauna2 UI] Insight action label failed for '{insight.Title}' — {e.Message}" );
			return "Take action →";
		}
	}

	public static void Open( GuestInsight insight )
	{
		try
		{
			Resolve( insight ).Open();
		}
		catch ( Exception e )
		{
			Log.Warning( $"[Fauna2 UI] Insight open failed for '{insight.Title}' — {e.Message}" );
		}
	}

	private static (string Label, Action Open) Resolve( GuestInsight insight ) =>
		insight.IsPositive ? ResolveWant( insight ) : ResolveHarm( insight );

	private static (string Label, Action Open) ResolveHarm( GuestInsight insight ) =>
		insight.Icon switch
		{
			"door_front" => ("Build entrance →", () => UiState.BeginPlace( "entrance" ) ),
			"route" => ("Lay paths →", () => UiState.BeginPlace( "path_straight" ) ),
			"wc" => ("Build restroom →", () => UiState.BeginPlace( "restroom" ) ),
			"restaurant" => ("Build restaurant →", () => UiState.BeginPlace( "restaurant" ) ),
			"storefront" => ("Build gift shop →", () => UiState.BeginPlace( "kiosk" ) ),
			"fence" => ("Build habitat →", () => UiState.BeginPlaceStarterHabitat() ),
			"park" or "grass" => ("Enrich habitat →", () => UiState.BeginPlace( "feeder" ) ),
			"pets" or "diversity_3" => ("Adopt animals →", () => UiState.OpenPage( UiPage.Market ) ),
			"emoji_objects" => ("Add decor →", () => UiState.OpenBuild( BuildCategory.Decorations ) ),
			"cleaning_services" => ("Extend paths →", () => UiState.BeginPlace( "path_straight" ) ),
			"sentiment_dissatisfied" => FollowUpHarmAction( insight.Icon ),
			_ => FollowUpHarmAction( insight.Icon ),
		};

	private static (string Label, Action Open) ResolveWant( GuestInsight insight )
	{
		return insight.Icon switch
		{
			"menu_book" => ("Open market →", () => UiState.OpenPage( UiPage.Market ) ),
			"route" => ("Lay paths →", () => UiState.BeginPlace( "path_straight" ) ),
			"emoji_objects" => ("Add decor →", () => UiState.OpenBuild( BuildCategory.Decorations ) ),
			"grass" => ("Enrich habitat →", () => UiState.BeginPlace( "pond" ) ),
			"thumb_up" or "star" or "waving_hand" => ("View stats →", () => UiState.OpenPage( UiPage.Stats ) ),
			_ => ("Take action →", () => UiState.OpenPage( UiPage.Market ) ),
		};
	}

	/// <summary>Jump from a summary harm to the next concrete fix — never recurse.</summary>
	private static (string Label, Action Open) FollowUpHarmAction( string skipIcon )
	{
		foreach ( var harm in ZooStatsReport.GetRatingHarms() )
		{
			if ( harm.Icon == skipIcon || harm.Icon == "sentiment_dissatisfied" )
				continue;

			if ( string.IsNullOrEmpty( harm.Title ) )
				continue;

			return ResolveHarm( harm );
		}

		return ("View stats →", () => UiState.OpenPage( UiPage.Stats ) );
	}
}
