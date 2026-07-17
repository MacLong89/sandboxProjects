using OffshoreFishing.Core;

namespace Sandbox;

public static class SaveAdapter
{
	private const string SavePath = "save/offshore_slot1.json";
	private const string BackupPath = "save/offshore_slot1.bak.json";

	public static void Save( GameSession session )
	{
		try
		{
			var dto = session.ToSaveDto();
			if ( FileSystem.Data.FileExists( SavePath ) )
			{
				var prev = FileSystem.Data.ReadAllText( SavePath );
				FileSystem.Data.WriteAllText( BackupPath, prev );
			}

			FileSystem.Data.WriteJson( SavePath, dto );
		}
		catch ( Exception e )
		{
			Log.Warning( $"[Offshore] Save failed: {e.Message}" );
		}
	}

	public static bool TryLoad( out SaveGameDto dto )
	{
		dto = null;
		try
		{
			if ( FileSystem.Data.FileExists( SavePath ) )
			{
				dto = FileSystem.Data.ReadJson<SaveGameDto>( SavePath );
				if ( dto?.State != null ) return true;
			}

			if ( FileSystem.Data.FileExists( BackupPath ) )
			{
				dto = FileSystem.Data.ReadJson<SaveGameDto>( BackupPath );
				return dto?.State != null;
			}
		}
		catch ( Exception e )
		{
			Log.Warning( $"[Offshore] Load failed: {e.Message}" );
		}

		return false;
	}

	public static void Delete()
	{
		try
		{
			if ( FileSystem.Data.FileExists( SavePath ) )
				FileSystem.Data.DeleteFile( SavePath );
		}
		catch ( Exception e )
		{
			Log.Warning( $"[Offshore] Delete save failed: {e.Message}" );
		}
	}
}
