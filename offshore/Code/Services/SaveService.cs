namespace Offshore;

public sealed class SaveData
{
	public int Version = 1;
	public int Coins = 250;
	public int Day = 1;
	public float MinuteOfDay = 18 * 60 + 45; // sunset start
	public string EquippedRod = "starter_rod";
	public string EquippedReel = "starter_reel";
	public string EquippedHook = "starter_hook";
	public string EquippedLine = "mono_line";
	public string EquippedBait = "worm";
	public string EquippedBoat = "";
	public Dictionary<string, int> BaitCounts = new();
	public List<string> OwnedRods = new();
	public List<string> OwnedReels = new();
	public List<string> OwnedHooks = new();
	public List<string> OwnedLines = new();
	public List<string> OwnedBoats = new();
	public List<CaughtFish> Storage = new();
	public Dictionary<string, FishLogEntry> FishLog = new();
	public int ObjectiveIndex;
	public int ObjectiveProgress;
	public HashSet<string> CompletedObjectives = new();
	public float MasterVolume = 1f;
	public float MusicVolume = 0.7f;
	public float EffectsVolume = 1f;
	public float AmbientVolume = 0.8f;
	public float UiScale = 1f;
	public bool Fullscreen = true;
	public int TotalCaught;
	public int LifetimeCoins;
	public bool HideTutorialTips;
	public HashSet<string> TutorialTipsShown = new();

	public static SaveData NewGame()
	{
		var s = new SaveData();
		s.OwnedRods.Add( "starter_rod" );
		s.OwnedReels.Add( "starter_reel" );
		s.OwnedHooks.Add( "starter_hook" );
		s.OwnedLines.Add( "mono_line" );
		// No starter boat — fish from the dock until you buy one.
		s.EquippedBoat = "";
		s.BaitCounts["worm"] = 10;
		return s;
	}
}

public sealed class FishLogEntry
{
	public string SpeciesId;
	public int TimesCaught;
	public float BestLength;
	public float BestWeight;
	public int BestValue;
}

public static class SaveService
{
	const string Path = "offshore_save.json";
	const int CurrentVersion = 1;

	public static SaveData LoadOrNew( out string warning )
	{
		warning = null;
		try
		{
			if ( !FileSystem.Data.FileExists( Path ) )
				return SaveData.NewGame();

			var data = FileSystem.Data.ReadJsonOrDefault( Path, (SaveData)null );
			if ( data is null )
			{
				warning = "Save was unreadable. Starting a new voyage.";
				return SaveData.NewGame();
			}

			Sanitize( data );
			return data;
		}
		catch ( Exception e )
		{
			Log.Warning( $"[OFFSHORE] Save load failed: {e.Message}" );
			warning = "Save was corrupted. Starting a new voyage.";
			return SaveData.NewGame();
		}
	}

	public static void Write( SaveData data )
	{
		if ( data is null )
			return;
		try
		{
			data.Version = CurrentVersion;
			Sanitize( data );
			FileSystem.Data.WriteJson( Path, data );
		}
		catch ( Exception e )
		{
			Log.Warning( $"[OFFSHORE] Save write failed: {e.Message}" );
		}
	}

	public static void Delete()
	{
		try
		{
			if ( FileSystem.Data.FileExists( Path ) )
				FileSystem.Data.DeleteFile( Path );
		}
		catch ( Exception e )
		{
			Log.Warning( $"[OFFSHORE] Save delete failed: {e.Message}" );
		}
	}

	static void Sanitize( SaveData data )
	{
		data.Version = Math.Max( 1, data.Version );
		data.Coins = Math.Max( 0, data.Coins );
		data.Day = Math.Max( 1, data.Day );
		data.MinuteOfDay = Math.Clamp( data.MinuteOfDay, 0, 24 * 60 - 1 );
		data.BaitCounts ??= new();
		data.OwnedRods ??= new();
		data.OwnedReels ??= new();
		data.OwnedHooks ??= new();
		data.OwnedLines ??= new();
		data.OwnedBoats ??= new();
		data.Storage ??= new();
		data.FishLog ??= new();
		data.CompletedObjectives ??= new();
		data.TutorialTipsShown ??= new();

		if ( Catalog.RodById( data.EquippedRod ) is null ) data.EquippedRod = "starter_rod";
		if ( Catalog.ReelById( data.EquippedReel ) is null ) data.EquippedReel = "starter_reel";
		if ( Catalog.HookById( data.EquippedHook ) is null ) data.EquippedHook = "starter_hook";
		if ( Catalog.LineById( data.EquippedLine ) is null ) data.EquippedLine = "mono_line";
		if ( Catalog.BaitById( data.EquippedBait ) is null ) data.EquippedBait = "worm";

		EnsureOwned( data.OwnedRods, data.EquippedRod, "starter_rod" );
		EnsureOwned( data.OwnedReels, data.EquippedReel, "starter_reel" );
		EnsureOwned( data.OwnedHooks, data.EquippedHook, "starter_hook" );
		EnsureOwned( data.OwnedLines, data.EquippedLine, "mono_line" );

		data.OwnedRods.RemoveAll( id => Catalog.RodById( id ) is null );
		data.OwnedReels.RemoveAll( id => Catalog.ReelById( id ) is null );
		data.OwnedHooks.RemoveAll( id => Catalog.HookById( id ) is null );
		data.OwnedLines.RemoveAll( id => Catalog.LineById( id ) is null );
		data.OwnedBoats.RemoveAll( id => Catalog.BoatById( id ) is null );

		// Boat is optional — never gift a free dinghy. Keep equipped only if owned.
		if ( string.IsNullOrEmpty( data.EquippedBoat ) || Catalog.BoatById( data.EquippedBoat ) is null
			|| !data.OwnedBoats.Contains( data.EquippedBoat ) )
		{
			data.EquippedBoat = data.OwnedBoats.FirstOrDefault() ?? "";
		}
		data.Storage.RemoveAll( f => Catalog.FishById( f.SpeciesId ) is null );

		var badBait = data.BaitCounts.Keys.Where( k => Catalog.BaitById( k ) is null ).ToList();
		foreach ( var k in badBait )
			data.BaitCounts.Remove( k );

		data.ObjectiveIndex = Math.Clamp( data.ObjectiveIndex, 0, Catalog.Objectives.Count );
		data.MasterVolume = Math.Clamp( data.MasterVolume, 0, 1 );
		data.MusicVolume = Math.Clamp( data.MusicVolume, 0, 1 );
		data.EffectsVolume = Math.Clamp( data.EffectsVolume, 0, 1 );
		data.AmbientVolume = Math.Clamp( data.AmbientVolume, 0, 1 );
		data.UiScale = Math.Clamp( data.UiScale, 0.75f, 1.5f );
	}

	static void EnsureOwned( List<string> list, string equipped, string fallback )
	{
		if ( !list.Contains( fallback ) )
			list.Add( fallback );
		if ( !string.IsNullOrEmpty( equipped ) && !list.Contains( equipped ) )
			list.Add( equipped );
	}
}
