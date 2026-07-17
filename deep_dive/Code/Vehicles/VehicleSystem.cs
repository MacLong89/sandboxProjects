namespace DeepDive;

public sealed class VehicleSystem : Component
{
	public MiniSubVehicle ActiveSub { get; private set; }
	private GameObject _subGo;

	public bool HasSubUnlocked( DeepDiveGame game ) =>
		game?.Loadout?.IsUnlocked( 7 ) == true;

	public void RespawnForDive( DeepDiveGame game )
	{
		Clear();
		if ( game is null || !HasSubUnlocked( game ) )
			return;

		var balance = game.Balance;
		var pos = new Vector3( balance.SurfaceSpawnX + 8f, 0.25f, balance.DiveStartZ - 1f );
		_subGo = new GameObject( true, "MiniSub" );
		_subGo.WorldPosition = pos;
		ActiveSub = _subGo.Components.Create<MiniSubVehicle>();
		ActiveSub.SetupAt( pos );
	}

	public void Clear()
	{
		if ( ActiveSub is not null && ActiveSub.Occupied )
		{
			var game = DeepDiveGame.Instance;
			game?.Diver?.SetInVehicle( false );
			game?.Run?.SetVehicleCargoBonus( 0 );
		}

		if ( _subGo.IsValid() )
			_subGo.Destroy();
		_subGo = null;
		ActiveSub = null;
	}

	public bool TryInteract( DeepDiveGame game ) =>
		ActiveSub is not null && ActiveSub.TryToggle( game );
}
