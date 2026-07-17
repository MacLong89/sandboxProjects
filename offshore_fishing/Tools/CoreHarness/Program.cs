using System.Text.Json;
using OffshoreFishing.Core;

var content = ContentCatalog.Create();
Console.WriteLine( $"Content OK: {content.Fish.Count} fish, {content.Zones.Count} zones, {content.Items.Count} items" );
var dataDir = Path.GetFullPath( Path.Combine( AppContext.BaseDirectory, "..", "..", "..", "..", "..", "Assets", "Data" ) );
if ( !Directory.Exists( dataDir ) )
	dataDir = Path.GetFullPath( Path.Combine( Directory.GetCurrentDirectory(), "Assets", "Data" ) );
Directory.CreateDirectory( dataDir );
var json = JsonSerializer.Serialize( content, new JsonSerializerOptions { WriteIndented = true } );
File.WriteAllText( Path.Combine( dataDir, "content.json" ), json );
Console.WriteLine( $"Exported content.json to {dataDir}" );

// Smoke: new game catch path
var session = new GameSession( content, GameSession.CreateNewState( 7 ) );
session.BeginCastAim();
session.SetAim( -0.7f );
for ( var i = 0; i < 30; i++ ) session.ChargeCast( 0.05f );
session.ReleaseCast();
for ( var i = 0; i < 600 && session.State.Mode != GameMode.CatchReveal; i++ )
{
	session.Advance( 0.05 );
	var f = session.State.Fishing;
	if ( f.Phase == FishingPhase.BiteWindow ) session.TryHook();
	if ( f.Phase == FishingPhase.Fighting ) session.SetReelHeld( MathF.Abs( f.LineTension - f.SafeZoneCenter ) > 0.02f ? f.LineTension < f.SafeZoneCenter : true );
}
if ( session.State.Mode == GameMode.CatchReveal )
{
	var fish = session.State.Fishing.PendingCatch;
	Console.WriteLine( $"Caught {fish.FishId} worth {fish.Worth}" );
	session.CloseCatchReveal();
}
else
{
	Console.WriteLine( $"No catch after smoke fight. phase={session.State.Fishing.Phase}" );
}

session.OpenShop();
var sold = session.SellAll();
Console.WriteLine( $"Sold for {sold}, gold={session.State.Gold}" );
session.BuyItem( "spool_braided" );
Console.WriteLine( $"Owned spool_braided={session.State.OwnedItemIds.Contains( "spool_braided" )}" );

// Save roundtrip (in-memory)
var dto = session.ToSaveDto();
var loaded = GameSession.FromSave( content, dto );
Console.WriteLine( $"Save roundtrip gold={loaded.State.Gold} catches={loaded.State.TotalCatches}" );

Console.WriteLine( "Running 300-minute balance sim..." );
var report = BalanceSimulator.Run( 300, 42 );
Console.WriteLine( $"Balance: gold={report.FinalGold} catches={report.TotalCatches} zones={report.ZonesUnlocked} fish={report.FishDiscovered}" );
Console.WriteLine( $"trench={report.ReachedTrench} oceanic={report.BoughtOceanic} crew2={report.HiredSecondCrew}" );
Console.WriteLine( $"upgradeBy3m={report.FirstUpgradeByMinute3} offshoreBy10m={report.FirstOffshoreByMinute10}" );

var ok = report.FirstUpgradeByMinute3
	&& report.FirstOffshoreByMinute10
	&& report.ZonesUnlocked >= 4
	&& report.ReachedTrench
	&& report.TotalCatches > 80
	&& report.FinalGold < 500_000;
Console.WriteLine( ok ? "HARNESS PASS" : "HARNESS WARN (soft pacing)" );
if ( !ok )
{
	Console.WriteLine( "Fail details: need upgrade@3, offshore@10, zones>=4, trench, catches>80, gold<500k (bot is optimistic)" );
}
return ok ? 0 : 2;
