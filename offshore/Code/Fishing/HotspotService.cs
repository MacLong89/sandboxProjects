namespace Offshore;

public sealed class Hotspot
{
	public float WorldX;
	public float Duration;
	public float Age;
	public float Intensity = 1f;
	public string SpeciesBias;
	public string Visual = "birds";
	public WeatherType PreferWeather;
	public DayPhase PreferTime;
}

public sealed class HotspotService
{
	public List<Hotspot> Active { get; } = new();
	readonly Random _rng = new();
	float _spawnTimer = 8f;

	public void Tick( float dt, float cameraX, float distance, WeatherService weather, DayPhase phase )
	{
		_spawnTimer -= dt;
		for ( var i = Active.Count - 1; i >= 0; i-- )
		{
			Active[i].Age += dt;
			if ( Active[i].Age >= Active[i].Duration )
				Active.RemoveAt( i );
		}

		if ( _spawnTimer > 0f || Active.Count >= 4 )
			return;

		_spawnTimer = 18f + (float)_rng.NextDouble() * 28f;
		var biasFish = Catalog.Fish.Where( f => distance >= f.MinDistance * 0.8f && distance <= f.MaxDistance ).OrderBy( _ => _rng.Next() ).FirstOrDefault()
			?? Catalog.Fish[0];

		Active.Add( new Hotspot
		{
			WorldX = cameraX + 80f + (float)_rng.NextDouble() * 420f,
			Duration = 40f + (float)_rng.NextDouble() * 50f,
			Intensity = 0.7f + (float)_rng.NextDouble() * 0.6f,
			SpeciesBias = biasFish.Id,
			Visual = _rng.Next( 4 ) switch { 0 => "birds", 1 => "splash", 2 => "bubbles", _ => "school" },
			PreferWeather = weather.Current,
			PreferTime = phase
		} );
	}

	public (float strength, string bias) Sample( float worldX )
	{
		float best = 0f;
		string bias = null;
		foreach ( var h in Active )
		{
			var d = Math.Abs( h.WorldX - worldX );
			if ( d > 90f ) continue;
			var s = h.Intensity * (1f - d / 90f);
			if ( s > best )
			{
				best = s;
				bias = h.SpeciesBias;
			}
		}
		return (best, bias);
	}
}
