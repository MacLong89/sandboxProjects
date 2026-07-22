namespace Offshore;

public sealed class WorldEvent
{
	public string Kind;
	public float WorldX;
	public float Life;
	public float Age;
	public string Label;
	public int CoinReward;
	public string BaitReward;
	public int BaitAmount;
	public bool Collected;
}

public sealed class WorldEventService
{
	public List<WorldEvent> Events { get; } = new();
	readonly Random _rng = new();
	float _timer = 12f;

	static readonly string[] Kinds =
	{
		// Ambient / wildlife only for now — crate/npc_boat/cargo cluttered the sky while Z was wrong.
		"dolphin", "turtle", "birds", "buoy", "bottle", "kelp"
	};

	public void Tick( float dt, float cameraX, float distance )
	{
		_timer -= dt;
		for ( var i = Events.Count - 1; i >= 0; i-- )
		{
			Events[i].Age += dt;
			if ( Events[i].Age >= Events[i].Life )
				Events.RemoveAt( i );
		}

		if ( _timer > 0f || Events.Count >= 5 )
			return;

		_timer = 14f + (float)_rng.NextDouble() * 22f;
		var kind = Kinds[_rng.Next( Kinds.Length )];
		if ( distance < 80 && kind is "rig" or "whale" )
			kind = "buoy";

		var ev = new WorldEvent
		{
			Kind = kind,
			WorldX = cameraX + 200f + (float)_rng.NextDouble() * 500f,
			Life = 55f + (float)_rng.NextDouble() * 40f,
			Label = LabelFor( kind )
		};

		if ( kind is "crate" or "bottle" )
		{
			ev.CoinReward = 15 + _rng.Next( 40 );
			if ( _rng.NextDouble() < 0.45 )
			{
				ev.BaitReward = "worm";
				ev.BaitAmount = 2 + _rng.Next( 4 );
			}
		}

		Events.Add( ev );
	}

	public WorldEvent TryCollect( float boatX )
	{
		foreach ( var e in Events )
		{
			if ( e.Collected ) continue;
			if ( e.Kind is not ("crate" or "bottle" or "buoy") ) continue;
			if ( Math.Abs( e.WorldX - boatX ) > 36f ) continue;
			e.Collected = true;
			return e;
		}
		return null;
	}

	static string LabelFor( string kind ) => kind switch
	{
		"dolphin" => "Dolphins nearby",
		"whale" => "Whale spout",
		"turtle" => "Sea turtle",
		"birds" => "Diving birds",
		"crate" => "Floating crate",
		"buoy" => "Marker buoy",
		"log" => "Drifting timber",
		"bottle" => "Message bottle",
		"npc_boat" => "Fishing boat",
		"cargo" => "Cargo ship",
		"lighthouse" => "Distant lighthouse",
		"rig" => "Offshore platform",
		"kelp" => "Kelp mat",
		"stormfront" => "Storm front",
		_ => "Ocean event"
	};
}
