namespace OffshoreFishing.Core;

public sealed class HiredBoatService
{
	private readonly GameContent _content;
	private readonly List<IDomainEvent> _events = new();

	public HiredBoatService( GameContent content )
	{
		_content = content;
	}

	public IReadOnlyList<IDomainEvent> DrainEvents()
	{
		var copy = _events.ToList();
		_events.Clear();
		return copy;
	}

	public void Tick( GameState state, SeededRng rng, double dtSeconds )
	{
		foreach ( var id in state.OwnedHiredBoatIds.ToList() )
		{
			var def = _content.GetHiredBoat( id );
			if ( !state.HiredBoatTripTimers.ContainsKey( id ) )
				state.HiredBoatTripTimers[id] = 0;

			state.HiredBoatTripTimers[id] += dtSeconds;
			var tripSeconds = def.TripMinutes * 60.0;
			while ( state.HiredBoatTripTimers[id] >= tripSeconds )
			{
				state.HiredBoatTripTimers[id] -= tripSeconds;
				var gold = rng.NextInt( def.GoldPerTripMin, def.GoldPerTripMax + 1 );
				state.Gold += gold;
				state.TotalGoldEarned += gold;
				_events.Add( new GoldChangedEvent { Gold = state.Gold, Delta = gold } );
				_events.Add( new HiredBoatReturnedEvent { HiredBoatId = id, GoldGained = gold } );
			}
		}
	}

	public int ApplyOffline( GameState state, SeededRng rng, TimeSpan offline )
	{
		var cap = TimeSpan.FromHours( _content.Economy.OfflineCapHours );
		if ( offline < TimeSpan.Zero ) offline = TimeSpan.Zero;
		if ( offline > cap ) offline = cap;

		var before = state.Gold;
		Tick( state, rng, offline.TotalSeconds );
		return state.Gold - before;
	}
}
