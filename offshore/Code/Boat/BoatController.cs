namespace Offshore;

public sealed class BoatController
{
	public float WorldX { get; set; }
	public float Velocity { get; private set; }
	public float Fuel { get; set; }
	public float BobPhase { get; private set; }
	public float RockAngle { get; private set; }
	public float WakeStrength { get; private set; }
	public bool Aboard { get; set; }
	/// <summary>+1 facing open water (right), −1 facing back toward the dock (left).</summary>
	public float Facing { get; private set; } = 1f;
	public bool AtDock => WorldX <= DockX + 8f;

	public const float DockX = 400f;

	public void ResetToDock( BoatDefinition boat )
	{
		WorldX = DockX;
		Velocity = 0f;
		Fuel = boat?.FuelCapacity ?? 40f;
		Aboard = false;
		WakeStrength = 0f;
		Facing = 1f;
	}

	public void Board( BoatDefinition boat )
	{
		Aboard = true;
		WorldX = DockX;
		Velocity = 0f;
		Facing = 1f;
		// Always top off at the berth so capacity bumps apply immediately.
		Fuel = boat?.FuelCapacity ?? Fuel;
	}

	public void Disembark()
	{
		Aboard = false;
		Velocity = 0f;
		WorldX = DockX;
		WakeStrength = 0f;
	}

	public void CutThrottle()
	{
		Velocity = 0f;
		WakeStrength = 0f;
	}

	public void Tick( float dt, float throttle, BoatDefinition boat, WeatherService weather, out string limitMessage )
	{
		limitMessage = null;
		if ( !Aboard || boat is null )
			return;

		if ( Math.Abs( throttle ) > 0.01f )
			Facing = Math.Sign( throttle );

		var target = throttle * boat.TopSpeed;
		target *= Math.Clamp( 1.1f - weather.Wind * boat.WindResponse * 0.25f, 0.55f, 1.1f );
		var accel = throttle != 0 ? boat.Acceleration : boat.Braking;
		Velocity = Velocity.LerpTo( target, dt * accel / Math.Max( 20f, boat.TopSpeed ) );

		var prevX = WorldX;
		var next = WorldX + Velocity * dt;
		var offshore = Math.Max( 0f, next - DockX );
		if ( offshore >= boat.MaxRange )
		{
			next = DockX + boat.MaxRange;
			Velocity = Math.Min( 0f, Velocity );
			limitMessage = "Maximum safe range reached. Purchase a more capable boat to travel farther.";
		}

		// Can't sail past the dock berth toward shore — left returns you to the dock.
		if ( next < DockX )
		{
			next = DockX;
			Velocity = Math.Max( 0f, Velocity );
		}

		WorldX = next;

		// Distance-based burn: a full tank covers ~2.4× MaxRange (round trip + weather margin).
		var traveled = Math.Abs( WorldX - prevX );
		if ( traveled > 0.01f && boat.MaxRange > 1f && boat.FuelCapacity > 0f )
		{
			var weatherMul = 1f + weather.WaveIntensity * 0.35f + weather.Wind * 0.2f;
			var burnPerMeter = boat.FuelCapacity / ( boat.MaxRange * 2.4f );
			Fuel = Math.Max( 0f, Fuel - traveled * burnPerMeter * weatherMul );
		}

		BobPhase += dt * (1.2f + weather.WaveIntensity);
		RockAngle = MathF.Sin( BobPhase * 1.7f ) * (2.2f + weather.WaveIntensity * 4f) * boat.WaveResponse * 0.35f;
		WakeStrength = WakeStrength.LerpTo( Math.Clamp( Math.Abs( Velocity ) / boat.TopSpeed, 0f, 1f ), dt * 4f );
	}

	public float OffshoreDistance => Math.Max( 0f, WorldX - DockX );

	/// <summary>Estimated travel meters left on current fuel (matches distance-based burn).</summary>
	public float ReturnRangeEstimate( BoatDefinition boat )
	{
		if ( boat is null || boat.FuelCapacity <= 0f || boat.MaxRange <= 0f ) return 0f;
		return (Fuel / boat.FuelCapacity) * boat.MaxRange * 2.4f;
	}
}
