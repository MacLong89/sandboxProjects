namespace OffshoreFishing.Core;

public static class SaveSchema
{
	public const int CurrentVersion = 1;
}

public sealed class SaveGameDto
{
	public int SchemaVersion { get; set; } = SaveSchema.CurrentVersion;
	public GameState State { get; set; }
	public string Checksum { get; set; }
}

public static class SaveMigrator
{
	public static GameState Migrate( SaveGameDto dto )
	{
		if ( dto?.State == null )
			throw new InvalidOperationException( "Save is empty." );

		var state = dto.State;
		var version = dto.SchemaVersion;

		// Future migrations go here.
		if ( version < 1 )
			version = 1;

		state.SchemaVersion = SaveSchema.CurrentVersion;
		return state;
	}
}
