namespace Sandbox;

using Terraingen.Animals;

[Title( "Thorns Wildlife Vocalization" )]
[Category( "Thorns/Audio" )]
[Icon( "record_voice_over" )]
public sealed class ThornsWildlifeVocalization : Component
{
	[Property] public float AlertMinSeconds { get; set; } = 6f;
	[Property] public float AlertMaxSeconds { get; set; } = 14f;
	[Property] public float AmbientMinSeconds { get; set; } = 24f;
	[Property] public float AmbientMaxSeconds { get; set; } = 52f;
	[Property] public float BaseVolume { get; set; } = 0.85f;

	ThornsAnimalBrain _brain;
	double _nextVocalTime;

	protected override void OnStart()
	{
		_brain = Components.Get<ThornsAnimalBrain>();
		ScheduleNext( alert: false );
	}

	protected override void OnUpdate()
	{
		if ( !Game.IsPlaying || Application.IsDedicatedServer || Application.IsHeadless )
			return;

		_brain ??= Components.Get<ThornsAnimalBrain>();
		if ( !_brain.IsValid() || _brain.IsDead )
			return;

		var alert = IsAlertState( _brain.AiState );
		if ( Time.Now < _nextVocalTime )
			return;

		var path = ResolveSpeciesVocalPath( _brain.Species?.Key );
		if ( string.IsNullOrWhiteSpace( path ) )
		{
			ScheduleNext( alert );
			return;
		}

		ThornsWorldSpatialSfx.PlayFollowingOnGameObject(
			GameObject,
			path,
			ThornsSpatialSfxCategory.WildlifeVocal,
			BaseVolume,
			Vector3.Up * 36f );
		ScheduleNext( alert );
	}

	void ScheduleNext( bool alert )
	{
		var min = alert ? AlertMinSeconds : AmbientMinSeconds;
		var max = alert ? AlertMaxSeconds : AmbientMaxSeconds;
		_nextVocalTime = Time.Now + Game.Random.Float( MathF.Max( 0.5f, min ), MathF.Max( min, max ) );
	}

	static bool IsAlertState( ThornsAnimalState state ) =>
		state is ThornsAnimalState.Chase or ThornsAnimalState.Attack or ThornsAnimalState.Flee;

	static string ResolveSpeciesVocalPath( string speciesKey )
	{
		var key = speciesKey?.Trim() ?? "";
		if ( string.Equals( key, "wolf", StringComparison.OrdinalIgnoreCase ) )
			return "sounds/wildlife_vocal_wolf.sound";
		if ( string.Equals( key, "deer", StringComparison.OrdinalIgnoreCase ) )
			return "sounds/wildlife_vocal_deer.sound";
		if ( string.Equals( key, "moose", StringComparison.OrdinalIgnoreCase ) )
			return "sounds/wildlife_vocal_moose.sound";
		if ( string.Equals( key, "panther", StringComparison.OrdinalIgnoreCase ) )
			return "sounds/wildlife_vocal_panther.sound";

		return "";
	}
}
