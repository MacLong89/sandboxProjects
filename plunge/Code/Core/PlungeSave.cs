using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Plunge;

public sealed class PlungeSaveData
{
	public int Gold { get; set; } = 40;
	public int Gems { get; set; }
	public int Level { get; set; } = 1;
	public int Xp { get; set; }
	public int XpTarget { get; set; } = 120;
	public int Day { get; set; } = 1;
	public int MinuteOfDay { get; set; } = 510;
	public Dictionary<string, string> Equipped { get; set; } = new();
	public List<string> OwnedGear { get; set; } = new();
	public List<string> OwnedSubs { get; set; } = new();
	public string EquippedSub { get; set; }
	public int SubLevel { get; set; } = 1;
	public Dictionary<string, int> SubUpgrades { get; set; } = new();
	public HashSet<string> FishLog { get; set; } = new();
	public HashSet<string> ArtifactLog { get; set; } = new();
	public HashSet<string> CreatureLog { get; set; } = new();
	public HashSet<string> BiomeLog { get; set; } = new() { "shallows" };
	public List<DiveRecord> DiveHistory { get; set; } = new();
	public float DeepestDive { get; set; }
	public float LongestDive { get; set; }
	public int MostItems { get; set; }
	public int MostCredits { get; set; }
	public int TotalDives { get; set; }
	public float TotalDiveTime { get; set; }
	public int TotalItems { get; set; }
	public int TotalCredits { get; set; }
	public int TutorialStep { get; set; }
	public bool HasSeenSubUnlock { get; set; }
	public bool HideTutorialTips { get; set; }
	public List<string> TutorialTipsShown { get; set; } = new();

	/// <summary>Tutorial gate — player bought gear, a sub, or an upgrade.</summary>
	public bool HasShopPurchase { get; set; }

	public static PlungeSaveData NewGame()
	{
		var data = new PlungeSaveData();
		foreach ( var slot in Catalog.Slots )
		{
			var starter = Catalog.GearFor( slot ).OrderBy( x => x.Tier ).First();
			data.Equipped[slot] = starter.Id;
			data.OwnedGear.Add( starter.Id );
		}
		foreach ( var id in new[] { "Hull", "Engine", "O2 Tank", "Sonar", "Lights", "Armor", "Storage" } )
			data.SubUpgrades[id] = 1;
		return data;
	}
}

public static class PlungeSave
{
	private const string Path = "plunge/save.json";

	public static PlungeSaveData Load()
	{
		try
		{
			if ( !FileSystem.Data.FileExists( Path ) )
				return PlungeSaveData.NewGame();
			return FileSystem.Data.ReadJsonOrDefault( Path, PlungeSaveData.NewGame() );
		}
		catch ( Exception ex )
		{
			Log.Warning( $"PLUNGE save could not be loaded: {ex.Message}" );
			return PlungeSaveData.NewGame();
		}
	}

	public static void Write( PlungeSaveData data )
	{
		try
		{
			FileSystem.Data.WriteJson( Path, data );
		}
		catch ( Exception ex )
		{
			Log.Warning( $"PLUNGE save could not be written: {ex.Message}" );
		}
	}
}
