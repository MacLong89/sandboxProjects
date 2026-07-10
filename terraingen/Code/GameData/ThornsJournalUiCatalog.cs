namespace Terraingen.GameData;

/// <summary>Journal sidebar icons and section copy backed by current journal snapshot data.</summary>
public static class ThornsJournalUiCatalog
{
	public static string CategoryIconPath( ThornsJourneyCategory category ) =>
		ThornsIconRegistry.JournalCategory( category );

	public static string SectionIconPath( ThornsJournalSection section ) =>
		ThornsIconRegistry.JournalSection( section );

	public static string CategoryTitle( ThornsJourneyCategory category ) => category.ToString().ToUpperInvariant();

	public static string SectionTitle( ThornsJournalSection section ) => section switch
	{
		ThornsJournalSection.Discoveries => "DISCOVERIES",
		ThornsJournalSection.Events => "EVENTS",
		ThornsJournalSection.Achievements => "ACHIEVEMENTS",
		_ => section.ToString().ToUpperInvariant()
	};
}
