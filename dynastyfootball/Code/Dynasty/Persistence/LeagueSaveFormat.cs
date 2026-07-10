namespace Dynasty.Persistence;

/// <summary>
/// Versioned save envelope for migration support and future web companion sync.
/// </summary>
public sealed class LeagueSaveEnvelope
{
	public int SaveFormatVersion { get; set; } = LeagueSaveSerializer.CurrentSaveFormatVersion;
	public string GameVersion { get; set; } = "0.1.0";
	public DateTime SavedUtc { get; set; }
	public string SaveSlotId { get; set; } = "";
	public string LeagueName { get; set; } = "";
	public Domain.League.LeagueState League { get; set; }
}
