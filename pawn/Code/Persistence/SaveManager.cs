namespace PawnShop;

/// <summary>Loads/saves <see cref="SaveData"/> as JSON under FileSystem.Data.</summary>
public static class SaveManager
{
	public static bool SaveExists()
	{
		try { return FileSystem.Data.FileExists( GameConstants.SaveFile ); }
		catch { return false; }
	}

	public static SaveData Load()
	{
		try
		{
			if ( FileSystem.Data.FileExists( GameConstants.SaveFile ) )
			{
				var json = FileSystem.Data.ReadAllText( GameConstants.SaveFile );
				var data = Json.Deserialize<SaveData>( json );
				if ( data is not null )
					return Migrate( data );
			}
		}
		catch ( Exception e )
		{
			Log.Warning( $"[PawnShop] Save load failed, starting fresh: {e.Message}" );
		}

		return new SaveData();
	}

	public static SaveData Wipe()
	{
		try
		{
			if ( FileSystem.Data.FileExists( GameConstants.SaveFile ) )
				FileSystem.Data.DeleteFile( GameConstants.SaveFile );
		}
		catch ( Exception e )
		{
			Log.Warning( $"[PawnShop] Save wipe failed: {e.Message}" );
		}

		return new SaveData();
	}

	public static void Save( SaveData data )
	{
		if ( data is null ) return;

		try
		{
			var dir = System.IO.Path.GetDirectoryName( GameConstants.SaveFile )?.Replace( '\\', '/' );
			if ( !string.IsNullOrEmpty( dir ) )
				FileSystem.Data.CreateDirectory( dir );

			FileSystem.Data.WriteAllText( GameConstants.SaveFile, Json.Serialize( data ) );
		}
		catch ( Exception e )
		{
			Log.Warning( $"[PawnShop] Save write failed: {e.Message}" );
		}
	}

	private static SaveData Migrate( SaveData data )
	{
		// Defensive nulls for saves written by older/newer builds.
		data.Inventory ??= new List<ItemInstance>();
		data.PawnContracts ??= new List<PawnContract>();
		data.Upgrades ??= new List<string>();
		data.Tools ??= new List<string>();
		data.Relationships ??= new Dictionary<string, RelationshipData>();

		if ( !data.Tools.Contains( nameof( InspectTool.Eyes ) ) )
			data.Tools.Add( nameof( InspectTool.Eyes ) );

		// Drop inventory entries whose definitions no longer exist.
		data.Inventory.RemoveAll( i => ItemCatalog.Get( i.DefId ) is null );
		foreach ( var item in data.Inventory )
		{
			item.Defects ??= new List<string>();
			item.DiscoveredDefects ??= new List<string>();
			item.CheckedSpots ??= new List<int>();
			item.Defects.RemoveAll( d => DefectCatalog.Get( d ) is null );
			item.DiscoveredDefects.RemoveAll( d => DefectCatalog.Get( d ) is null );
		}

		// Orphaned pawn contracts (item gone) are cancelled gracefully.
		data.PawnContracts.RemoveAll( c => data.Inventory.All( i => i.Id != c.ItemId ) );

		data.Version = SaveData.CurrentVersion;
		return data;
	}
}
