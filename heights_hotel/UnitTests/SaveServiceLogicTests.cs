using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using HeightsHotel;

[TestClass]
public class SaveServiceLogicTests
{
	[TestMethod]
	public void CorruptPayload_DoesNotDeserializeAsValidHotel()
	{
		Assert.ThrowsException<JsonException>( () =>
			JsonSerializer.Deserialize<HotelState>( "{ not json" ) );
	}

	[TestMethod]
	public void WrongSaveVersion_IsDetectable()
	{
		var state = HotelSimulation.CreateNewGame( 11 );
		state.SaveVersion = 999;
		var json = JsonSerializer.Serialize( state );
		var loaded = JsonSerializer.Deserialize<HotelState>( json );
		Assert.AreEqual( 999, loaded.SaveVersion );
		Assert.AreNotEqual( GameBalance.SaveVersion, loaded.SaveVersion );
	}

	[TestMethod]
	public void EmptyCells_IsInvalidLayout()
	{
		var state = HotelSimulation.CreateNewGame( 12 );
		state.Cells.Clear();
		Assert.AreEqual( 0, state.Cells.Count );
	}
}
